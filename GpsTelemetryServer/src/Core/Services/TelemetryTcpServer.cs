using Core.Interfaces;
using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace Core.Services;

public class TelemetryTcpServer : TcpServer
{
    private readonly ILogger _logger;
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly ConnectionManager _connectionManager;

    public TelemetryTcpServer(
        ILogger logger,
        ITelemetryProcessor telemetryProcessor,
        IPEndPoint endpoint,
        ConnectionManager connectionManager) : base(endpoint)
    {
        _logger = logger;
        _telemetryProcessor = telemetryProcessor;
        _connectionManager = connectionManager;
        
        // Configure server options for high performance
        OptionReuseAddress = true;
        OptionNoDelay = true;
        OptionKeepAlive = true;
    }

    protected override TcpSession CreateSession()
    {
        return new TelemetryTcpSession(this, _logger, _telemetryProcessor, _connectionManager);
    }

    protected override void OnStarted()
    {
        _logger.LogInformation("TCP Server started on {Endpoint}", Endpoint);
    }

    protected override void OnStopped()
    {
        _logger.LogInformation("TCP Server stopped");
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogError("TCP Server error: {Error}", error);
    }
}

public class TelemetryTcpSession : TcpSession
{
    private readonly ILogger _logger;
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly ConnectionManager _connectionManager;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly Guid _sessionId;
    private string? _deviceId;

    public TelemetryTcpSession(
        TcpServer server,
        ILogger logger,
        ITelemetryProcessor telemetryProcessor,
        ConnectionManager connectionManager) : base(server)
    {
        _logger = logger;
        _telemetryProcessor = telemetryProcessor;
        _connectionManager = connectionManager;
        _bufferPool = ArrayPool<byte>.Shared;
        _sessionId = Guid.NewGuid();
    }

    protected override void OnConnected()
    {
        _connectionManager.RegisterConnection(_sessionId);
        _logger.LogDebug("TCP client connected: {SessionId} from {Endpoint}", 
            _sessionId, Socket.RemoteEndPoint);
    }

    protected override void OnDisconnected()
    {
        _connectionManager.UnregisterConnection(_sessionId);
        _logger.LogDebug("TCP client disconnected: {SessionId}", _sessionId);
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            // Use ArrayPool for zero-allocation buffer management
            var data = _bufferPool.Rent((int)size);
            try
            {
                Array.Copy(buffer, offset, data, 0, size);
                var actualData = new byte[size];
                Array.Copy(data, 0, actualData, 0, size);

                // Extract device ID from first message if not set
                if (string.IsNullOrEmpty(_deviceId))
                {
                    _deviceId = ExtractDeviceId(actualData) ?? _sessionId.ToString();
                    _logger.LogDebug("Device ID identified: {DeviceId} for session {SessionId}", 
                        _deviceId, _sessionId);
                }

                // Process telemetry data asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _telemetryProcessor.ProcessAsync(actualData, _deviceId!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing telemetry data for device {DeviceId}", _deviceId);
                    }
                });
            }
            finally
            {
                _bufferPool.Return(data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received data for session {SessionId}", _sessionId);
        }
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogWarning("TCP session error for {SessionId}: {Error}", _sessionId, error);
    }

    private string? ExtractDeviceId(byte[] data)
    {
        try
        {
            // Try to extract device ID from NMEA data
            var text = System.Text.Encoding.ASCII.GetString(data);
            
            // Look for device ID patterns in NMEA data
            // This is a simple implementation - could be enhanced based on actual device protocols
            if (text.StartsWith("$") && text.Contains(","))
            {
                // For now, use the first 8 characters after $ as a simple device identifier
                var parts = text.Split(',');
                if (parts.Length > 0)
                {
                    var header = parts[0];
                    if (header.Length > 3)
                    {
                        // Extract talker ID (first 2 chars after $) + message type (next 3 chars)
                        return header.Substring(1, Math.Min(5, header.Length - 1));
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}