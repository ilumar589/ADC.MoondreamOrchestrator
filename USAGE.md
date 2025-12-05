# Moondream Orchestrator - Usage Examples

This document provides practical examples for using the Moondream Orchestrator API.

## Prerequisites

1. **Start the Application**
   ```bash
   cd MoondreamOrchestrator.AppHost
   dotnet run
   ```

2. **Ensure Moondream is Running**
   - The local Moondream instance should be accessible at `http://localhost:5000`
   - Update `appsettings.json` if using a different URL

3. **Access Aspire Dashboard**
   - Open the Aspire dashboard URL shown in the console (usually `http://localhost:15000` or similar)
   - Note the API Service endpoint URL

## Example 1: Upload a Video Frame

```bash
curl -X POST "http://localhost:5398/api/frames/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@path/to/your/frame.jpg"
```

Response:
```json
{
  "url": "https://127.0.0.1:10000/devstoreaccount1/frames/frame.jpg",
  "fileName": "frame.jpg"
}
```

## Example 2: Detect Person in a Single Image

```bash
curl -X POST "http://localhost:5398/api/detect/person?characteristics=person%20wearing%20red%20shirt" \
  -H "Content-Type: multipart/form-data" \
  -F "image=@path/to/your/image.jpg"
```

Response:
```json
{
  "detections": [
    {
      "characteristics": "person wearing red shirt",
      "boundingBox": {
        "x": 120.5,
        "y": 85.3,
        "width": 200.0,
        "height": 350.0
      },
      "confidence": 0.87
    }
  ]
}
```

## Example 3: Process a Video with Person Detection

### Step 1: Start Video Processing

```bash
curl -X POST "http://localhost:5398/api/videos/process" \
  -H "Content-Type: application/json" \
  -d '{
    "videoUrl": "https://127.0.0.1:10000/devstoreaccount1/videos/sample.mp4",
    "personCharacteristics": "person wearing blue jeans and white t-shirt",
    "confidenceThreshold": 0.6
  }'
```

Response:
```json
{
  "jobId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Step 2: Check Job Status

```bash
curl -X GET "http://localhost:5398/api/videos/status/a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

Response (Processing):
```json
{
  "jobId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Processing",
  "processedVideoUrl": null,
  "framesProcessed": 45,
  "detectionsFound": 12
}
```

Response (Completed):
```json
{
  "jobId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Completed",
  "processedVideoUrl": "https://127.0.0.1:10000/devstoreaccount1/videos/a1b2c3d4_output.mp4",
  "framesProcessed": 120,
  "detectionsFound": 35
}
```

## Example 4: Using C# HttpClient

```csharp
using System.Net.Http;
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5398") };

// Upload a frame
using var fileStream = File.OpenRead("frame.jpg");
using var content = new MultipartFormDataContent();
content.Add(new StreamContent(fileStream), "file", "frame.jpg");

var uploadResponse = await client.PostAsync("/api/frames/upload", content);
var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResult>();
Console.WriteLine($"Uploaded to: {uploadResult.Url}");

// Process a video
var processRequest = new
{
    videoUrl = uploadResult.Url,
    personCharacteristics = "person with red backpack",
    confidenceThreshold = 0.5
};

var processResponse = await client.PostAsJsonAsync("/api/videos/process", processRequest);
var jobResult = await processResponse.Content.ReadFromJsonAsync<JobResult>();
Console.WriteLine($"Job ID: {jobResult.JobId}");

// Poll for status
while (true)
{
    await Task.Delay(2000); // Wait 2 seconds
    
    var statusResponse = await client.GetAsync($"/api/videos/status/{jobResult.JobId}");
    var status = await statusResponse.Content.ReadFromJsonAsync<StatusResult>();
    
    Console.WriteLine($"Status: {status.Status}, Frames: {status.FramesProcessed}");
    
    if (status.Status == "Completed" || status.Status == "Failed")
    {
        if (status.ProcessedVideoUrl != null)
        {
            Console.WriteLine($"Processed video: {status.ProcessedVideoUrl}");
        }
        break;
    }
}

record UploadResult(string Url, string FileName);
record JobResult(string JobId);
record StatusResult(string JobId, string Status, string? ProcessedVideoUrl, int FramesProcessed, int DetectionsFound);
```

## Example 5: Using Python

```python
import requests
import time
import json

base_url = "http://localhost:5398"

# Upload a frame
with open("frame.jpg", "rb") as f:
    files = {"file": f}
    response = requests.post(f"{base_url}/api/frames/upload", files=files)
    upload_result = response.json()
    print(f"Uploaded to: {upload_result['url']}")

# Process video
process_data = {
    "videoUrl": upload_result["url"],
    "personCharacteristics": "person wearing sunglasses",
    "confidenceThreshold": 0.6
}

response = requests.post(f"{base_url}/api/videos/process", json=process_data)
job_result = response.json()
job_id = job_result["jobId"]
print(f"Job ID: {job_id}")

# Poll for status
while True:
    time.sleep(2)
    
    response = requests.get(f"{base_url}/api/videos/status/{job_id}")
    status = response.json()
    
    print(f"Status: {status['status']}, Frames: {status['framesProcessed']}")
    
    if status["status"] in ["Completed", "Failed"]:
        if status.get("processedVideoUrl"):
            print(f"Processed video: {status['processedVideoUrl']}")
        break
```

## Working with Azure Storage Emulator

The application uses Azure Storage Emulator (Azurite) running in a container via Aspire.

**Connection String:**
```
DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=...;BlobEndpoint=https://127.0.0.1:10000/devstoreaccount1;
```

**Accessing Blobs:**
- Frames are stored in the `frames` container
- Videos are stored in the `videos` container
- Use Azure Storage Explorer or Azure CLI to browse the emulator

## Performance Tips

1. **Adjust Confidence Threshold**: Lower values detect more but may have false positives
2. **Frame Rate**: The service extracts 1 frame per second by default
3. **Batch Processing**: For multiple videos, submit jobs in parallel
4. **Monitoring**: Use Aspire Dashboard to monitor telemetry and logs

## Troubleshooting

### Issue: "Moondream service not responding"
- Verify Moondream is running on the configured URL
- Check `appsettings.json` for correct Moondream URL

### Issue: "Azure Storage connection failed"
- Ensure Aspire AppHost is running (it starts the emulator)
- Check Aspire Dashboard for storage service status

### Issue: "FFmpeg not found"
- Install FFmpeg on your system
- Ensure FFmpeg binaries are in system PATH

### Issue: "Video processing never completes"
- Check logs in Aspire Dashboard
- Verify video file is accessible at the provided URL
- Check that Moondream is responding to detection requests

## Next Steps

- Implement custom bounding box drawing
- Add support for different video formats
- Implement batch processing endpoints
- Add video quality settings
- Enhance error handling and retry logic
