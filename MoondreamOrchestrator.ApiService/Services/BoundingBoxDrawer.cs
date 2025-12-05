using SkiaSharp;

namespace MoondreamOrchestrator.ApiService.Services;

/// <summary>
/// Options for drawing bounding boxes
/// </summary>
public record BoundingBoxDrawingOptions
{
    public SKColor BoxColor { get; init; } = SKColors.Red;
    public float BoxThickness { get; init; } = 3f;
    public float CornerRadius { get; init; } = 0f;
    public bool DrawLabel { get; init; } = true;
    public SKColor LabelBackgroundColor { get; init; } = new SKColor(0, 0, 0, 180);
    public SKColor LabelTextColor { get; init; } = SKColors.White;
    public float LabelFontSize { get; init; } = 16f;
    public float LabelPadding { get; init; } = 4f;
}

/// <summary>
/// Service for drawing bounding boxes and labels on images
/// </summary>
public class BoundingBoxDrawer
{
    private readonly ILogger<BoundingBoxDrawer> _logger;

    public BoundingBoxDrawer(ILogger<BoundingBoxDrawer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Draws bounding boxes on an image
    /// </summary>
    /// <param name="imageData">Input image data</param>
    /// <param name="detections">Detections with bounding boxes</param>
    /// <param name="options">Drawing options</param>
    /// <returns>Image data with bounding boxes drawn</returns>
    public byte[] DrawBoundingBoxes(byte[] imageData, PersonDetection[] detections, BoundingBoxDrawingOptions? options = null)
    {
        if (detections.Length == 0)
        {
            return imageData;
        }

        options ??= new BoundingBoxDrawingOptions();

        try
        {
            using var inputStream = new MemoryStream(imageData);
            using var originalBitmap = SKBitmap.Decode(inputStream);
            
            if (originalBitmap == null)
            {
                _logger.LogError("Failed to decode image");
                return imageData;
            }

            using var surface = SKSurface.Create(new SKImageInfo(originalBitmap.Width, originalBitmap.Height));
            var canvas = surface.Canvas;

            // Draw the original image
            canvas.DrawBitmap(originalBitmap, 0, 0);

            // Draw each bounding box
            foreach (var detection in detections)
            {
                DrawSingleBoundingBox(canvas, detection, originalBitmap.Width, originalBitmap.Height, options);
            }

            // Encode to JPEG
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing bounding boxes");
            return imageData;
        }
    }

    /// <summary>
    /// Draws bounding boxes on an image with per-class colors
    /// </summary>
    public byte[] DrawBoundingBoxesWithColors(byte[] imageData, PersonDetection[] detections, Dictionary<string, SKColor>? classColors = null)
    {
        if (detections.Length == 0)
        {
            return imageData;
        }

        classColors ??= new Dictionary<string, SKColor>();

        try
        {
            using var inputStream = new MemoryStream(imageData);
            using var originalBitmap = SKBitmap.Decode(inputStream);
            
            if (originalBitmap == null)
            {
                _logger.LogError("Failed to decode image");
                return imageData;
            }

            using var surface = SKSurface.Create(new SKImageInfo(originalBitmap.Width, originalBitmap.Height));
            var canvas = surface.Canvas;

            // Draw the original image
            canvas.DrawBitmap(originalBitmap, 0, 0);

            // Draw each bounding box with its color
            foreach (var detection in detections)
            {
                var color = GetColorForClass(detection.Characteristics, classColors);
                var options = new BoundingBoxDrawingOptions { BoxColor = color, LabelBackgroundColor = new SKColor(color.Red, color.Green, color.Blue, 180) };
                DrawSingleBoundingBox(canvas, detection, originalBitmap.Width, originalBitmap.Height, options);
            }

            // Encode to JPEG
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing bounding boxes with colors");
            return imageData;
        }
    }

    private void DrawSingleBoundingBox(SKCanvas canvas, PersonDetection detection, int imageWidth, int imageHeight, BoundingBoxDrawingOptions options)
    {
        // Convert normalized coordinates to pixel coordinates
        var x = (float)(detection.BoundingBox.X * imageWidth);
        var y = (float)(detection.BoundingBox.Y * imageHeight);
        var width = (float)(detection.BoundingBox.Width * imageWidth);
        var height = (float)(detection.BoundingBox.Height * imageHeight);

        // Create paint for the box
        using var boxPaint = new SKPaint
        {
            Color = options.BoxColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = options.BoxThickness,
            IsAntialias = true
        };

        // Draw the bounding box
        if (options.CornerRadius > 0)
        {
            var rect = new SKRect(x, y, x + width, y + height);
            canvas.DrawRoundRect(rect, options.CornerRadius, options.CornerRadius, boxPaint);
        }
        else
        {
            canvas.DrawRect(x, y, width, height, boxPaint);
        }

        // Draw label if enabled
        if (options.DrawLabel)
        {
            DrawLabel(canvas, detection, x, y, options);
        }
    }

    private void DrawLabel(SKCanvas canvas, PersonDetection detection, float x, float y, BoundingBoxDrawingOptions options)
    {
        // Create label text (e.g., "person 0.87")
        var labelText = $"{GetShortCharacteristics(detection.Characteristics)} {detection.Confidence:F2}";

        using var textPaint = new SKPaint
        {
            Color = options.LabelTextColor,
            TextSize = options.LabelFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        // Measure text
        var textBounds = new SKRect();
        textPaint.MeasureText(labelText, ref textBounds);

        // Calculate label background rectangle
        var labelX = x;
        var labelY = y - textBounds.Height - options.LabelPadding * 2;
        if (labelY < 0) labelY = y; // If no space above, draw below

        var labelWidth = textBounds.Width + options.LabelPadding * 2;
        var labelHeight = textBounds.Height + options.LabelPadding * 2;

        // Draw label background
        using var bgPaint = new SKPaint
        {
            Color = options.LabelBackgroundColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRect(labelX, labelY, labelWidth, labelHeight, bgPaint);

        // Draw label text
        canvas.DrawText(labelText, labelX + options.LabelPadding, labelY + labelHeight - options.LabelPadding, textPaint);
    }

    private static string GetShortCharacteristics(string characteristics)
    {
        // Extract a short version of characteristics for label (e.g., first word or "person")
        var words = characteristics.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words[0].Equals("person", StringComparison.OrdinalIgnoreCase) 
            ? "person" 
            : (words.Length > 2 ? string.Join(" ", words.Take(2)) : characteristics);
    }

    private static SKColor GetColorForClass(string className, Dictionary<string, SKColor> classColors)
    {
        if (classColors.TryGetValue(className, out var color))
        {
            return color;
        }

        // Generate a deterministic color based on class name hash
        var hash = className.GetHashCode();
        var r = (byte)((hash & 0xFF0000) >> 16);
        var g = (byte)((hash & 0x00FF00) >> 8);
        var b = (byte)(hash & 0x0000FF);
        
        // Ensure colors are bright enough
        r = (byte)Math.Max((int)r, 100);
        g = (byte)Math.Max((int)g, 100);
        b = (byte)Math.Max((int)b, 100);

        return new SKColor(r, g, b);
    }
}
