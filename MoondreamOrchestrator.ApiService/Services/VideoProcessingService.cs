using Azure.Storage.Blobs;
using FFMpegCore;
using FFMpegCore.Pipes;
using System.Collections.Concurrent;
using System.Drawing;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// Service for processing videos with person detection and bounding boxes
/// </summary>
public class VideoProcessingService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly MoondreamService _moondreamService;
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly ConcurrentDictionary<string, VideoProcessResponse> _jobs;

    public VideoProcessingService(
        BlobServiceClient blobServiceClient,
        MoondreamService moondreamService,
        ILogger<VideoProcessingService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _moondreamService = moondreamService;
        _logger = logger;
        _jobs = new ConcurrentDictionary<string, VideoProcessResponse>();
    }

    /// <summary>
    /// Upload a video frame to Azure Storage
    /// </summary>
    public virtual async Task<string> UploadFrameAsync(string fileName, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogFrameUpload(fileName);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient("frames");
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(fileName);
            using var stream = new MemoryStream(data);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

            _logger.LogFrameUploadSuccess(fileName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogFrameUploadError(fileName, ex);
            throw;
        }
    }

    /// <summary>
    /// Start processing a video with person detection
    /// </summary>
    public virtual async Task<string> StartVideoProcessingAsync(string videoUrl, string personCharacteristics, double confidenceThreshold, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();
        
        _logger.LogVideoProcessingStart(jobId, videoUrl);

        // Initialize job status
        _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, 0, 0);

        // Start processing in background
        _ = Task.Run(async () => await ProcessVideoAsync(jobId, videoUrl, personCharacteristics, confidenceThreshold, cancellationToken), cancellationToken);

        return jobId;
    }

    /// <summary>
    /// Get job status
    /// </summary>
    public virtual VideoProcessResponse? GetJobStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var response) ? response : null;
    }

    private async Task ProcessVideoAsync(string jobId, string videoUrl, string personCharacteristics, double confidenceThreshold, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogVideoProcessing(jobId);

            // Download video from blob storage
            var containerClient = _blobServiceClient.GetBlobContainerClient("videos");
            var blobName = Path.GetFileName(new Uri(videoUrl).LocalPath);
            var blobClient = containerClient.GetBlobClient(blobName);

            var tempVideoPath = Path.Combine(Path.GetTempPath(), $"{jobId}_input.mp4");
            var outputVideoPath = Path.Combine(Path.GetTempPath(), $"{jobId}_output.mp4");

            await blobClient.DownloadToAsync(tempVideoPath, cancellationToken);

            // Extract frames and process
            var framesProcessed = 0;
            var detectionsFound = 0;
            var processedFrames = new List<string>();

            await foreach (var frameData in ExtractFramesAsync(tempVideoPath, cancellationToken))
            {
                var detections = await _moondreamService.DetectPersonAsync(frameData, personCharacteristics, cancellationToken);
                
                var validDetections = detections.Where(d => d.Confidence >= confidenceThreshold).ToArray();
                if (validDetections.Length > 0)
                {
                    detectionsFound += validDetections.Length;
                    var processedFrame = DrawBoundingBoxes(frameData, validDetections);
                    var framePath = Path.Combine(Path.GetTempPath(), $"{jobId}_frame_{framesProcessed}.jpg");
                    await File.WriteAllBytesAsync(framePath, processedFrame, cancellationToken);
                    processedFrames.Add(framePath);
                }

                framesProcessed++;
                
                // Update job status
                _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, framesProcessed, detectionsFound);
            }

            // Create output video from processed frames
            if (processedFrames.Count > 0)
            {
                await CreateVideoFromFramesAsync(processedFrames, outputVideoPath, cancellationToken);

                // Upload processed video
                var outputBlobClient = containerClient.GetBlobClient($"{jobId}_output.mp4");
                await using var outputStream = File.OpenRead(outputVideoPath);
                await outputBlobClient.UploadAsync(outputStream, overwrite: true, cancellationToken);

                // Update final status
                _jobs[jobId] = new VideoProcessResponse(jobId, "Completed", outputBlobClient.Uri.ToString(), framesProcessed, detectionsFound);

                _logger.LogVideoProcessingComplete(jobId, framesProcessed, detectionsFound);
            }
            else
            {
                _jobs[jobId] = new VideoProcessResponse(jobId, "Completed", null, framesProcessed, 0);
                _logger.LogVideoProcessingNoDetections(jobId);
            }

            // Cleanup temp files
            CleanupTempFiles(tempVideoPath, outputVideoPath, processedFrames);
        }
        catch (Exception ex)
        {
            _logger.LogVideoProcessingError(jobId, ex);
            _jobs[jobId] = new VideoProcessResponse(jobId, "Failed", null, 0, 0);
        }
    }

    private async IAsyncEnumerable<byte[]> ExtractFramesAsync(string videoPath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var mediaInfo = await FFProbe.AnalyseAsync(videoPath, cancellationToken: cancellationToken);
        var frameCount = mediaInfo.PrimaryVideoStream?.Duration.TotalSeconds ?? 0;

        for (var i = 0; i < frameCount; i++)
        {
            var tempFramePath = Path.Combine(Path.GetTempPath(), $"temp_frame_{i}.jpg");
            await FFMpeg.SnapshotAsync(videoPath, tempFramePath, new Size(640, 480), TimeSpan.FromSeconds(i));
            var frameData = await File.ReadAllBytesAsync(tempFramePath, cancellationToken);
            File.Delete(tempFramePath);
            yield return frameData;
        }
    }

    private byte[] DrawBoundingBoxes(byte[] imageData, PersonDetection[] detections)
    {
        // Simple bounding box drawing - in production, use a proper image processing library
        // For now, return the original image
        // TODO: Implement actual bounding box drawing with System.Drawing or SkiaSharp
        _logger.LogDrawingBoundingBoxes(detections.Length);
        return imageData;
    }

    private Task CreateVideoFromFramesAsync(List<string> framePaths, string outputPath, CancellationToken cancellationToken)
    {
        // Create video from frames using FFmpeg
        // This is a simplified version - proper implementation would use FFMpegArguments
        FFMpeg.JoinImageSequence(outputPath, frameRate: 1, framePaths.ToArray());
        return Task.CompletedTask;
    }

    private void CleanupTempFiles(string inputVideo, string outputVideo, List<string> frames)
    {
        try
        {
            if (File.Exists(inputVideo)) File.Delete(inputVideo);
            if (File.Exists(outputVideo)) File.Delete(outputVideo);
            foreach (var frame in frames)
            {
                if (File.Exists(frame)) File.Delete(frame);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCleanupError(ex);
        }
    }
}
