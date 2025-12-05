# ADC.MoondreamOrchestrator

A high-performance .NET 10 WebAPI application that uses a local Moondream instance for person recognition based on user-defined characteristics. The application processes video frames, uploads them to Azure Storage (deployed locally as an emulator using Aspire), and uses FFmpeg C# bindings to create videos with bounding boxes around detected persons.

## Features

- **Minimal APIs** with .NET 10 for high performance
- **Azure Storage Emulator** integration via Aspire for local development
- **Moondream Integration** for AI-powered person recognition
- **FFmpeg Video Processing** for frame extraction and video generation
- **Struct-based DTOs** for minimal memory allocation and high performance
- **High-Performance Logging** using LoggerMessage source generators (zero-allocation logging)
- **OpenAPI/Swagger** support for API documentation
- **Comprehensive Test Suite** with 19 passing tests

## Architecture

The solution consists of:

- **MoondreamOrchestrator.ApiService**: Main WebAPI service with minimal APIs
- **MoondreamOrchestrator.AppHost**: Aspire AppHost for orchestration
- **MoondreamOrchestrator.ServiceDefaults**: Shared service configurations
- **MoondreamOrchestrator.Web**: Blazor web frontend (scaffolded, can be customized)

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for Azure Storage Emulator)
- FFmpeg (will be used by FFMpegCore)
- Local Moondream instance running on `http://localhost:5000`

## Configuration

Update `appsettings.json` in the ApiService project to configure Moondream URL:

```json
{
  "Moondream": {
    "Url": "http://localhost:5000"
  }
}
```

## API Endpoints

### 1. Upload Frame
```
POST /api/frames/upload
Content-Type: multipart/form-data
```
Upload a video frame to Azure Storage.

### 2. Process Video
```
POST /api/videos/process
Content-Type: application/json

{
  "videoUrl": "blob-storage-url",
  "personCharacteristics": "person wearing red shirt",
  "confidenceThreshold": 0.5
}
```
Start processing a video with person detection based on characteristics.

### 3. Process Frame Batch
```
POST /api/frames/process-batch
Content-Type: application/json

{
  "frameUrls": [
    "blob-storage-url-frame1",
    "blob-storage-url-frame2",
    "blob-storage-url-frame3"
  ],
  "personCharacteristics": "person wearing red shirt",
  "confidenceThreshold": 0.5
}
```
Start processing a batch of pre-extracted frames with person detection. This endpoint allows users to submit frames that have already been extracted from a video, skipping the video-to-frames conversion step.

### 4. Get Job Status
```
GET /api/videos/status/{jobId}
```
Get the status of a video or frame batch processing job.

### 5. Detect Person in Image
```
POST /api/detect/person
Content-Type: multipart/form-data
```
Detect person in a single image based on characteristics.

## Running the Application

1. **Start the Aspire AppHost**:
```bash
cd MoondreamOrchestrator.AppHost
dotnet run
```

This will start:
- Azure Storage Emulator (via Aspire)
- The API Service
- The Web Frontend
- Aspire Dashboard for monitoring

2. **Access the Application**:
- API Service: Check Aspire Dashboard for the URL
- Swagger UI: Navigate to `/openapi` endpoint in development mode
- Aspire Dashboard: Usually at `http://localhost:15000` or similar

## Data Models

The application uses struct-based DTOs for performance:

- `BoundingBox`: Coordinates for detected persons
- `PersonDetection`: Detection result with confidence
- `VideoProcessRequest`: Request to process a video
- `VideoProcessResponse`: Job status and results
- `FrameUploadRequest`: Frame upload data
- `FrameBatchProcessRequest`: Request to process a batch of pre-extracted frames

## How It Works

### Video Processing Workflow
1. **Frame Upload**: Video frames are uploaded to Azure Blob Storage via the `/api/frames/upload` endpoint
2. **Video Processing**: Submit a video URL and person characteristics to `/api/videos/process`
3. **Frame Extraction**: FFmpeg extracts frames from the video
4. **Person Detection**: Each frame is sent to Moondream for person detection
5. **Bounding Boxes**: Detected persons meeting the confidence threshold get bounding boxes
6. **Video Generation**: Processed frames are combined into a new video with bounding boxes
7. **Result Storage**: The processed video is uploaded to Azure Storage

### Frame Batch Processing Workflow
1. **Pre-extracted Frames**: Users upload frames to Azure Blob Storage via `/api/frames/upload` or have them pre-stored
2. **Batch Processing**: Submit an array of frame URLs and person characteristics to `/api/frames/process-batch`
3. **Frame Download**: Each frame is downloaded from blob storage
4. **Person Detection**: Each frame is sent to Moondream for person detection
5. **Bounding Boxes**: Detected persons meeting the confidence threshold get bounding boxes
6. **Video Generation**: Processed frames are combined into a new video with bounding boxes
7. **Result Storage**: The processed video is uploaded to Azure Storage

This dual approach provides flexibility: users can process complete videos (with automatic frame extraction) or submit pre-extracted frames directly (skipping the extraction step).

## Development Notes

- The solution uses minimal abstractions for performance
- Struct-based DTOs reduce heap allocations
- High-performance logging is configured via ServiceDefaults
- Azure Storage runs as an emulator locally via Aspire
- FFmpeg binaries are required for video processing

## Performance Considerations

- Structs are used for DTOs to minimize heap allocations
- Background tasks process videos asynchronously
- Minimal API endpoints reduce overhead
- Direct Azure Storage client usage (no additional abstractions)
- **High-performance logging** using LoggerMessage source generators:
  - Zero-allocation logging with compile-time code generation
  - Structured logging with event IDs for better observability
  - See [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging) for details

## Testing

The solution includes comprehensive tests (23 tests, all passing):

```bash
dotnet test
```

Test categories:
- **Models Tests**: Validates struct behavior and immutability (13 tests)
- **MoondreamService Tests**: Tests HTTP integration and person detection logic (6 tests)
- **VideoProcessingService Tests**: Validates video processing workflows (3 tests)
- **API Endpoint Tests**: Integration tests for API endpoints (1 test)
- **Bounding Box Drawing Tests**: Tests actual bounding box rendering (10 tests)
- **Video Processing Options Tests**: Tests new video processing features (5 tests)
- **Retry Policy Tests**: Tests retry logic and error handling (13 tests)

**Total: 51 tests, all passing ✅**

## Advanced Features

### Bounding Box Drawing

The application now includes production-ready bounding box drawing with configurable options:

```json
{
  "processingOptions": {
    "targetResolution": "1920x1080",
    "preserveAspectRatio": true,
    "letterbox": true
  }
}
```

Features:
- Configurable colors, thickness, and rounded corners
- Label text with class name and confidence scores
- Per-class color support or deterministic color generation
- Cross-platform using SkiaSharp

### Video Processing Options

New sophisticated options for video processing:

```json
{
  "videoUrl": "https://...",
  "personCharacteristics": "person with red shirt",
  "confidenceThreshold": 0.7,
  "processingOptions": {
    "targetFps": 24,
    "targetResolution": "1280x720",
    "preserveAspectRatio": true,
    "letterbox": true,
    "grayscale": false,
    "codec": "libx264",
    "quality": 23,
    "enableMotionDetection": true,
    "motionThreshold": 0.05
  }
}
```

Options:
- **targetFps**: Output frame rate (resamples frames)
- **targetResolution**: Output resolution (e.g., "1920x1080")
- **preserveAspectRatio**: Maintain aspect ratio when resizing
- **letterbox**: Add black bars if needed
- **grayscale**: Convert to grayscale
- **codec**: Video codec (default: libx264)
- **quality**: CRF quality (0-51, lower is better)
- **enableMotionDetection**: Skip near-duplicate frames
- **motionThreshold**: Similarity threshold (0.0-1.0)

### Error Handling and Retry Logic

Automatic retry with exponential backoff for:
- Moondream API calls
- Azure Blob Storage operations
- Network timeouts and transient errors

Configuration in code:
```csharp
var retryOptions = new RetryOptions
{
    MaxAttempts = 3,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromSeconds(30),
    BackoffMultiplier = 2.0,
    UseJitter = true
};
```

### Telemetry and Monitoring

#### Configuration

Add to `appsettings.json`:
```json
{
  "Telemetry": {
    "Enabled": true,
    "FilePath": "logs/telemetry.jsonl"
  }
}
```

#### Metrics Endpoint

Access Prometheus-style metrics at:
```
GET /metrics
```

Sample output:
```
frames_processed_count 150
detections_found_count 45
processing_time_seconds_count 10
processing_time_seconds_sum 125.5
processing_time_seconds{quantile="0.5"} 12.1
processing_time_seconds{quantile="0.95"} 18.7
processing_time_seconds{quantile="0.99"} 22.3
```

#### Telemetry Events

Telemetry events are written to JSON lines format:
```json
{"EventName":"VideoProcessingStarted","Timestamp":"2025-12-05T20:00:00Z","JobId":"abc-123","Properties":{},"Metrics":{}}
{"EventName":"FrameProcessed","Timestamp":"2025-12-05T20:00:01Z","JobId":"abc-123","Metrics":{"ProcessingTimeMs":150}}
```
