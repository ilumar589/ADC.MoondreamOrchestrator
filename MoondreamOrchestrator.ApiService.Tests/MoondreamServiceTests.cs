using Xunit;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using MoondreamOrchestrator.ApiService.Services;

namespace MoondreamOrchestrator.ApiService.Tests;

public class MoondreamServiceTests
{
    private readonly Mock<ILogger<MoondreamService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public MoondreamServiceTests()
    {
        _mockLogger = new Mock<ILogger<MoondreamService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["Moondream:Url"]).Returns("http://localhost:5000");
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldReturnDetections_WhenMoondreamRespondsSuccessfully()
    {
        // Arrange
        var mockResponse = new
        {
            Detections = new[]
            {
                new
                {
                    Box = new { X = 100.0, Y = 150.0, Width = 200.0, Height = 300.0 },
                    Confidence = 0.85
                }
            }
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        var imageData = new byte[] { 1, 2, 3, 4, 5 };
        var characteristics = "person wearing red shirt";

        // Act
        var result = await service.DetectPersonAsync(imageData, characteristics);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Characteristics.Should().Be(characteristics);
        result[0].BoundingBox.X.Should().Be(100.0);
        result[0].BoundingBox.Y.Should().Be(150.0);
        result[0].Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldReturnEmpty_WhenMoondreamReturnsNoDetections()
    {
        // Arrange
        var mockResponse = new { Detections = Array.Empty<object>() };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        var imageData = new byte[] { 1, 2, 3 };
        var characteristics = "person wearing blue hat";

        // Act
        var result = await service.DetectPersonAsync(imageData, characteristics);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldReturnEmpty_WhenMoondreamRequestFails()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        var imageData = new byte[] { 1, 2, 3 };
        var characteristics = "person";

        // Act
        var result = await service.DetectPersonAsync(imageData, characteristics);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldHandleMultipleDetections()
    {
        // Arrange
        var mockResponse = new
        {
            Detections = new[]
            {
                new { Box = new { X = 100.0, Y = 150.0, Width = 200.0, Height = 300.0 }, Confidence = 0.85 },
                new { Box = new { X = 400.0, Y = 450.0, Width = 250.0, Height = 350.0 }, Confidence = 0.92 }
            }
        };

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        var imageData = new byte[] { 1, 2, 3 };
        var characteristics = "multiple people";

        // Act
        var result = await service.DetectPersonAsync(imageData, characteristics);

        // Assert
        result.Should().HaveCount(2);
        result[0].Confidence.Should().Be(0.85);
        result[1].Confidence.Should().Be(0.92);
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldUseConfiguredUrl()
    {
        // Arrange
        var customUrl = "http://custom-moondream:8080";
        _mockConfiguration.Setup(x => x["Moondream:Url"]).Returns(customUrl);

        var mockResponse = new { Detections = Array.Empty<object>() };
        
        HttpRequestMessage? capturedRequest = null;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        await service.DetectPersonAsync(new byte[] { 1, 2, 3 }, "test");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri.Should().NotBeNull();
        capturedRequest.RequestUri!.ToString().Should().StartWith(customUrl);
    }

    [Fact]
    public async Task DetectPersonAsync_ShouldHandleNullDetections()
    {
        // Arrange
        var mockResponse = "{}"; // No Detections property

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponse)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var service = new MoondreamService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        var result = await service.DetectPersonAsync(new byte[] { 1, 2, 3 }, "test");

        // Assert
        result.Should().BeEmpty();
    }
}
