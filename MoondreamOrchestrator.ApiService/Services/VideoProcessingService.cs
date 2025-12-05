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
    private readonly BoundingBoxDrawer _boundingBoxDrawer;
    private readonly VideoFrameProcessor _frameProcessor;
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly ConcurrentDictionary<string, VideoProcessResponse> _jobs;

    public VideoProcessingService(
        BlobServiceClient blobServiceClient,
        MoondreamService moondreamService,
        BoundingBoxDrawer boundingBoxDrawer,
        VideoFrameProcessor frameProcessor,
        RetryPolicy retryPolicy,
        ILogger<VideoProcessingService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _moondreamService = moondreamService;
        _boundingBoxDrawer = boundingBoxDrawer;
        _frameProcessor = frameProcessor;
        _retryPolicy = retryPolicy;
        _logger = logger;
        _jobs = new ConcurrentDictionary<string, VideoProcessResponse>();
    }

    /// <summary>
    /// Upload a video frame to Azure Storage
    /// </summary>
    public virtual async Task<string> UploadFrameAsync(string fileName, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        _logger.LogFrameUpload(fileName);
        
        var retryOptions = new RetryOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            PerAttemptTimeout = TimeSpan.FromSeconds(60)
        };

        try
        {
            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("frames");
                await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

                var blobClient = containerClient.GetBlobClient(fileName);
                using var stream = new MemoryStream(data);
                await blobClient.UploadAsync(stream, overwrite: true, ct);

                _logger.LogFrameUploadSuccess(fileName);
                return blobClient.Uri.ToString();
            }, retryOptions, cancellationToken: cancellationToken);
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
    public virtual async Task<string> StartVideoProcessingAsync(string videoUrl, string personCharacteristics, double confidenceThreshold, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();
        
        _logger.LogVideoProcessingStart(jobId, videoUrl);

        // Initialize job status
        _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, 0, 0);

        // Start processing in background
        _ = Task.Run(async () => await ProcessVideoAsync(jobId, videoUrl, personCharacteristics, confidenceThreshold, options ?? VideoProcessingOptions.Default, cancellationToken), cancellationToken);

        return jobId;
    }

    /// <summary>
    /// Get job status
    /// </summary>
    public virtual VideoProcessResponse? GetJobStatus(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var response) ? response : null;
    }

    /// <summary>
    /// Start processing a batch of pre-extracted frames with person detection
    /// </summary>
    public virtual async Task<string> StartFrameBatchProcessingAsync(string[] frameUrls, string personCharacteristics, double confidenceThreshold, VideoProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();
        
        _logger.LogFrameBatchProcessingStart(jobId, frameUrls.Length);

        // Initialize job status
        _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, 0, 0);

        // Start processing in background
        _ = Task.Run(async () => await ProcessFrameBatchAsync(jobId, frameUrls, personCharacteristics, confidenceThreshold, options ?? VideoProcessingOptions.Default, cancellationToken), cancellationToken);

        return jobId;
    }

    private async Task ProcessVideoAsync(string jobId, string videoUrl, string personCharacteristics, double confidenceThreshold, VideoProcessingOptions options, CancellationToken cancellationToken)
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
            byte[]? previousFrame = null;

            await foreach (var frameData in ExtractFramesAsync(tempVideoPath, options, cancellationToken))
            {
                // Motion detection: skip similar frames
                if (options.EnableMotionDetection && previousFrame != null)
                {
                    if (_frameProcessor.AreFramesSimilar(previousFrame, frameData, options.MotionThreshold))
                    {
                        continue; // Skip this frame, it's too similar to the previous one
                    }
                }

                // Apply processing options (resize, grayscale)
                var processedFrameData = _frameProcessor.ProcessFrame(frameData, options);

                var detections = await _moondreamService.DetectPersonAsync(processedFrameData, personCharacteristics, cancellationToken);
                
                var validDetections = detections.Where(d => d.Confidence >= confidenceThreshold).ToArray();
                if (validDetections.Length > 0)
                {
                    detectionsFound += validDetections.Length;
                    var frameWithBoxes = DrawBoundingBoxes(processedFrameData, validDetections);
                    var framePath = Path.Combine(Path.GetTempPath(), $"{jobId}_frame_{framesProcessed}.jpg");
                    await File.WriteAllBytesAsync(framePath, frameWithBoxes, cancellationToken);
                    processedFrames.Add(framePath);
                }

                previousFrame = frameData;
                framesProcessed++;
                
                // Update job status
                _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, framesProcessed, detectionsFound);
            }

            // Create output video from processed frames
            if (processedFrames.Count > 0)
            {
                await CreateVideoFromFramesAsync(processedFrames, outputVideoPath, options, cancellationToken);

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

    private async Task ProcessFrameBatchAsync(string jobId, string[] frameUrls, string personCharacteristics, double confidenceThreshold, VideoProcessingOptions options, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogFrameBatchProcessing(jobId);

            var containerClient = _blobServiceClient.GetBlobContainerClient("frames");
            var outputVideoPath = Path.Combine(Path.GetTempPath(), $"{jobId}_output.mp4");

            // Process frames
            var framesProcessed = 0;
            var detectionsFound = 0;
            var processedFrames = new List<string>();
            byte[]? previousFrame = null;

            for (var i = 0; i < frameUrls.Length; i++)
            {
                var frameUrl = frameUrls[i];
                _logger.LogFrameDownload(i, frameUrl);

                // Download frame from blob storage
                if (!Uri.TryCreate(frameUrl, UriKind.Absolute, out var uri))
                {
                    _logger.LogFrameBatchProcessingError(jobId, new ArgumentException($"Invalid frame URL: {frameUrl}"));
                    continue;
                }
                
                var blobName = Path.GetFileName(uri.LocalPath);
                var blobClient = containerClient.GetBlobClient(blobName);

                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream, cancellationToken);
                var frameData = memoryStream.ToArray();

                // Motion detection: skip similar frames
                if (options.EnableMotionDetection && previousFrame != null)
                {
                    if (_frameProcessor.AreFramesSimilar(previousFrame, frameData, options.MotionThreshold))
                    {
                        continue;
                    }
                }

                // Apply processing options (resize, grayscale)
                var processedFrameData = _frameProcessor.ProcessFrame(frameData, options);

                // Process frame for person detection
                var detections = await _moondreamService.DetectPersonAsync(processedFrameData, personCharacteristics, cancellationToken);
                
                var validDetections = detections.Where(d => d.Confidence >= confidenceThreshold).ToArray();
                if (validDetections.Length > 0)
                {
                    detectionsFound += validDetections.Length;
                    var frameWithBoxes = DrawBoundingBoxes(processedFrameData, validDetections);
                    var framePath = Path.Combine(Path.GetTempPath(), $"{jobId}_frame_{i}.jpg");
                    await File.WriteAllBytesAsync(framePath, frameWithBoxes, cancellationToken);
                    processedFrames.Add(framePath);
                }

                previousFrame = frameData;
                framesProcessed++;
                
                // Update job status
                _jobs[jobId] = new VideoProcessResponse(jobId, "Processing", null, framesProcessed, detectionsFound);
            }

            // Create output video from processed frames
            if (processedFrames.Count > 0)
            {
                await CreateVideoFromFramesAsync(processedFrames, outputVideoPath, options, cancellationToken);

                // Upload processed video
                var containerClient2 = _blobServiceClient.GetBlobContainerClient("videos");
                var outputBlobClient = containerClient2.GetBlobClient($"{jobId}_output.mp4");
                await using var outputStream = File.OpenRead(outputVideoPath);
                await outputBlobClient.UploadAsync(outputStream, overwrite: true, cancellationToken);

                // Update final status
                _jobs[jobId] = new VideoProcessResponse(jobId, "Completed", outputBlobClient.Uri.ToString(), framesProcessed, detectionsFound);

                _logger.LogFrameBatchProcessingComplete(jobId, framesProcessed, detectionsFound);
            }
            else
            {
                _jobs[jobId] = new VideoProcessResponse(jobId, "Completed", null, framesProcessed, 0);
                _logger.LogFrameBatchProcessingNoDetections(jobId);
            }

            // Cleanup temp files
            CleanupTempFiles(string.Empty, outputVideoPath, processedFrames);
        }
        catch (Exception ex)
        {
            _logger.LogFrameBatchProcessingError(jobId, ex);
            _jobs[jobId] = new VideoProcessResponse(jobId, "Failed", null, 0, 0);
        }
    }

    private async IAsyncEnumerable<byte[]> ExtractFramesAsync(string videoPath, VideoProcessingOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var mediaInfo = await FFProbe.AnalyseAsync(videoPath, cancellationToken: cancellationToken);
        var duration = mediaInfo.PrimaryVideoStream?.Duration.TotalSeconds ?? 0;
        var fps = options.TargetFps ?? 1; // Default 1 frame per second

        var frameInterval = 1.0 / fps;
        var frameCount = (int)(duration * fps);

        for (var i = 0; i < frameCount; i++)
        {
            var timestamp = TimeSpan.FromSeconds(i * frameInterval);
            var tempFramePath = Path.Combine(Path.GetTempPath(), $"temp_frame_{i}.jpg");
            await FFMpeg.SnapshotAsync(videoPath, tempFramePath, new Size(640, 480), timestamp);
            var frameData = await File.ReadAllBytesAsync(tempFramePath, cancellationToken);
            File.Delete(tempFramePath);
            yield return frameData;
        }
    }

    private byte[] DrawBoundingBoxes(byte[] imageData, PersonDetection[] detections)
    {
        _logger.LogDrawingBoundingBoxes(detections.Length);
        return _boundingBoxDrawer.DrawBoundingBoxes(imageData, detections);
    }

    private Task CreateVideoFromFramesAsync(List<string> framePaths, string outputPath, VideoProcessingOptions options, CancellationToken cancellationToken)
    {
        var fps = options.TargetFps ?? 1;
        FFMpeg.JoinImageSequence(outputPath, frameRate: fps, framePaths.ToArray());
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
