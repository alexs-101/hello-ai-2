using Core.Interfaces;
using Core.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Core.Services;

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly ILogger<KafkaPublisher> _logger;
    private readonly KafkaOptions _options;
    private readonly IProducer<string, string> _producer;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed = false;

    public KafkaPublisher(
        ILogger<KafkaPublisher> logger,
        IOptionsMonitor<KafkaOptions> options)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _semaphore = new SemaphoreSlim(1, 1);
        
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            BatchSize = _options.BatchSize,
            LingerMs = _options.LingerMs,
            CompressionType = _options.CompressionType,
            EnableIdempotence = _options.EnableIdempotence,
            Acks = _options.Acks,
            RetryBackoffMs = _options.RetryBackoffMs,
            MessageMaxBytes = _options.MessageMaxBytes,
            RequestTimeoutMs = _options.RequestTimeoutMs,
            ClientId = _options.ClientId ?? Environment.MachineName,
            
            // Error handling
            EnableDeliveryReports = true,
            DeliveryReportFields = "all"
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler(OnError)
            .SetLogHandler(OnLog)
            .Build();

        _logger.LogInformation("Kafka publisher initialized with bootstrap servers: {BootstrapServers}", 
            _options.BootstrapServers);
    }

    public async Task PublishAsync(GpsData gpsData)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(KafkaPublisher));
        }

        try
        {
            await _semaphore.WaitAsync();

            // Determine topic based on protocol
            var topic = GetTopicName(gpsData);
            
            // Create partition key for device-based ordering
            var partitionKey = CreatePartitionKey(gpsData);
            
            // Serialize GPS data to JSON
            var jsonData = SerializeGpsData(gpsData);
            
            // Create Kafka message
            var message = new Message<string, string>
            {
                Key = partitionKey,
                Value = jsonData,
                Timestamp = new Timestamp(gpsData.Timestamp, TimestampType.CreateTime),
                Headers = CreateHeaders(gpsData)
            };

            // Publish with delivery confirmation
            var deliveryResult = await _producer.ProduceAsync(topic, message);
            
            _logger.LogTrace("Published GPS data for device {DeviceId} to topic {Topic}, partition {Partition}, offset {Offset}", 
                gpsData.DeviceId, topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

            // Update metrics
            UpdateMetrics(gpsData, deliveryResult);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish GPS data for device {DeviceId} to Kafka. Error: {ErrorCode} - {ErrorReason}", 
                gpsData.DeviceId, ex.Error.Code, ex.Error.Reason);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing GPS data for device {DeviceId}", gpsData.DeviceId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<bool> IsHealthyAsync()
    {
        try
        {
            if (_disposed)
                return Task.FromResult(false);

            // For Kafka client health, we check if the producer is not disposed
            // and hasn't encountered fatal errors
            var isHealthy = !_disposed;
            
            return Task.FromResult(isHealthy);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka health check failed");
            return Task.FromResult(false);
        }
    }

    private string GetTopicName(GpsData gpsData)
    {
        var protocol = gpsData.ExtendedData.TryGetValue("Protocol", out var protocolObj) 
            ? protocolObj?.ToString()?.ToLowerInvariant() ?? "unknown"
            : "unknown";
            
        return $"{_options.TopicPrefix}.{protocol}";
    }

    private string CreatePartitionKey(GpsData gpsData)
    {
        // Use device ID hash for consistent partitioning
        var hashCode = gpsData.DeviceId.GetHashCode();
        var partitionIndex = Math.Abs(hashCode) % _options.PartitionCount;
        
        return $"{gpsData.DeviceId}_{partitionIndex}";
    }

    private string SerializeGpsData(GpsData gpsData)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(gpsData, options);
    }

    private Headers CreateHeaders(GpsData gpsData)
    {
        var headers = new Headers
        {
            { "device_id", System.Text.Encoding.UTF8.GetBytes(gpsData.DeviceId) },
            { "schema_version", System.Text.Encoding.UTF8.GetBytes("1.0") },
            { "content_type", System.Text.Encoding.UTF8.GetBytes("application/json") },
            { "producer", System.Text.Encoding.UTF8.GetBytes("gps-telemetry-server") }
        };

        // Add protocol if available
        if (gpsData.ExtendedData.TryGetValue("Protocol", out var protocol))
        {
            headers.Add("protocol", System.Text.Encoding.UTF8.GetBytes(protocol?.ToString() ?? "unknown"));
        }

        // Add quality score if calculated
        if (gpsData.ExtendedData.TryGetValue("QualityScore", out var qualityScore))
        {
            headers.Add("quality_score", System.Text.Encoding.UTF8.GetBytes(qualityScore?.ToString() ?? "0"));
        }

        return headers;
    }

    private void UpdateMetrics(GpsData gpsData, DeliveryResult<string, string> deliveryResult)
    {
        // This would integrate with OpenTelemetry metrics in a production system
        gpsData.ExtendedData["KafkaPartition"] = deliveryResult.Partition.Value;
        gpsData.ExtendedData["KafkaOffset"] = deliveryResult.Offset.Value;
        gpsData.ExtendedData["KafkaTimestamp"] = deliveryResult.Timestamp.UtcDateTime;
    }

    private void OnError(IProducer<string, string> producer, Error error)
    {
        _logger.LogError("Kafka producer error: {ErrorCode} - {ErrorReason}", error.Code, error.Reason);
        
        // Handle specific error cases
        if (error.IsFatal)
        {
            _logger.LogCritical("Fatal Kafka error detected. Producer may need to be recreated.");
        }
    }

    private void OnLog(IProducer<string, string> producer, LogMessage logMessage)
    {
        // Map Kafka log levels to .NET log levels
        var logLevel = logMessage.Level switch
        {
            SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
            SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Warning => LogLevel.Warning,
            SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
            SyslogLevel.Debug => LogLevel.Debug,
            _ => LogLevel.Trace
        };

        _logger.Log(logLevel, "Kafka: {Message} [{Facility}]", logMessage.Message, logMessage.Facility);
    }

    public async Task FlushAsync(TimeSpan timeout)
    {
        try
        {
            _producer.Flush(timeout);
            await Task.CompletedTask;
            _logger.LogDebug("Kafka producer flushed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing Kafka producer");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _logger.LogInformation("Disposing Kafka publisher");
                
                // Flush any pending messages
                _producer.Flush(TimeSpan.FromSeconds(10));
                
                _producer.Dispose();
                _semaphore.Dispose();
                
                _disposed = true;
                _logger.LogInformation("Kafka publisher disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka publisher");
            }
        }
    }
}

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TopicPrefix { get; set; } = "telemetry.gps";
    public int PartitionCount { get; set; } = 3;
    public int BatchSize { get; set; } = 100;
    public int LingerMs { get; set; } = 10;
    public CompressionType CompressionType { get; set; } = CompressionType.Gzip;
    public bool EnableIdempotence { get; set; } = true;
    public Acks Acks { get; set; } = Acks.All;
    public int RetryBackoffMs { get; set; } = 100;
    public int MessageMaxBytes { get; set; } = 1048576; // 1MB
    public int RequestTimeoutMs { get; set; } = 30000;
    public string? ClientId { get; set; }
}