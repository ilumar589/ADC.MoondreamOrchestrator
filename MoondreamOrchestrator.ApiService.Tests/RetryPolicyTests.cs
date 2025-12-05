using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MoondreamOrchestrator.ApiService.Services;

namespace MoondreamOrchestrator.ApiService.Tests;

public class RetryPolicyTests
{
    private readonly RetryPolicy _retryPolicy;
    private readonly Mock<ILogger<RetryPolicy>> _loggerMock;

    public RetryPolicyTests()
    {
        _loggerMock = new Mock<ILogger<RetryPolicy>>();
        _retryPolicy = new RetryPolicy(_loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await _retryPolicy.ExecuteAsync(
            ct => Task.FromResult(expectedResult));

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientErrorThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        var options = new RetryOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        };

        // Act
        var result = await _retryPolicy.ExecuteAsync(
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new HttpRequestException("Transient error");
                }
                return Task.FromResult("success");
            },
            options);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithPermanentError_FailsImmediately()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _retryPolicy.ExecuteAsync(
                ct =>
                {
                    attemptCount++;
                    throw new ArgumentException("Permanent error");
                });
        });

        attemptCount.Should().Be(1); // Should not retry
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsAllRetries_ThrowsInvalidOperationException()
    {
        // Arrange
        var attemptCount = 0;
        var options = new RetryOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            UseJitter = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _retryPolicy.ExecuteAsync(
                ct =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Always fails");
                },
                options);
        });

        attemptCount.Should().Be(3);
        exception.Message.Should().Contain("3 attempts");
        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomShouldRetry_UsesCustomLogic()
    {
        // Arrange
        var attemptCount = 0;
        var options = new RetryOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(10)
        };

        // Custom retry logic: only retry ArgumentException
        Func<Exception, bool> shouldRetry = ex => ex is ArgumentException;

        // Act
        var result = await _retryPolicy.ExecuteAsync(
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new ArgumentException("Retry this");
                }
                return Task.FromResult("success");
            },
            options,
            shouldRetry);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_ExecutesSuccessfully()
    {
        // Arrange
        var executed = false;

        // Act
        await _retryPolicy.ExecuteAsync(
            ct =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithHttpRequestException_ReturnsTrue()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act & Assert
        RetryPolicy.IsTransientError(exception).Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithTaskCanceledException_ReturnsTrue()
    {
        // Arrange
        var exception = new TaskCanceledException("Timeout");

        // Act & Assert
        RetryPolicy.IsTransientError(exception).Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithTimeoutException_ReturnsTrue()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act & Assert
        RetryPolicy.IsTransientError(exception).Should().BeTrue();
    }

    [Fact]
    public void IsTransientError_WithArgumentException_ReturnsFalse()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act & Assert
        RetryPolicy.IsTransientError(exception).Should().BeFalse();
    }

    [Fact]
    public void CreateHttpRetryPredicate_RetriesOn500StatusCode()
    {
        // Arrange
        var predicate = RetryPolicy.CreateHttpRetryPredicate();
        var exception = new HttpRequestException("Server error", null, System.Net.HttpStatusCode.InternalServerError);

        // Act & Assert
        predicate(exception).Should().BeTrue();
    }

    [Fact]
    public void CreateHttpRetryPredicate_DoesNotRetryOn400StatusCode()
    {
        // Arrange
        var predicate = RetryPolicy.CreateHttpRetryPredicate();
        var exception = new HttpRequestException("Client error", null, System.Net.HttpStatusCode.BadRequest);

        // Act & Assert
        predicate(exception).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithPerAttemptTimeout_RespectsSingleAttemptTimeout()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxAttempts = 1,
            PerAttemptTimeout = TimeSpan.FromMilliseconds(100)
        };

        // Act & Assert - Operation taking too long should timeout and wrap in InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _retryPolicy.ExecuteAsync(
                async ct =>
                {
                    await Task.Delay(500, ct); // Longer than timeout
                    return "should not reach here";
                },
                options);
        });
        
        // The inner exception should be TaskCanceledException or OperationCanceledException
        ex.InnerException.Should().BeAssignableTo<OperationCanceledException>();
    }
}
