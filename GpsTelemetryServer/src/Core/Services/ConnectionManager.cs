using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCoreServer;
using System.Collections.Concurrent;
using System.Net;

namespace Core.Services;

public class ConnectionManager : IConnectionManager, IDisposable
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly ConnectionManagerOptions _options;
    private TelemetryTcpServer? _tcpServer;
    private TelemetryUdpServer? _udpServer;
    private readonly ConcurrentDictionary<Guid, DateTime> _activeConnections = new();
    private bool _disposed = false;

    public ConnectionManager(
        ILogger<ConnectionManager> logger,
        ITelemetryProcessor telemetryProcessor,
        IOptionsMonitor<ConnectionManagerOptions> options)
    {
        _logger = logger;
        _telemetryProcessor = telemetryProcessor;
        _options = options.CurrentValue;
    }

    public int ActiveConnections => _activeConnections.Count;
    public bool IsListening => (_tcpServer?.IsStarted ?? false) || (_udpServer?.IsStarted ?? false);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting connection manager on TCP:{TcpPort}, UDP:{UdpPort}", 
                _options.TcpPort, _options.UdpPort);

            // Start TCP server
            if (_options.EnableTcp)
            {
                var tcpEndpoint = new IPEndPoint(IPAddress.Any, _options.TcpPort);
                _tcpServer = new TelemetryTcpServer(_logger, _telemetryProcessor, tcpEndpoint, this);
                _tcpServer.Start();
                
                _logger.LogInformation("TCP server started on {Endpoint}", tcpEndpoint);
            }

            // Start UDP server
            if (_options.EnableUdp)
            {
                var udpEndpoint = new IPEndPoint(IPAddress.Any, _options.UdpPort);
                _udpServer = new TelemetryUdpServer(_logger, _telemetryProcessor, udpEndpoint, this);
                _udpServer.Start();
                
                _logger.LogInformation("UDP server started on {Endpoint}", udpEndpoint);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start connection manager");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Stopping connection manager");

            if (_tcpServer != null)
            {
                _tcpServer.Stop();
                _logger.LogInformation("TCP server stopped");
            }

            if (_udpServer != null)
            {
                _udpServer.Stop();
                _logger.LogInformation("UDP server stopped");
            }

            _activeConnections.Clear();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping connection manager");
            throw;
        }
    }

    public Task<bool> IsHealthyAsync()
    {
        var isHealthy = IsListening && 
                       (_tcpServer?.IsStarted ?? true) && 
                       (_udpServer?.IsStarted ?? true);
        
        return Task.FromResult(isHealthy);
    }

    public bool IsHealthy()
    {
        return IsListening && 
               (_tcpServer?.IsStarted ?? true) && 
               (_udpServer?.IsStarted ?? true);
    }

    public bool StartTcp(int port)
    {
        try
        {
            var tcpEndpoint = new IPEndPoint(IPAddress.Any, port);
            _tcpServer = new TelemetryTcpServer(_logger, _telemetryProcessor, tcpEndpoint, this);
            _tcpServer.Start();
            
            _logger.LogInformation("TCP server started on port {Port}", port);
            return _tcpServer.IsStarted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start TCP server on port {Port}", port);
            return false;
        }
    }

    public bool StartUdp(int port)
    {
        try
        {
            var udpEndpoint = new IPEndPoint(IPAddress.Any, port);
            _udpServer = new TelemetryUdpServer(_logger, _telemetryProcessor, udpEndpoint, this);
            _udpServer.Start();
            
            _logger.LogInformation("UDP server started on port {Port}", port);
            return _udpServer.IsStarted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start UDP server on port {Port}", port);
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _logger.LogInformation("Stopping connection manager");

            if (_tcpServer != null)
            {
                _tcpServer.Stop();
                _logger.LogInformation("TCP server stopped");
            }

            if (_udpServer != null)
            {
                _udpServer.Stop();
                _logger.LogInformation("UDP server stopped");
            }

            _activeConnections.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping connection manager");
        }
    }

    public ConnectionStatistics GetStatistics()
    {
        return new ConnectionStatistics
        {
            ActiveTcpConnections = _tcpServer?.IsStarted == true ? _activeConnections.Count : 0,
            ActiveUdpConnections = _udpServer?.IsStarted == true ? 1 : 0, // UDP is connectionless
            TotalMessagesReceived = GetTotalMessages(),
            MessagesPerSecond = CalculateMessagesPerSecond(),
            UptimeSeconds = GetUptimeSeconds()
        };
    }

    private long _totalMessages = 0;
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastMessageTime = DateTime.UtcNow;

    internal void IncrementMessageCount()
    {
        Interlocked.Increment(ref _totalMessages);
        _lastMessageTime = DateTime.UtcNow;
    }

    private long GetTotalMessages()
    {
        return _totalMessages;
    }

    private double CalculateMessagesPerSecond()
    {
        var elapsed = DateTime.UtcNow - _startTime;
        return elapsed.TotalSeconds > 0 ? _totalMessages / elapsed.TotalSeconds : 0;
    }

    private double GetUptimeSeconds()
    {
        return (DateTime.UtcNow - _startTime).TotalSeconds;
    }

    internal void RegisterConnection(Guid connectionId)
    {
        _activeConnections.TryAdd(connectionId, DateTime.UtcNow);
        _logger.LogDebug("Connection registered: {ConnectionId}. Total: {Count}", 
            connectionId, _activeConnections.Count);
    }

    internal void UnregisterConnection(Guid connectionId)
    {
        _activeConnections.TryRemove(connectionId, out _);
        _logger.LogDebug("Connection unregistered: {ConnectionId}. Total: {Count}", 
            connectionId, _activeConnections.Count);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection manager disposal");
            }

            _tcpServer?.Dispose();
            _udpServer?.Dispose();
            _disposed = true;
        }
    }
}

public class ConnectionManagerOptions
{
    public int TcpPort { get; set; } = 8080;
    public int UdpPort { get; set; } = 8081;
    public bool EnableTcp { get; set; } = true;
    public bool EnableUdp { get; set; } = true;
    public int MaxConnections { get; set; } = 10000;
    public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int BufferSize { get; set; } = 4096;
}