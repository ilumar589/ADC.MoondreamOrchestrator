using Xunit;
using FluentAssertions;

namespace MoondreamOrchestrator.ApiService.Tests;

public class ModelsTests
{
    [Fact]
    public void BoundingBox_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var bbox = new BoundingBox(10.5, 20.3, 100.0, 150.0);

        // Assert
        bbox.X.Should().Be(10.5);
        bbox.Y.Should().Be(20.3);
        bbox.Width.Should().Be(100.0);
        bbox.Height.Should().Be(150.0);
    }

    [Fact]
    public void PersonDetection_ShouldInitializeCorrectly()
    {
        // Arrange
        var bbox = new BoundingBox(10, 20, 100, 150);
        var characteristics = "person wearing red shirt";
        var confidence = 0.85;

        // Act
        var detection = new PersonDetection(characteristics, bbox, confidence);

        // Assert
        detection.Characteristics.Should().Be(characteristics);
        detection.BoundingBox.Should().Be(bbox);
        detection.Confidence.Should().Be(confidence);
    }

    [Fact]
    public void VideoProcessRequest_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var request = new VideoProcessRequest("http://test.com/video.mp4", "person with backpack");

        // Assert
        request.VideoUrl.Should().Be("http://test.com/video.mp4");
        request.PersonCharacteristics.Should().Be("person with backpack");
        request.ConfidenceThreshold.Should().Be(0.5); // Default value
    }

    [Fact]
    public void VideoProcessRequest_ShouldInitializeWithCustomThreshold()
    {
        // Arrange & Act
        var request = new VideoProcessRequest("http://test.com/video.mp4", "person with backpack", 0.75);

        // Assert
        request.ConfidenceThreshold.Should().Be(0.75);
    }

    [Fact]
    public void VideoProcessResponse_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var response = new VideoProcessResponse(
            "job-123", 
            "Processing", 
            "http://test.com/output.mp4", 
            100, 
            25);

        // Assert
        response.JobId.Should().Be("job-123");
        response.Status.Should().Be("Processing");
        response.ProcessedVideoUrl.Should().Be("http://test.com/output.mp4");
        response.FramesProcessed.Should().Be(100);
        response.DetectionsFound.Should().Be(25);
    }

    [Fact]
    public void FrameUploadRequest_ShouldInitializeCorrectly()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var request = new FrameUploadRequest("frame.jpg", data, "image/jpeg");

        // Assert
        request.FileName.Should().Be("frame.jpg");
        request.Data.Should().BeEquivalentTo(data);
        request.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public void BoundingBox_ShouldBeValueType()
    {
        // Arrange
        var bbox1 = new BoundingBox(10, 20, 100, 150);
        var bbox2 = new BoundingBox(10, 20, 100, 150);

        // Assert - structs are value types and compare by value
        bbox1.Should().Be(bbox2);
    }

    [Fact]
    public void PersonDetection_ShouldHandleHighConfidence()
    {
        // Arrange & Act
        var detection = new PersonDetection(
            "test", 
            new BoundingBox(0, 0, 10, 10), 
            0.99);

        // Assert
        detection.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void PersonDetection_ShouldHandleLowConfidence()
    {
        // Arrange & Act
        var detection = new PersonDetection(
            "test", 
            new BoundingBox(0, 0, 10, 10), 
            0.1);

        // Assert
        detection.Confidence.Should().BeLessThan(0.5);
    }

    [Fact]
    public void FrameBatchProcessRequest_ShouldInitializeWithDefaults()
    {
        // Arrange
        var frameUrls = new[] { "http://test.com/frame1.jpg", "http://test.com/frame2.jpg" };

        // Act
        var request = new FrameBatchProcessRequest(frameUrls, "person wearing blue hat");

        // Assert
        request.FrameUrls.Should().BeEquivalentTo(frameUrls);
        request.PersonCharacteristics.Should().Be("person wearing blue hat");
        request.ConfidenceThreshold.Should().Be(0.5); // Default value
    }

    [Fact]
    public void FrameBatchProcessRequest_ShouldInitializeWithCustomThreshold()
    {
        // Arrange
        var frameUrls = new[] { "http://test.com/frame1.jpg", "http://test.com/frame2.jpg" };

        // Act
        var request = new FrameBatchProcessRequest(frameUrls, "person wearing blue hat", 0.75);

        // Assert
        request.ConfidenceThreshold.Should().Be(0.75);
    }

    [Fact]
    public void FrameBatchProcessRequest_ShouldHandleMultipleFrames()
    {
        // Arrange
        var frameUrls = new[] 
        { 
            "http://test.com/frame1.jpg", 
            "http://test.com/frame2.jpg",
            "http://test.com/frame3.jpg",
            "http://test.com/frame4.jpg",
            "http://test.com/frame5.jpg"
        };

        // Act
        var request = new FrameBatchProcessRequest(frameUrls, "person with backpack", 0.6);

        // Assert
        request.FrameUrls.Should().HaveCount(5);
        request.FrameUrls.Should().ContainInOrder(frameUrls);
    }

    [Fact]
    public void FrameBatchProcessRequest_ShouldBeValueType()
    {
        // Arrange
        var frameUrls = new[] { "http://test.com/frame1.jpg" };
        var request1 = new FrameBatchProcessRequest(frameUrls, "test", 0.5);
        var request2 = new FrameBatchProcessRequest(frameUrls, "test", 0.5);

        // Assert - structs are value types
        request1.FrameUrls.Should().BeEquivalentTo(request2.FrameUrls);
        request1.PersonCharacteristics.Should().Be(request2.PersonCharacteristics);
        request1.ConfidenceThreshold.Should().Be(request2.ConfidenceThreshold);
    }
}
