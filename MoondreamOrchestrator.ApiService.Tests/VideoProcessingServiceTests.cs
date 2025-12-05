using Xunit;
using FluentAssertions;

namespace MoondreamOrchestrator.ApiService.Tests;

public class VideoProcessingServiceTests
{
    [Fact]
    public void VideoProcessResponse_ShouldBeImmutable()
    {
        // Arrange & Act
        var response = new VideoProcessResponse("job-123", "Processing", null, 10, 5);

        // Assert
        response.JobId.Should().Be("job-123");
        response.Status.Should().Be("Processing");
        response.ProcessedVideoUrl.Should().BeNull();
        response.FramesProcessed.Should().Be(10);
        response.DetectionsFound.Should().Be(5);
    }

    [Fact]
    public void VideoProcessRequest_ShouldStoreValues()
    {
        // Arrange & Act
        var request = new VideoProcessRequest("http://test.com/video.mp4", "person wearing red", 0.75);

        // Assert
        request.VideoUrl.Should().Be("http://test.com/video.mp4");
        request.PersonCharacteristics.Should().Be("person wearing red");
        request.ConfidenceThreshold.Should().Be(0.75);
    }

    [Fact]
    public void FrameUploadRequest_ShouldHandleLargeData()
    {
        // Arrange
        var largeData = new byte[10000];
        Array.Fill(largeData, (byte)255);

        // Act
        var request = new FrameUploadRequest("large.jpg", largeData, "image/jpeg");

        // Assert
        request.Data.Length.Should().Be(10000);
        request.Data.All(b => b == 255).Should().BeTrue();
    }
}
