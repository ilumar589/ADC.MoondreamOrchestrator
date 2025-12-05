using MoondreamOrchestrator.ApiService;
using MoondreamOrchestrator.ApiService.Services;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Azure Blob Storage client
builder.AddAzureBlobServiceClient("blobs");

// Add services to the container.
builder.Services.AddProblemDetails();

// Add HttpClient for Moondream service
builder.Services.AddHttpClient<MoondreamService>();

// Add custom services
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<MoondreamService>();
builder.Services.AddSingleton<BoundingBoxDrawer>();
builder.Services.AddSingleton<VideoFrameProcessor>();
builder.Services.AddSingleton<VideoProcessingService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Root endpoint
app.MapGet("/", () => "Moondream Orchestrator API - Video Processing with Person Detection")
    .WithName("Root")
    .WithSummary("API information");

// Upload video frame endpoint
app.MapPost("/api/frames/upload", async (
    IFormFile file,
    VideoProcessingService videoService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (file == null || file.Length == 0)
    {
        logger.LogInvalidFileUpload();
        return Results.BadRequest("No file uploaded");
    }

    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream, cancellationToken);
    var data = memoryStream.ToArray();

    var url = await videoService.UploadFrameAsync(file.FileName, data, file.ContentType, cancellationToken);
    
    logger.LogFrameUploadComplete(file.FileName, url);
    return Results.Ok(new { url, fileName = file.FileName });
})
.WithName("UploadFrame")
.WithSummary("Upload a video frame to Azure Storage")
.DisableAntiforgery();

// Start video processing endpoint
app.MapPost("/api/videos/process", async (
    VideoProcessRequest request,
    VideoProcessingService videoService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogVideoProcessRequest(request.VideoUrl, request.PersonCharacteristics);

    var jobId = await videoService.StartVideoProcessingAsync(
        request.VideoUrl,
        request.PersonCharacteristics,
        request.ConfidenceThreshold,
        request.ProcessingOptions,
        cancellationToken);

    return Results.Accepted($"/api/videos/status/{jobId}", new { jobId });
})
.WithName("ProcessVideo")
.WithSummary("Start processing a video with person detection");

// Start frame batch processing endpoint
app.MapPost("/api/frames/process-batch", async (
    FrameBatchProcessRequest request,
    VideoProcessingService videoService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.FrameUrls == null || request.FrameUrls.Length == 0)
    {
        return Results.BadRequest("No frame URLs provided");
    }

    logger.LogFrameBatchProcessRequest(request.FrameUrls.Length, request.PersonCharacteristics);

    var jobId = await videoService.StartFrameBatchProcessingAsync(
        request.FrameUrls,
        request.PersonCharacteristics,
        request.ConfidenceThreshold,
        request.ProcessingOptions,
        cancellationToken);

    return Results.Accepted($"/api/videos/status/{jobId}", new { jobId });
})
.WithName("ProcessFrameBatch")
.WithSummary("Start processing a batch of pre-extracted frames with person detection");

// Get video processing status endpoint
app.MapGet("/api/videos/status/{jobId}", (
    string jobId,
    VideoProcessingService videoService,
    ILogger<Program> logger) =>
{
    var status = videoService.GetJobStatus(jobId);
    
    if (status == null)
    {
        logger.LogJobNotFound(jobId);
        return Results.NotFound(new { error = "Job not found" });
    }

    logger.LogJobStatusRetrieved(jobId, status.Value.Status);
    return Results.Ok(status.Value);
})
.WithName("GetJobStatus")
.WithSummary("Get the status of a video processing job");

// Detect person in image endpoint
app.MapPost("/api/detect/person", async (
    IFormFile image,
    string characteristics,
    MoondreamService moondreamService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (image == null || image.Length == 0)
    {
        logger.LogInvalidImageUpload();
        return Results.BadRequest("No image uploaded");
    }

    using var memoryStream = new MemoryStream();
    await image.CopyToAsync(memoryStream, cancellationToken);
    var imageData = memoryStream.ToArray();

    logger.LogPersonDetectionRequest(characteristics);
    var detections = await moondreamService.DetectPersonAsync(imageData, characteristics, cancellationToken);

    return Results.Ok(new { detections = detections.Select(d => new
    {
        characteristics = d.Characteristics,
        boundingBox = new
        {
            x = d.BoundingBox.X,
            y = d.BoundingBox.Y,
            width = d.BoundingBox.Width,
            height = d.BoundingBox.Height
        },
        confidence = d.Confidence
    })});
})
.WithName("DetectPerson")
.WithSummary("Detect person in an image based on characteristics")
.DisableAntiforgery();

app.MapDefaultEndpoints();

app.Run();
public partial class Program { }
