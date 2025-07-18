using Core.Interfaces;
using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace Core.Services;

public class TelemetryUdpServer : UdpServer
{
    private readonly ILogger _logger;
    private readonly ITelemetryProcessor _telemetryProcessor;
    private readonly ConnectionManager _connectionManager;
    private readonly ArrayPool<byte> _bufferPool;

    public TelemetryUdpServer(
        ILogger logger,
        ITelemetryProcessor telemetryProcessor,
        IPEndPoint endpoint,
        ConnectionManager connectionManager) : base(endpoint)
    {
        _logger = logger;
        _telemetryProcessor = telemetryProcessor;
        _connectionManager = connectionManager;
        _bufferPool = ArrayPool<byte>.Shared;
        
        // Configure server options for high performance
        OptionReuseAddress = true;
    }

    protected override void OnStarted()
    {
        _logger.LogInformation("UDP Server started on {Endpoint}", Endpoint);
        
        // Start receiving
        ReceiveAsync();
    }

    protected override void OnStopped()
    {
        _logger.LogInformation("UDP Server stopped");
    }

    protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
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

                // Extract device ID from data
                var deviceId = ExtractDeviceId(actualData, endpoint) ?? endpoint.ToString()!;

                // Process telemetry data asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _telemetryProcessor.ProcessAsync(actualData, deviceId);
                        
                        _logger.LogTrace("Processed UDP message from {Endpoint}, device {DeviceId}, size {Size}", 
                            endpoint, deviceId, size);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing UDP telemetry data from {Endpoint}", endpoint);
                    }
                });
            }
            finally
            {
                _bufferPool.Return(data);
            }

            // Continue receiving
            ReceiveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling UDP data from {Endpoint}", endpoint);
            
            // Continue receiving even after error
            ReceiveAsync();
        }
    }

    protected override void OnSent(EndPoint endpoint, long sent)
    {
        // UDP server typically doesn't send responses, but this could be used for acknowledgments
        _logger.LogTrace("UDP data sent to {Endpoint}: {Bytes} bytes", endpoint, sent);
    }

    protected override void OnError(SocketError error)
    {
        _logger.LogWarning("UDP Server error: {Error}", error);
    }

    private string? ExtractDeviceId(byte[] data, EndPoint endpoint)
    {
        try
        {
            // Try to extract device ID from NMEA data
            var text = System.Text.Encoding.ASCII.GetString(data);
            
            // Look for device ID patterns in NMEA data
            if (text.StartsWith("$") && text.Contains(","))
            {
                var parts = text.Split(',');
                if (parts.Length > 0)
                {
                    var header = parts[0];
                    if (header.Length > 3)
                    {
                        // Extract talker ID + message type as device identifier
                        var deviceFromNmea = header.Substring(1, Math.Min(5, header.Length - 1));
                        
                        // Combine with endpoint for uniqueness in UDP (stateless)
                        return $"{deviceFromNmea}_{endpoint}".Replace(":", "_");
                    }
                }
            }

            // Fallback to endpoint-based ID
            return $"UDP_{endpoint}".Replace(":", "_");
        }
        catch
        {
            return $"UDP_{endpoint}".Replace(":", "_");
        }
    }

    public void SendResponse(EndPoint endpoint, byte[] data)
    {
        try
        {
            SendAsync(endpoint, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending UDP response to {Endpoint}", endpoint);
        }
    }
}