using System.Text;
using System.Text.Json;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// Service for interacting with local Moondream instance
/// </summary>
public class MoondreamService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MoondreamService> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly string _moondreamUrl;

    public MoondreamService(HttpClient httpClient, ILogger<MoondreamService> logger, RetryPolicy retryPolicy, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _retryPolicy = retryPolicy;
        _moondreamUrl = configuration["Moondream:Url"] ?? "http://localhost:5000";
    }

    /// <summary>
    /// Detects persons in an image based on user-defined characteristics
    /// </summary>
    public virtual async Task<PersonDetection[]> DetectPersonAsync(byte[] imageData, string characteristics, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDetectionRequest(characteristics);

            var retryOptions = new RetryOptions
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(0.5),
                MaxDelay = TimeSpan.FromSeconds(10),
                PerAttemptTimeout = TimeSpan.FromSeconds(30)
            };

            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                var requestContent = new
                {
                    image = Convert.ToBase64String(imageData),
                    prompt = $"Detect person with these characteristics: {characteristics}. Return bounding box coordinates.",
                    task = "object_detection"
                };

                var json = JsonSerializer.Serialize(requestContent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_moondreamUrl}/detect", content, ct);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<MoondreamResponse>(responseJson);

                if (result?.Detections == null)
                {
                    return Array.Empty<PersonDetection>();
                }

                return result.Detections.Select(d => new PersonDetection(
                    characteristics,
                    new BoundingBox(d.Box.X, d.Box.Y, d.Box.Width, d.Box.Height),
                    d.Confidence
                )).ToArray();
            }, retryOptions, RetryPolicy.CreateHttpRetryPredicate(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDetectionError(ex);
            return Array.Empty<PersonDetection>();
        }
    }

    private sealed class MoondreamResponse
    {
        public Detection[]? Detections { get; set; }
    }

    private sealed class Detection
    {
        public BoundingBoxDto Box { get; set; } = new();
        public double Confidence { get; set; }
    }

    private sealed class BoundingBoxDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
