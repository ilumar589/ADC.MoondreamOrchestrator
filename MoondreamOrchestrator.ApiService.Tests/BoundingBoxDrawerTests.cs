using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using MoondreamOrchestrator.ApiService.Services;

namespace MoondreamOrchestrator.ApiService.Tests;

public class BoundingBoxDrawerTests
{
    private readonly BoundingBoxDrawer _drawer;
    private readonly Mock<ILogger<BoundingBoxDrawer>> _loggerMock;

    public BoundingBoxDrawerTests()
    {
        _loggerMock = new Mock<ILogger<BoundingBoxDrawer>>();
        _drawer = new BoundingBoxDrawer(_loggerMock.Object);
    }

    private byte[] CreateTestImage(int width, int height, SKColor backgroundColor)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(backgroundColor);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    [Fact]
    public void DrawBoundingBoxes_WithNoDetections_ReturnsOriginalImage()
    {
        // Arrange
        var imageData = CreateTestImage(100, 100, SKColors.White);
        var detections = Array.Empty<PersonDetection>();

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections);

        // Assert
        result.Should().BeSameAs(imageData);
    }

    [Fact]
    public void DrawBoundingBoxes_WithSingleDetection_ModifiesImage()
    {
        // Arrange
        var imageData = CreateTestImage(200, 200, SKColors.White);
        var detections = new[]
        {
            new PersonDetection(
                "person with red shirt",
                new BoundingBox(0.25, 0.25, 0.5, 0.5), // 25% offset, 50% size
                0.87
            )
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeSameAs(imageData);
        result.Length.Should().BeGreaterThan(0);

        // Verify the image can be decoded
        using var stream = new MemoryStream(result);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(200);
        bitmap.Height.Should().Be(200);
    }

    [Fact]
    public void DrawBoundingBoxes_WithMultipleDetections_DrawsAllBoxes()
    {
        // Arrange
        var imageData = CreateTestImage(300, 300, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("person 1", new BoundingBox(0.1, 0.1, 0.3, 0.3), 0.90),
            new PersonDetection("person 2", new BoundingBox(0.6, 0.6, 0.3, 0.3), 0.85)
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections);

        // Assert
        result.Should().NotBeNull();
        using var stream = new MemoryStream(result);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
    }

    [Fact]
    public void DrawBoundingBoxes_WithCustomOptions_UsesOptions()
    {
        // Arrange
        var imageData = CreateTestImage(200, 200, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("person", new BoundingBox(0.2, 0.2, 0.6, 0.6), 0.75)
        };
        var options = new BoundingBoxDrawingOptions
        {
            BoxColor = SKColors.Blue,
            BoxThickness = 5f,
            CornerRadius = 10f,
            DrawLabel = true,
            LabelFontSize = 20f
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections, options);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeSameAs(imageData);
    }

    [Fact]
    public void DrawBoundingBoxes_WithLabelDisabled_SkipsLabel()
    {
        // Arrange
        var imageData = CreateTestImage(200, 200, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("person", new BoundingBox(0.2, 0.2, 0.6, 0.6), 0.75)
        };
        var options = new BoundingBoxDrawingOptions { DrawLabel = false };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections, options);

        // Assert
        result.Should().NotBeNull();
        using var stream = new MemoryStream(result);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
    }

    [Fact]
    public void DrawBoundingBoxesWithColors_UsesPerClassColors()
    {
        // Arrange
        var imageData = CreateTestImage(300, 300, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("person with red shirt", new BoundingBox(0.1, 0.1, 0.3, 0.3), 0.90),
            new PersonDetection("person with blue shirt", new BoundingBox(0.6, 0.6, 0.3, 0.3), 0.85)
        };
        var classColors = new Dictionary<string, SKColor>
        {
            ["person with red shirt"] = SKColors.Red,
            ["person with blue shirt"] = SKColors.Blue
        };

        // Act
        var result = _drawer.DrawBoundingBoxesWithColors(imageData, detections, classColors);

        // Assert
        result.Should().NotBeNull();
        using var stream = new MemoryStream(result);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
    }

    [Fact]
    public void DrawBoundingBoxesWithColors_GeneratesDeterministicColors()
    {
        // Arrange
        var imageData = CreateTestImage(200, 200, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("unknown class", new BoundingBox(0.2, 0.2, 0.6, 0.6), 0.80)
        };

        // Act
        var result1 = _drawer.DrawBoundingBoxesWithColors(imageData, detections);
        var result2 = _drawer.DrawBoundingBoxesWithColors(imageData, detections);

        // Assert - Both results should be identical (deterministic)
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Length.Should().Be(result2.Length);
    }

    [Fact]
    public void DrawBoundingBoxes_WithInvalidImage_ReturnsOriginalData()
    {
        // Arrange
        var invalidImageData = new byte[] { 0x00, 0x01, 0x02 };
        var detections = new[]
        {
            new PersonDetection("person", new BoundingBox(0.2, 0.2, 0.6, 0.6), 0.75)
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(invalidImageData, detections);

        // Assert
        result.Should().BeSameAs(invalidImageData);
    }

    [Fact]
    public void DrawBoundingBoxes_WithEdgeCoordinates_HandlesCorrectly()
    {
        // Arrange
        var imageData = CreateTestImage(100, 100, SKColors.White);
        var detections = new[]
        {
            // Box at top-left corner
            new PersonDetection("person 1", new BoundingBox(0, 0, 0.1, 0.1), 0.90),
            // Box at bottom-right corner
            new PersonDetection("person 2", new BoundingBox(0.9, 0.9, 0.1, 0.1), 0.85)
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections);

        // Assert
        result.Should().NotBeNull();
        using var stream = new MemoryStream(result);
        using var bitmap = SKBitmap.Decode(stream);
        bitmap.Should().NotBeNull();
    }

    [Fact]
    public void DrawBoundingBoxes_VerifyPixelChanges_AtExpectedLocations()
    {
        // Arrange
        var imageData = CreateTestImage(100, 100, SKColors.White);
        var detections = new[]
        {
            new PersonDetection("person", new BoundingBox(0.25, 0.25, 0.5, 0.5), 0.85)
        };

        // Act
        var result = _drawer.DrawBoundingBoxes(imageData, detections);

        // Assert - Decode both images and verify pixels changed
        using var originalStream = new MemoryStream(imageData);
        using var originalBitmap = SKBitmap.Decode(originalStream);
        using var resultStream = new MemoryStream(result);
        using var resultBitmap = SKBitmap.Decode(resultStream);

        // At the bounding box edge (x=25, y=25), pixels should have changed
        var originalPixel = originalBitmap!.GetPixel(25, 25);
        var resultPixel = resultBitmap!.GetPixel(25, 25);

        // The pixels should be different (box was drawn)
        resultPixel.Should().NotBe(originalPixel);
    }
}
