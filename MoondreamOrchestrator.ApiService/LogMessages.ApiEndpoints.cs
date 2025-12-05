using Microsoft.Extensions.Logging;

namespace MoondreamOrchestrator.ApiService;

/// <summary>
/// High-performance logging for API endpoints using LoggerMessage source generators
/// </summary>
public static partial class ApiEndpointLogMessages
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Invalid file upload attempt")]
    public static partial void LogInvalidFileUpload(this ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Frame uploaded successfully: {FileName} -> {Url}")]
    public static partial void LogFrameUploadComplete(this ILogger logger, string fileName, string url);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Starting video processing for: {VideoUrl} with characteristics: {Characteristics}")]
    public static partial void LogVideoProcessRequest(this ILogger logger, string videoUrl, string characteristics);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "Job not found: {JobId}")]
    public static partial void LogJobNotFound(this ILogger logger, string jobId);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Information,
        Message = "Job status retrieved: {JobId} - {Status}")]
    public static partial void LogJobStatusRetrieved(this ILogger logger, string jobId, string status);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Warning,
        Message = "Invalid image upload for person detection")]
    public static partial void LogInvalidImageUpload(this ILogger logger);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "Detecting person with characteristics: {Characteristics}")]
    public static partial void LogPersonDetectionRequest(this ILogger logger, string characteristics);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Information,
        Message = "Starting frame batch processing for {FrameCount} frames with characteristics: {Characteristics}")]
    public static partial void LogFrameBatchProcessRequest(this ILogger logger, int frameCount, string characteristics);
}
