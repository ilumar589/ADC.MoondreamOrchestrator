using Microsoft.Extensions.Logging;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// High-performance logging for VideoProcessingService using LoggerMessage source generators
/// </summary>
public static partial class VideoProcessingServiceLogMessages
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Uploading frame: {FileName}")]
    public static partial void LogFrameUpload(this ILogger logger, string fileName);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Frame uploaded successfully: {FileName}")]
    public static partial void LogFrameUploadSuccess(this ILogger logger, string fileName);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Error uploading frame: {FileName}")]
    public static partial void LogFrameUploadError(this ILogger logger, string fileName, Exception ex);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Starting video processing job {JobId} for video {VideoUrl}")]
    public static partial void LogVideoProcessingStart(this ILogger logger, string jobId, string videoUrl);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Processing video for job {JobId}")]
    public static partial void LogVideoProcessing(this ILogger logger, string jobId);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Video processing completed for job {JobId}. Processed {FrameCount} frames, found {DetectionCount} detections")]
    public static partial void LogVideoProcessingComplete(this ILogger logger, string jobId, int frameCount, int detectionCount);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Video processing completed for job {JobId}. No detections found")]
    public static partial void LogVideoProcessingNoDetections(this ILogger logger, string jobId);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Error,
        Message = "Error processing video for job {JobId}")]
    public static partial void LogVideoProcessingError(this ILogger logger, string jobId, Exception ex);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Drawing {Count} bounding boxes on frame")]
    public static partial void LogDrawingBoundingBoxes(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "Error cleaning up temporary files")]
    public static partial void LogCleanupError(this ILogger logger, Exception ex);
}
