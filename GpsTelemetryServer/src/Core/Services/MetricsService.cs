using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Core.Configuration;
using Core.Interfaces;

namespace Core.Services;

public class MetricsService : IMetricsService, IDisposable
{
    private readonly ILogger<MetricsService> _logger;
    private readonly MonitoringOptions _options;
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _messagesReceivedCounter;
    private readonly Counter<long> _messagesProcessedCounter;
    private readonly Counter<long> _messagesPublishedCounter;
    private readonly Counter<long> _messagesFailedCounter;
    private readonly Counter<long> _connectionsOpenedCounter;
    private readonly Counter<long> _connectionsClosedCounter;
    
    // Gauges
    private readonly ObservableGauge<int> _activeConnectionsGauge;
    private readonly ObservableGauge<double> _processingLatencyGauge;
    private readonly ObservableGauge<double> _kafkaPublishLatencyGauge;
    
    // Histograms
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Histogram<double> _kafkaPublishDuration;
    private readonly Histogram<long> _messageSizeHistogram;
    
    private int _activeConnections = 0;
    private double _lastProcessingLatency = 0;
    private double _lastKafkaLatency = 0;
    
    public MetricsService(ILogger<MetricsService> logger, IOptions<MonitoringOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        _meter = new Meter(_options.ServiceName, _options.ServiceVersion);
        
        // Initialize counters
        _messagesReceivedCounter = _meter.CreateCounter<long>(
            "telemetry_messages_received_total",
            description: "Total number of telemetry messages received");
            
        _messagesProcessedCounter = _meter.CreateCounter<long>(
            "telemetry_messages_processed_total", 
            description: "Total number of telemetry messages successfully processed");
            
        _messagesPublishedCounter = _meter.CreateCounter<long>(
            "telemetry_messages_published_total",
            description: "Total number of telemetry messages published to Kafka");
            
        _messagesFailedCounter = _meter.CreateCounter<long>(
            "telemetry_messages_failed_total",
            description: "Total number of failed telemetry message processing attempts");
            
        _connectionsOpenedCounter = _meter.CreateCounter<long>(
            "telemetry_connections_opened_total",
            description: "Total number of connections opened");
            
        _connectionsClosedCounter = _meter.CreateCounter<long>(
            "telemetry_connections_closed_total", 
            description: "Total number of connections closed");
        
        // Initialize gauges
        _activeConnectionsGauge = _meter.CreateObservableGauge<int>(
            "telemetry_active_connections",
            observeValue: () => _activeConnections,
            description: "Current number of active connections");
            
        _processingLatencyGauge = _meter.CreateObservableGauge<double>(
            "telemetry_processing_latency_ms",
            observeValue: () => _lastProcessingLatency,
            description: "Current message processing latency in milliseconds");
            
        _kafkaPublishLatencyGauge = _meter.CreateObservableGauge<double>(
            "telemetry_kafka_publish_latency_ms", 
            observeValue: () => _lastKafkaLatency,
            description: "Current Kafka publish latency in milliseconds");
        
        // Initialize histograms
        _messageProcessingDuration = _meter.CreateHistogram<double>(
            "telemetry_message_processing_duration_ms",
            description: "Duration of message processing in milliseconds");
            
        _kafkaPublishDuration = _meter.CreateHistogram<double>(
            "telemetry_kafka_publish_duration_ms",
            description: "Duration of Kafka publish operations in milliseconds");
            
        _messageSizeHistogram = _meter.CreateHistogram<long>(
            "telemetry_message_size_bytes",
            description: "Size of telemetry messages in bytes");
        
        _logger.LogInformation("Metrics service initialized for {ServiceName} v{ServiceVersion}", 
            _options.ServiceName, _options.ServiceVersion);
    }
    
    public void RecordMessageReceived(string protocol, long sizeBytes)
    {
        _messagesReceivedCounter.Add(1, new KeyValuePair<string, object?>("protocol", protocol));
        _messageSizeHistogram.Record(sizeBytes, new KeyValuePair<string, object?>("protocol", protocol));
    }
    
    public void RecordMessageProcessed(string protocol, double durationMs)
    {
        _messagesProcessedCounter.Add(1, new KeyValuePair<string, object?>("protocol", protocol));
        _messageProcessingDuration.Record(durationMs, new KeyValuePair<string, object?>("protocol", protocol));
        _lastProcessingLatency = durationMs;
    }
    
    public void RecordMessagePublished(string protocol, double durationMs)
    {
        _messagesPublishedCounter.Add(1, new KeyValuePair<string, object?>("protocol", protocol));
        _kafkaPublishDuration.Record(durationMs, new KeyValuePair<string, object?>("protocol", protocol));
        _lastKafkaLatency = durationMs;
    }
    
    public void RecordMessageFailed(string protocol, string errorType)
    {
        _messagesFailedCounter.Add(1, 
            new KeyValuePair<string, object?>("protocol", protocol),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
    
    public void RecordConnectionOpened(string connectionType)
    {
        _connectionsOpenedCounter.Add(1, new KeyValuePair<string, object?>("type", connectionType));
        Interlocked.Increment(ref _activeConnections);
    }
    
    public void RecordConnectionClosed(string connectionType)
    {
        _connectionsClosedCounter.Add(1, new KeyValuePair<string, object?>("type", connectionType));
        Interlocked.Decrement(ref _activeConnections);
    }
    
    public void UpdateActiveConnections(int count)
    {
        _activeConnections = count;
    }
    
    public void Dispose()
    {
        _meter?.Dispose();
        _logger.LogInformation("Metrics service disposed");
    }
}