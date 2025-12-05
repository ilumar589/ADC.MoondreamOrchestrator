using Microsoft.Extensions.Logging;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// High-performance logging for MoondreamService using LoggerMessage source generators
/// </summary>
public static partial class MoondreamServiceLogMessages
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Sending image to Moondream for person detection with characteristics: {Characteristics}")]
    public static partial void LogDetectionRequest(this ILogger logger, string characteristics);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Error detecting person in image")]
    public static partial void LogDetectionError(this ILogger logger, Exception ex);
}
