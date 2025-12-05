using FFMpegCore;
using FFMpegCore.Enums;
using SkiaSharp;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// Helper service for video frame processing operations
/// </summary>
public class VideoFrameProcessor
{
    private readonly ILogger<VideoFrameProcessor> _logger;

    public VideoFrameProcessor(ILogger<VideoFrameProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies video processing options to a frame
    /// </summary>
    public byte[] ProcessFrame(byte[] frameData, VideoProcessingOptions options)
    {
        try
        {
            using var inputStream = new MemoryStream(frameData);
            using var originalBitmap = SKBitmap.Decode(inputStream);
            
            if (originalBitmap == null)
            {
                _logger.LogWarning("Failed to decode frame for processing");
                return frameData;
            }

            var processedBitmap = originalBitmap;
            var shouldDispose = false;

            // Apply resize if specified
            if (!string.IsNullOrEmpty(options.TargetResolution))
            {
                processedBitmap = ResizeFrame(processedBitmap, options.TargetResolution, options.PreserveAspectRatio, options.Letterbox);
                shouldDispose = true;
            }

            // Apply grayscale if specified
            if (options.Grayscale)
            {
                var grayscaleBitmap = ConvertToGrayscale(processedBitmap);
                if (shouldDispose) processedBitmap.Dispose();
                processedBitmap = grayscaleBitmap;
                shouldDispose = true;
            }

            // Encode to JPEG
            using var surface = SKSurface.Create(new SKImageInfo(processedBitmap.Width, processedBitmap.Height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(processedBitmap, 0, 0);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            
            if (shouldDispose) processedBitmap.Dispose();
            
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame");
            return frameData;
        }
    }

    /// <summary>
    /// Detects if two frames are similar (for motion detection)
    /// </summary>
    public bool AreFramesSimilar(byte[] frame1, byte[] frame2, double threshold)
    {
        try
        {
            using var stream1 = new MemoryStream(frame1);
            using var stream2 = new MemoryStream(frame2);
            using var bitmap1 = SKBitmap.Decode(stream1);
            using var bitmap2 = SKBitmap.Decode(stream2);

            if (bitmap1 == null || bitmap2 == null)
            {
                return false;
            }

            // Resize to small size for quick comparison
            var compareWidth = 32;
            var compareHeight = 32;
            
            using var resized1 = bitmap1.Resize(new SKImageInfo(compareWidth, compareHeight), SKFilterQuality.Low);
            using var resized2 = bitmap2.Resize(new SKImageInfo(compareWidth, compareHeight), SKFilterQuality.Low);

            if (resized1 == null || resized2 == null)
            {
                return false;
            }

            // Calculate difference
            var totalPixels = compareWidth * compareHeight;
            var differentPixels = 0;

            for (var y = 0; y < compareHeight; y++)
            {
                for (var x = 0; x < compareWidth; x++)
                {
                    var pixel1 = resized1.GetPixel(x, y);
                    var pixel2 = resized2.GetPixel(x, y);

                    var diff = Math.Abs(pixel1.Red - pixel2.Red) + 
                               Math.Abs(pixel1.Green - pixel2.Green) + 
                               Math.Abs(pixel1.Blue - pixel2.Blue);
                    
                    if (diff > 30) // Threshold for pixel difference
                    {
                        differentPixels++;
                    }
                }
            }

            var differenceRatio = (double)differentPixels / totalPixels;
            return differenceRatio < threshold;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing frames");
            return false;
        }
    }

    private SKBitmap ResizeFrame(SKBitmap bitmap, string targetResolution, bool preserveAspectRatio, bool letterbox)
    {
        var parts = targetResolution.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var targetWidth) || !int.TryParse(parts[1], out var targetHeight))
        {
            _logger.LogWarning("Invalid target resolution format: {Resolution}", targetResolution);
            return bitmap;
        }

        if (!preserveAspectRatio)
        {
            // Simple resize without preserving aspect ratio
            var resized = bitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
            return resized ?? bitmap;
        }

        // Calculate scaling to preserve aspect ratio
        var scaleX = (double)targetWidth / bitmap.Width;
        var scaleY = (double)targetHeight / bitmap.Height;
        var scale = Math.Min(scaleX, scaleY);

        var scaledWidth = (int)(bitmap.Width * scale);
        var scaledHeight = (int)(bitmap.Height * scale);

        if (!letterbox)
        {
            // Resize to fit within target, no letterboxing
            var resized = bitmap.Resize(new SKImageInfo(scaledWidth, scaledHeight), SKFilterQuality.High);
            return resized ?? bitmap;
        }

        // Resize with letterboxing (add black bars)
        using var scaledBitmap = bitmap.Resize(new SKImageInfo(scaledWidth, scaledHeight), SKFilterQuality.High);
        if (scaledBitmap == null)
        {
            return bitmap;
        }

        using var surface = SKSurface.Create(new SKImageInfo(targetWidth, targetHeight));
        var canvas = surface.Canvas;
        
        // Fill with black
        canvas.Clear(SKColors.Black);
        
        // Center the scaled image
        var x = (targetWidth - scaledWidth) / 2;
        var y = (targetHeight - scaledHeight) / 2;
        canvas.DrawBitmap(scaledBitmap, x, y);

        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private SKBitmap ConvertToGrayscale(SKBitmap bitmap)
    {
        var grayscaleBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
        
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var gray = (byte)((pixel.Red * 0.299) + (pixel.Green * 0.587) + (pixel.Blue * 0.114));
                grayscaleBitmap.SetPixel(x, y, new SKColor(gray, gray, gray, pixel.Alpha));
            }
        }

        return grayscaleBitmap;
    }

    /// <summary>
    /// Gets FFMpeg arguments for video creation based on options
    /// </summary>
    public string GetCodecName(VideoProcessingOptions options)
    {
        return options.Codec ?? "libx264";
    }

    /// <summary>
    /// Gets quality parameter for FFMpeg
    /// </summary>
    public int GetQuality(VideoProcessingOptions options)
    {
        // CRF value: 0-51, lower is better. Default 23 for libx264
        return options.Quality ?? 23;
    }
}
