using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Core.Services;

public class TelemetryProcessor : ITelemetryProcessor, IDisposable
{
    private readonly ILogger<TelemetryProcessor> _logger;
    private readonly IPluginManager _pluginManager;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly Channel<TelemetryMessage> _messageChannel;
    private readonly ChannelWriter<TelemetryMessage> _channelWriter;
    private readonly ChannelReader<TelemetryMessage> _channelReader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    private bool _disposed = false;

    public TelemetryProcessor(
        ILogger<TelemetryProcessor> logger,
        IPluginManager pluginManager,
        IKafkaPublisher kafkaPublisher)
    {
        _logger = logger;
        _pluginManager = pluginManager;
        _kafkaPublisher = kafkaPublisher;
        
        // Create unbounded channel for high-throughput scenarios
        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };
        
        _messageChannel = Channel.CreateUnbounded<TelemetryMessage>(channelOptions);
        _channelWriter = _messageChannel.Writer;
        _channelReader = _messageChannel.Reader;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start background processing task
        _processingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);
        
        _logger.LogInformation("Telemetry processor initialized");
    }

    public async Task<GpsData?> ProcessAsync(byte[] data, string deviceId)
    {
        if (data == null || data.Length == 0)
        {
            _logger.LogWarning("Received empty data for device {DeviceId}", deviceId);
            return null;
        }

        try
        {
            // Create telemetry message for processing
            var telemetryMessage = new TelemetryMessage
            {
                DeviceId = deviceId,
                RawData = data,
                ReceivedAt = DateTime.UtcNow
            };

            // Queue message for async processing
            await _channelWriter.WriteAsync(telemetryMessage, _cancellationTokenSource.Token);
            
            _logger.LogTrace("Queued message for processing: Device {DeviceId}, Size {Size}", 
                deviceId, data.Length);

            // For synchronous response, process immediately
            return await ProcessTelemetryMessageAsync(telemetryMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry data for device {DeviceId}", deviceId);
            return null;
        }
    }

    public Task<bool> IsHealthyAsync()
    {
        var isHealthy = !_disposed && 
                       !_cancellationTokenSource.Token.IsCancellationRequested &&
                       !_processingTask.IsCompleted;
        
        return Task.FromResult(isHealthy);
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting telemetry message processing loop");
        
        try
        {
            await foreach (var message in _channelReader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessTelemetryMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing telemetry message for device {DeviceId}", 
                        message.DeviceId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telemetry message processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in telemetry message processing loop");
        }
    }

    private async Task<GpsData?> ProcessTelemetryMessageAsync(TelemetryMessage message)
    {
        try
        {
            // Find appropriate plugin for the data
            var plugin = await _pluginManager.GetPluginForDataAsync(message.RawData);
            if (plugin == null)
            {
                _logger.LogWarning("No plugin found to handle data for device {DeviceId}", 
                    message.DeviceId);
                return null;
            }

            // Set protocol information
            message.Protocol = plugin.SupportedProtocol.ToString();
            
            _logger.LogTrace("Processing message with plugin {PluginName} for device {DeviceId}", 
                plugin.Name, message.DeviceId);

            // Process data through plugin
            var gpsData = await plugin.ProcessAsync(message.RawData, message.DeviceId);
            if (gpsData == null)
            {
                _logger.LogWarning("Plugin {PluginName} returned null data for device {DeviceId}", 
                    plugin.Name, message.DeviceId);
                return null;
            }

            // Validate processed data
            var validationResult = await plugin.ValidateAsync(gpsData);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Invalid GPS data for device {DeviceId}: {Errors}", 
                    message.DeviceId, validationResult.ErrorMessage);
                return null;
            }

            // Enrich with processing metadata
            gpsData.ExtendedData["ProcessedAt"] = DateTime.UtcNow;
            gpsData.ExtendedData["ProcessingId"] = message.Id;
            gpsData.ExtendedData["Protocol"] = message.Protocol;
            gpsData.ExtendedData["DataSize"] = message.RawData.Length;

            // Publish to Kafka
            await _kafkaPublisher.PublishAsync(gpsData);
            
            _logger.LogTrace("Successfully processed and published GPS data for device {DeviceId}", 
                message.DeviceId);

            return gpsData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry message for device {DeviceId}", 
                message.DeviceId);
            return null;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            _logger.LogInformation("Stopping telemetry processor");
            
            // Signal completion and close writer
            _channelWriter.Complete();
            
            // Cancel processing
            _cancellationTokenSource.Cancel();
            
            // Wait for processing task to complete
            await _processingTask;
            
            _logger.LogInformation("Telemetry processor stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping telemetry processor");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                StopAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during telemetry processor disposal");
            }

            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}

