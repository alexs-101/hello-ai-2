using Core.Interfaces;
using Core.Services;
using Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GpsTelemetryServer.Services;

public class TelemetryBackgroundService : BackgroundService
{
    private readonly ILogger<TelemetryBackgroundService> _logger;
    private readonly ConnectionManager _connectionManager;
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly IPluginManager _pluginManager;
    private readonly TelemetryServerOptions _options;

    public TelemetryBackgroundService(
        ILogger<TelemetryBackgroundService> logger,
        ConnectionManager connectionManager,
        ITelemetryProcessor telemetryProcessor,
        IKafkaPublisher kafkaPublisher,
        IPluginManager pluginManager,
        IOptions<TelemetryServerOptions> options)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _telemetryProcessor = telemetryProcessor;
        _kafkaPublisher = kafkaPublisher;
        _pluginManager = pluginManager;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GPS Telemetry Background Service starting...");

        try
        {
            // Load plugins first
            await LoadPluginsAsync(stoppingToken);

            // Start connection managers
            await StartConnectionManagersAsync(stoppingToken);

            // Monitor health and perform periodic tasks
            await MonitorHealthAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GPS Telemetry Background Service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in GPS Telemetry Background Service");
            throw;
        }
    }

    private async Task LoadPluginsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading plugins...");
        
        try
        {
            var plugins = await _pluginManager.LoadPluginsAsync();
            var pluginCount = plugins.Count();
            
            _logger.LogInformation("Successfully loaded {PluginCount} plugins", pluginCount);
            
            foreach (var plugin in plugins)
            {
                _logger.LogInformation("Loaded plugin: {PluginName} v{Version} for protocol {Protocol}",
                    plugin.Name, plugin.Version, plugin.SupportedProtocol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugins");
            throw;
        }
    }

    private async Task StartConnectionManagersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting connection managers...");
        
        // Start TCP server
        if (_options.TcpEnabled)
        {
            _logger.LogInformation("Starting TCP server on port {Port}", _options.TcpPort);
            if (!_connectionManager.StartTcp(_options.TcpPort))
            {
                throw new InvalidOperationException($"Failed to start TCP server on port {_options.TcpPort}");
            }
            _logger.LogInformation("TCP server started successfully on port {Port}", _options.TcpPort);
        }

        // Start UDP server
        if (_options.UdpEnabled)
        {
            _logger.LogInformation("Starting UDP server on port {Port}", _options.UdpPort);
            if (!_connectionManager.StartUdp(_options.UdpPort))
            {
                throw new InvalidOperationException($"Failed to start UDP server on port {_options.UdpPort}");
            }
            _logger.LogInformation("UDP server started successfully on port {Port}", _options.UdpPort);
        }

        await Task.Delay(1000, cancellationToken); // Allow servers to fully initialize
    }

    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting health monitoring...");
        
        var healthCheckInterval = TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds);
        
        using var timer = new PeriodicTimer(healthCheckInterval);
        
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await PerformHealthChecksAsync();
                await LogStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health monitoring cycle");
            }
        }
    }

    private async Task PerformHealthChecksAsync()
    {
        // Check telemetry processor health
        var processorHealthy = await _telemetryProcessor.IsHealthyAsync();
        if (!processorHealthy)
        {
            _logger.LogWarning("Telemetry processor health check failed");
        }

        // Check Kafka publisher health
        var kafkaHealthy = await _kafkaPublisher.IsHealthyAsync();
        if (!kafkaHealthy)
        {
            _logger.LogWarning("Kafka publisher health check failed");
        }

        // Check connection manager health
        var connectionHealthy = _connectionManager.IsHealthy();
        if (!connectionHealthy)
        {
            _logger.LogWarning("Connection manager health check failed");
        }

        var overallHealthy = processorHealthy && kafkaHealthy && connectionHealthy;
        
        if (overallHealthy)
        {
            _logger.LogTrace("All health checks passed");
        }
        else
        {
            _logger.LogWarning("One or more health checks failed");
        }
    }

    private async Task LogStatisticsAsync()
    {
        var stats = _connectionManager.GetStatistics();
        
        _logger.LogInformation("Connection Statistics - TCP: {TcpConnections} active, " +
                             "UDP: {UdpConnections} active, " +
                             "Total Messages: {TotalMessages}, " +
                             "Messages/sec: {MessagesPerSecond:F2}",
            stats.ActiveTcpConnections,
            stats.ActiveUdpConnections,
            stats.TotalMessagesReceived,
            stats.MessagesPerSecond);

        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GPS Telemetry Background Service stopping...");

        try
        {
            // Stop connection managers first
            _connectionManager.Stop();
            
            // Stop telemetry processor
            if (_telemetryProcessor is TelemetryProcessor processor)
            {
                await processor.StopAsync();
            }
            
            // Flush Kafka publisher
            await _kafkaPublisher.FlushAsync(TimeSpan.FromSeconds(30));
            
            // Shutdown plugins
            await _pluginManager.ShutdownAsync();
            
            _logger.LogInformation("GPS Telemetry Background Service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping GPS Telemetry Background Service");
        }

        await base.StopAsync(cancellationToken);
    }
}

