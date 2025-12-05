using Xunit;
using FluentAssertions;

namespace MoondreamOrchestrator.ApiService.Tests;

public class VideoProcessingOptionsTests
{
    [Fact]
    public void VideoProcessingOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = VideoProcessingOptions.Default;

        // Assert
        options.TargetFps.Should().BeNull();
        options.TargetResolution.Should().BeNull();
        options.PreserveAspectRatio.Should().BeTrue();
        options.Letterbox.Should().BeFalse();
        options.Grayscale.Should().BeFalse();
        options.Codec.Should().BeNull();
        options.Quality.Should().BeNull();
        options.EnableMotionDetection.Should().BeFalse();
        options.MotionThreshold.Should().Be(0.05);
    }

    [Fact]
    public void VideoProcessingOptions_WithCustomValues_StoresCorrectly()
    {
        // Arrange & Act
        var options = new VideoProcessingOptions
        {
            TargetFps = 30,
            TargetResolution = "1920x1080",
            PreserveAspectRatio = true,
            Letterbox = true,
            Grayscale = true,
            Codec = "libx264",
            Quality = 23,
            EnableMotionDetection = true,
            MotionThreshold = 0.1
        };

        // Assert
        options.TargetFps.Should().Be(30);
        options.TargetResolution.Should().Be("1920x1080");
        options.PreserveAspectRatio.Should().BeTrue();
        options.Letterbox.Should().BeTrue();
        options.Grayscale.Should().BeTrue();
        options.Codec.Should().Be("libx264");
        options.Quality.Should().Be(23);
        options.EnableMotionDetection.Should().BeTrue();
        options.MotionThreshold.Should().Be(0.1);
    }

    [Fact]
    public void VideoProcessRequest_WithOptions_StoresCorrectly()
    {
        // Arrange
        var options = new VideoProcessingOptions
        {
            TargetFps = 24,
            Grayscale = true
        };

        // Act
        var request = new VideoProcessRequest(
            "http://test.com/video.mp4",
            "person with hat",
            0.7,
            options);

        // Assert
        request.VideoUrl.Should().Be("http://test.com/video.mp4");
        request.PersonCharacteristics.Should().Be("person with hat");
        request.ConfidenceThreshold.Should().Be(0.7);
        request.ProcessingOptions.Should().NotBeNull();
        request.ProcessingOptions!.Value.TargetFps.Should().Be(24);
        request.ProcessingOptions!.Value.Grayscale.Should().BeTrue();
    }

    [Fact]
    public void FrameBatchProcessRequest_WithOptions_StoresCorrectly()
    {
        // Arrange
        var frameUrls = new[] { "http://test.com/frame1.jpg", "http://test.com/frame2.jpg" };
        var options = new VideoProcessingOptions
        {
            TargetResolution = "640x480",
            EnableMotionDetection = true
        };

        // Act
        var request = new FrameBatchProcessRequest(
            frameUrls,
            "person wearing glasses",
            0.8,
            options);

        // Assert
        request.FrameUrls.Should().BeEquivalentTo(frameUrls);
        request.PersonCharacteristics.Should().Be("person wearing glasses");
        request.ConfidenceThreshold.Should().Be(0.8);
        request.ProcessingOptions.Should().NotBeNull();
        request.ProcessingOptions!.Value.TargetResolution.Should().Be("640x480");
        request.ProcessingOptions!.Value.EnableMotionDetection.Should().BeTrue();
    }

    [Fact]
    public void VideoProcessRequest_WithoutOptions_UsesNull()
    {
        // Act
        var request = new VideoProcessRequest(
            "http://test.com/video.mp4",
            "person with backpack",
            0.5);

        // Assert
        request.ProcessingOptions.Should().BeNull();
    }
}
