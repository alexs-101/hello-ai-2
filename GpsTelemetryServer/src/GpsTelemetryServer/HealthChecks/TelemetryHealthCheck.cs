using Core.Interfaces;
using Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GpsTelemetryServer.HealthChecks;

public class TelemetryHealthCheck : IHealthCheck
{
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly ConnectionManager _connectionManager;
    private readonly ILogger<TelemetryHealthCheck> _logger;

    public TelemetryHealthCheck(
        ITelemetryProcessor telemetryProcessor,
        IKafkaPublisher kafkaPublisher,
        ConnectionManager connectionManager,
        ILogger<TelemetryHealthCheck> logger)
    {
        _telemetryProcessor = telemetryProcessor;
        _kafkaPublisher = kafkaPublisher;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var checks = new Dictionary<string, bool>();
            var data = new Dictionary<string, object>();

            // Check telemetry processor
            checks["TelemetryProcessor"] = await _telemetryProcessor.IsHealthyAsync();
            
            // Check Kafka publisher
            checks["KafkaPublisher"] = await _kafkaPublisher.IsHealthyAsync();
            
            // Check connection manager
            checks["ConnectionManager"] = _connectionManager.IsHealthy();
            
            // Get statistics
            var stats = _connectionManager.GetStatistics();
            data["ActiveTcpConnections"] = stats.ActiveTcpConnections;
            data["ActiveUdpConnections"] = stats.ActiveUdpConnections;
            data["TotalMessagesReceived"] = stats.TotalMessagesReceived;
            data["MessagesPerSecond"] = stats.MessagesPerSecond;

            var allHealthy = checks.Values.All(x => x);
            
            if (allHealthy)
            {
                return HealthCheckResult.Healthy("All telemetry components are healthy", data);
            }
            
            var failedChecks = checks.Where(x => !x.Value).Select(x => x.Key);
            var message = $"Failed health checks: {string.Join(", ", failedChecks)}";
            
            return HealthCheckResult.Degraded(message, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            return HealthCheckResult.Unhealthy("Health check failed with exception", ex);
        }
    }
}