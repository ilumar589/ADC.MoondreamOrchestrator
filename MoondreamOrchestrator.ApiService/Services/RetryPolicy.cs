namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// Configuration options for retry policy
/// </summary>
public record RetryOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public double BackoffMultiplier { get; init; } = 2.0;
    public bool UseJitter { get; init; } = true;
    public TimeSpan? PerAttemptTimeout { get; init; } = null;
}

/// <summary>
/// Retry policy utility with exponential backoff and jitter
/// </summary>
public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly Random _random;

    public RetryPolicy(ILogger<RetryPolicy> logger)
    {
        _logger = logger;
        _random = new Random();
    }

    /// <summary>
    /// Executes an operation with retry logic
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryOptions? options = null,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RetryOptions();
        shouldRetry ??= IsTransientError;

        Exception? lastException = null;
        var attempt = 0;

        while (attempt < options.MaxAttempts)
        {
            attempt++;
            
            try
            {
                _logger.LogDebug("Retry attempt {Attempt} of {MaxAttempts}", attempt, options.MaxAttempts);

                if (options.PerAttemptTimeout.HasValue)
                {
                    using var timeoutCts = new CancellationTokenSource(options.PerAttemptTimeout.Value);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    return await operation(linkedCts.Token);
                }
                else
                {
                    return await operation(cancellationToken);
                }
            }
            catch (Exception ex) when (shouldRetry(ex))
            {
                lastException = ex;
                
                if (attempt < options.MaxAttempts)
                {
                    var delay = CalculateDelay(attempt, options);
                    
                    _logger.LogWarning(ex, 
                        "Attempt {Attempt} failed. Retrying after {Delay}ms", 
                        attempt, delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                }
                // On last attempt, fall through to throw wrapped exception
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed on attempt {Attempt} (no retry)", attempt);
                throw;
            }
        }

        _logger.LogError(lastException, "All {MaxAttempts} retry attempts exhausted", options.MaxAttempts);
        throw new InvalidOperationException(
            $"Operation failed after {options.MaxAttempts} attempts. See inner exception for details.",
            lastException);
    }

    /// <summary>
    /// Executes an operation with retry logic (non-generic version)
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        RetryOptions? options = null,
        Func<Exception, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            options,
            shouldRetry,
            cancellationToken);
    }

    private TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        // Calculate exponential backoff: initialDelay * (multiplier ^ (attempt - 1))
        var exponentialDelay = options.InitialDelay.TotalMilliseconds * 
                               Math.Pow(options.BackoffMultiplier, attempt - 1);

        // Cap at max delay
        var delay = Math.Min(exponentialDelay, options.MaxDelay.TotalMilliseconds);

        // Add jitter if enabled (randomize ±20%)
        if (options.UseJitter)
        {
            var jitter = delay * 0.2; // ±20%
            delay = delay + (_random.NextDouble() * jitter * 2 - jitter);
        }

        return TimeSpan.FromMilliseconds(delay);
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried
    /// </summary>
    public static bool IsTransientError(Exception exception)
    {
        return exception switch
        {
            // Network errors
            HttpRequestException => true,
            TaskCanceledException => true,
            OperationCanceledException => true,
            TimeoutException => true,
            
            // IO errors that might be transient
            IOException io when IsTransientIoError(io) => true,
            
            // Azure Storage errors (5xx status codes typically)
            Azure.RequestFailedException azure when azure.Status >= 500 && azure.Status < 600 => true,
            
            _ => false
        };
    }

    private static bool IsTransientIoError(IOException exception)
    {
        // Common transient IO errors
        var message = exception.Message.ToLowerInvariant();
        return message.Contains("timeout") ||
               message.Contains("network") ||
               message.Contains("connection");
    }

    /// <summary>
    /// Creates a should-retry predicate that also retries on specific HTTP status codes
    /// </summary>
    public static Func<Exception, bool> CreateHttpRetryPredicate(params int[] additionalStatusCodes)
    {
        return ex =>
        {
            // For HTTP exceptions, check status code first
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode.HasValue)
                {
                    var statusCode = (int)httpEx.StatusCode.Value;
                    // Retry on server errors (5xx) or additional specified codes
                    return (statusCode >= 500 && statusCode < 600) || 
                           additionalStatusCodes.Contains(statusCode);
                }
                // HttpRequestException without status code (e.g., network failure) should retry
                return true;
            }

            // For non-HTTP exceptions, use standard transient error logic
            return IsTransientError(ex);
        };
    }
}
