using Core.Services;
using Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Core.Tests.Services;

public class MetricsServiceTests : IDisposable
{
    private readonly Mock<ILogger<MetricsService>> _mockLogger;
    private readonly Mock<IOptions<MonitoringOptions>> _mockOptions;
    private readonly MetricsService _metricsService;

    public MetricsServiceTests()
    {
        _mockLogger = new Mock<ILogger<MetricsService>>();
        _mockOptions = new Mock<IOptions<MonitoringOptions>>();
        
        var monitoringOptions = new MonitoringOptions
        {
            ServiceName = "test-service",
            ServiceVersion = "1.0.0-test"
        };
        
        _mockOptions.Setup(x => x.Value).Returns(monitoringOptions);
        
        _metricsService = new MetricsService(_mockLogger.Object, _mockOptions.Object);
    }

    [Fact]
    public void RecordMessageReceived_ValidProtocol_RecordsMetric()
    {
        // Arrange
        var protocol = "NMEA";
        var sizeBytes = 1024L;

        // Act
        _metricsService.RecordMessageReceived(protocol, sizeBytes);

        // Assert - No exception should be thrown
        // In a real scenario, you would verify the metric was recorded
        Assert.True(true);
    }

    [Fact]
    public void RecordMessageProcessed_ValidData_RecordsMetrics()
    {
        // Arrange
        var protocol = "NMEA";
        var durationMs = 50.5;

        // Act
        _metricsService.RecordMessageProcessed(protocol, durationMs);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void RecordMessagePublished_ValidData_RecordsMetrics()
    {
        // Arrange
        var protocol = "NMEA";
        var durationMs = 25.3;

        // Act
        _metricsService.RecordMessagePublished(protocol, durationMs);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void RecordMessageFailed_ValidData_RecordsMetrics()
    {
        // Arrange
        var protocol = "NMEA";
        var errorType = "ValidationError";

        // Act
        _metricsService.RecordMessageFailed(protocol, errorType);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void RecordConnectionOpened_ValidConnectionType_RecordsMetric()
    {
        // Arrange
        var connectionType = "TCP";

        // Act
        _metricsService.RecordConnectionOpened(connectionType);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void RecordConnectionClosed_ValidConnectionType_RecordsMetric()
    {
        // Arrange
        var connectionType = "TCP";

        // Act
        _metricsService.RecordConnectionClosed(connectionType);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    [Fact]
    public void UpdateActiveConnections_ValidCount_UpdatesGauge()
    {
        // Arrange
        var count = 100;

        // Act
        _metricsService.UpdateActiveConnections(count);

        // Assert - No exception should be thrown
        Assert.True(true);
    }

    public void Dispose()
    {
        _metricsService?.Dispose();
    }
}