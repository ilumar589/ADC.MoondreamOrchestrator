namespace MoondreamOrchestrator.ApiService;

/// <summary>
/// Bounding box coordinates returned from Moondream
/// </summary>
public readonly struct BoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public BoundingBox(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>
/// Person detection result from Moondream
/// </summary>
public readonly struct PersonDetection
{
    public string Characteristics { get; init; }
    public BoundingBox BoundingBox { get; init; }
    public double Confidence { get; init; }

    public PersonDetection(string characteristics, BoundingBox boundingBox, double confidence)
    {
        Characteristics = characteristics;
        BoundingBox = boundingBox;
        Confidence = confidence;
    }
}

/// <summary>
/// Request to process video with person detection
/// </summary>
public readonly struct VideoProcessRequest
{
    public string VideoUrl { get; init; }
    public string PersonCharacteristics { get; init; }
    public double ConfidenceThreshold { get; init; }

    public VideoProcessRequest(string videoUrl, string personCharacteristics, double confidenceThreshold = 0.5)
    {
        VideoUrl = videoUrl;
        PersonCharacteristics = personCharacteristics;
        ConfidenceThreshold = confidenceThreshold;
    }
}

/// <summary>
/// Response for video processing
/// </summary>
public readonly struct VideoProcessResponse
{
    public string JobId { get; init; }
    public string Status { get; init; }
    public string? ProcessedVideoUrl { get; init; }
    public int FramesProcessed { get; init; }
    public int DetectionsFound { get; init; }

    public VideoProcessResponse(string jobId, string status, string? processedVideoUrl, int framesProcessed, int detectionsFound)
    {
        JobId = jobId;
        Status = status;
        ProcessedVideoUrl = processedVideoUrl;
        FramesProcessed = framesProcessed;
        DetectionsFound = detectionsFound;
    }
}

/// <summary>
/// Frame upload request
/// </summary>
public readonly struct FrameUploadRequest
{
    public string FileName { get; init; }
    public byte[] Data { get; init; }
    public string ContentType { get; init; }

    public FrameUploadRequest(string fileName, byte[] data, string contentType)
    {
        FileName = fileName;
        Data = data;
        ContentType = contentType;
    }
}

/// <summary>
/// Request to process batch of pre-extracted frames with person detection
/// </summary>
public readonly struct FrameBatchProcessRequest
{
    public string[] FrameUrls { get; init; }
    public string PersonCharacteristics { get; init; }
    public double ConfidenceThreshold { get; init; }

    public FrameBatchProcessRequest(string[] frameUrls, string personCharacteristics, double confidenceThreshold = 0.5)
    {
        FrameUrls = frameUrls;
        PersonCharacteristics = personCharacteristics;
        ConfidenceThreshold = confidenceThreshold;
    }
}
