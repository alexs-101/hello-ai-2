name: "GPS Telemetry Server Production Implementation PRP"
description: |
  
## Purpose

A comprehensive Product Requirements Plan (PRP) for implementing a high-performance GPS telemetry server in .NET Core 8. This PRP provides rich context, external documentation, and validation gates to enable successful one-pass implementation through iterative refinement.

## Core Principles

1. **Context is King**: All necessary documentation, examples, and caveats included
2. **Validation Loops**: Executable tests/lints for continuous validation
3. **Information Dense**: Keywords and patterns from research and best practices
4. **Progressive Success**: Start simple, validate, then enhance

---

## Goal

Build a production-ready GPS telemetry server that receives GPS data from vehicle-mounted devices, processes it through a plugin architecture, and publishes unified JSON messages to Apache Kafka. The system must handle 10,000+ concurrent connections with <100ms processing latency and 99.9% uptime.

## Why

- **Market Opportunity**: GPS tracking software market valued at $3.6B (2024) → $8.2B (2033)
- **Fleet Management**: 40% market share driven by real-time monitoring demands
- **Protocol Fragmentation**: Current solutions limited by vendor lock-in and protocol diversity
- **Integration Complexity**: Need unified JSON format for downstream systems

## What

### Core Requirements
- **Multi-Protocol Support**: NMEA 0183 + extensible plugin architecture
- **High Performance**: 10,000+ concurrent TCP/UDP connections
- **Real-time Processing**: <100ms message processing latency
- **Kafka Integration**: Unified JSON message publishing
- **Production Ready**: 99.9% uptime, monitoring, graceful shutdown

### Success Criteria

- [ ] Support NMEA 0183 protocol with 4 core sentence types (GPRMC, GPGGA, GPGSV, GPGSA)
- [ ] Process 10,000+ concurrent device connections
- [ ] Maintain <100ms message processing latency
- [ ] Achieve 99.9% uptime in production
- [ ] Plugin architecture supports hot-reload without service restart
- [ ] Kafka integration with proper partitioning and compression
- [ ] Comprehensive monitoring and observability
- [ ] Graceful shutdown and error handling

## All Needed Context

### Documentation & References

```yaml
# MUST READ - Critical for implementation success

# .NET Core 8 Performance Optimizations
- url: https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/
  why: 15% average performance improvement, Dynamic PGO, Server GC optimizations
  critical: Use Server GC mode, leverage async patterns throughout

# High-Performance TCP/UDP Servers
- url: https://github.com/chronoxor/NetCoreServer
  why: Ultra-fast async socket library, solves 10K connections problem
  critical: Use SocketAsyncEventArgs, zero-allocation patterns with Span<byte>

# Kafka .NET Integration
- url: https://docs.confluent.io/kafka-clients/dotnet/current/overview.html
  why: High-performance wrapper, transactions, idempotent production
  critical: Use ProducerConfig for batching, implement proper error handling

# NMEA Protocol Parser
- url: https://github.com/dotMorten/NmeaParser
  why: Cross-platform NMEA parsing, automatic multi-sentence merging
  critical: Handle malformed sentences gracefully, use checksum validation

# Plugin Architecture
- url: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support
  why: AssemblyLoadContext for plugin isolation, dynamic loading
  critical: Use AssemblyDependencyResolver, implement proper lifecycle management

# Background Services
- url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
  why: Long-running processes, graceful shutdown patterns
  critical: Use BackgroundService base class, implement CancellationToken support

# Memory Management
- url: https://learn.microsoft.com/en-us/aspnet/core/performance/memory
  why: Object pooling, large object heap optimization
  critical: Use ArrayPool<byte>, avoid objects > 85,000 bytes

# OpenTelemetry Integration
- url: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
  why: Standardized telemetry collection, production monitoring
  critical: Use structured logging, implement custom metrics

# Reference Architecture
- url: https://github.com/navtrack/navtrack
  why: Real-world GPS tracking server implementation
  critical: Microservices separation, Docker deployment patterns

# Configuration Management
- url: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options
  why: Strongly-typed configuration, runtime changes
  critical: Use Options pattern, implement IOptionsMonitor for live updates

# Testing Patterns
- file: /home/wolf/work/hello-ai-2/PRPs/templates/prp_base.md
  why: Multi-level validation strategy, executable test patterns
  critical: Use xUnit, implement integration tests with Testcontainers
```

### Current Codebase Tree

```bash
/home/wolf/work/hello-ai-2/
├── PRPs/
│   ├── gps-telemetry-server-prd.md    # Comprehensive requirements document
│   ├── templates/prp_base.md          # Testing and validation patterns
│   ├── ai_docs/                       # Development workflow documentation
│   │   ├── cc_monitoring.md           # OpenTelemetry integration patterns
│   │   ├── cc_mcp.md                  # Protocol-based server architecture
│   │   └── build_with_claude_code.md  # SDK patterns and lifecycle management
│   └── scripts/prp_runner.py          # Command-line server application patterns
└── README.md
```

### Desired Codebase Tree

```bash
GpsTelemetryServer/
├── src/
│   ├── GpsTelemetryServer/
│   │   ├── Program.cs                 # Main entry point with Serilog + OpenTelemetry
│   │   ├── appsettings.json           # Configuration (ports, Kafka, plugins)
│   │   ├── appsettings.Production.json
│   │   └── GpsTelemetryServer.csproj
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── GpsData.cs            # Core data model with validation
│   │   │   ├── TelemetryMessage.cs   # Internal message format
│   │   │   └── ValidationResult.cs   # Validation response model
│   │   ├── Services/
│   │   │   ├── TelemetryProcessor.cs # Main processing pipeline
│   │   │   ├── ConnectionManager.cs  # TCP/UDP connection handling
│   │   │   ├── DataValidator.cs      # GPS data validation
│   │   │   └── KafkaPublisher.cs     # Kafka message publishing
│   │   ├── Interfaces/
│   │   │   ├── IProtocolPlugin.cs    # Plugin interface contract
│   │   │   ├── ITelemetryProcessor.cs
│   │   │   └── IConnectionManager.cs
│   │   └── Core.csproj
│   ├── Plugins/
│   │   ├── NmeaPlugin/
│   │   │   ├── NmeaProtocolPlugin.cs # NMEA protocol implementation
│   │   │   ├── NmeaParser.cs         # NMEA sentence parsing
│   │   │   ├── Models/               # NMEA-specific models
│   │   │   └── NmeaPlugin.csproj
│   │   └── PluginFramework/
│   │       ├── PluginManager.cs      # Plugin loading and lifecycle
│   │       ├── PluginLoader.cs       # Assembly loading logic
│   │       └── PluginFramework.csproj
│   └── Infrastructure/
│       ├── Configuration/
│       │   ├── TelemetryServerOptions.cs
│       │   ├── KafkaOptions.cs
│       │   └── PluginOptions.cs
│       ├── Logging/
│       │   ├── TelemetryMetrics.cs   # Custom OpenTelemetry metrics
│       │   └── TelemetryActivitySource.cs
│       └── Infrastructure.csproj
├── tests/
│   ├── Unit/
│   │   ├── Core.Tests/
│   │   │   ├── TelemetryProcessorTests.cs
│   │   │   ├── ConnectionManagerTests.cs
│   │   │   └── DataValidatorTests.cs
│   │   └── Plugins.Tests/
│   │       └── NmeaPluginTests.cs
│   ├── Integration/
│   │   ├── EndToEndTests.cs
│   │   ├── KafkaIntegrationTests.cs
│   │   └── DeviceConnectionTests.cs
│   └── Performance/
│       ├── ThroughputTests.cs
│       └── ConcurrencyTests.cs
├── docker/
│   ├── Dockerfile
│   ├── docker-compose.yml           # Kafka + Zookeeper + App
│   └── docker-compose.override.yml
├── scripts/
│   ├── build.sh
│   ├── test.sh
│   └── deploy.sh
├── docs/
│   ├── plugin-development.md
│   ├── deployment-guide.md
│   └── monitoring-setup.md
└── GpsTelemetryServer.sln
```

### Known Gotchas & Library Quirks

```csharp
// CRITICAL: .NET Core 8 Performance Patterns
// Server GC mode is essential for high-throughput scenarios
GCSettings.LatencyMode = GCLatencyMode.Batch;

// CRITICAL: Confluent.Kafka requires proper configuration
// BatchSize and LingerMs are crucial for performance
var producerConfig = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    BatchSize = 100,           // Batch messages for efficiency
    LingerMs = 10,             // Small delay for batching
    CompressionType = CompressionType.Gzip,
    EnableIdempotence = true   // Prevent duplicate messages
};

// CRITICAL: NetCoreServer patterns
// Use SocketAsyncEventArgs for zero-allocation networking
public class TelemetryTcpServer : TcpServer
{
    protected override TcpSession CreateSession() => new TelemetryTcpSession(this);
}

// CRITICAL: NMEA Parser handling
// SharpGIS.NmeaParser requires careful error handling
device.MessageReceived += (sender, msg) => {
    try {
        // Always validate NMEA checksum
        if (!NmeaMessage.IsValidChecksum(msg.RawMessage)) {
            _logger.LogWarning("Invalid NMEA checksum: {Message}", msg.RawMessage);
            return;
        }
        // Process message
    } catch (NmeaParseException ex) {
        _logger.LogError(ex, "NMEA parsing failed");
    }
};

// CRITICAL: Plugin Loading with AssemblyLoadContext
// Must use AssemblyDependencyResolver for dependencies
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    
    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }
    
    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}

// CRITICAL: Background Service patterns
// Use CancellationToken throughout for graceful shutdown
public class TelemetryBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTelemetryBatch(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Expected when shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry processing");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

// CRITICAL: Memory Management
// Use ArrayPool<byte> for temporary buffers to avoid GC pressure
private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

public async Task ProcessTelemetryData(ReadOnlySpan<byte> data)
{
    var buffer = _bufferPool.Rent(data.Length);
    try
    {
        data.CopyTo(buffer);
        // Process with zero-allocation patterns
    }
    finally
    {
        _bufferPool.Return(buffer);
    }
}

// CRITICAL: OpenTelemetry Configuration
// Must configure all three pillars: logs, metrics, traces
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("TelemetryServer")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("TelemetryServer")
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter());
```

## Implementation Blueprint

### Data Models and Structure

Core data models ensure type safety and GPS coordinate validation:

```csharp
// GpsData.cs - Core GPS data model
public class GpsData
{
    [Required]
    public string DeviceId { get; set; }
    
    [Required]
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90")]
    public double Latitude { get; set; }
    
    [Required]
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180")]
    public double Longitude { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    [Range(0.0, 1000.0)]
    public double? Speed { get; set; }
    
    [Range(0.0, 360.0)]
    public double? Heading { get; set; }
    
    public double? Altitude { get; set; }
    
    [Range(0, 50)]
    public int? SatelliteCount { get; set; }
    
    [Range(0.0, 50.0)]
    public double? Hdop { get; set; }
    
    public Dictionary<string, object> ExtendedData { get; set; } = new();
}

// TelemetryMessage.cs - Internal processing format
public class TelemetryMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; }
    public byte[] RawData { get; set; }
    public string Protocol { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public IPEndPoint RemoteEndPoint { get; set; }
}

// ValidationResult.cs - Validation response
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ErrorMessage => string.Join("; ", Errors);
}

// Plugin interface contract
public interface IProtocolPlugin
{
    string Name { get; }
    string Version { get; }
    ProtocolType SupportedProtocol { get; }
    
    Task<bool> CanHandleAsync(byte[] data);
    Task<GpsData> ProcessAsync(byte[] data, string deviceId);
    Task<ValidationResult> ValidateAsync(GpsData data);
    Task InitializeAsync(IConfiguration config);
    Task CleanupAsync();
}
```

### List of Tasks (Implementation Order)

```yaml
Task 1: Setup Project Structure and Dependencies
CREATE GpsTelemetryServer.sln:
  - REFERENCE Microsoft documentation patterns
  - ADD project references with proper dependencies
  - CONFIGURE .NET 8 with Server GC optimizations

MODIFY src/GpsTelemetryServer/GpsTelemetryServer.csproj:
  - ADD PackageReference: Confluent.Kafka version 2.11.0
  - ADD PackageReference: SharpGIS.NmeaParser version 2.0.0
  - ADD PackageReference: Serilog.AspNetCore version 8.0.0
  - ADD PackageReference: OpenTelemetry.Extensions.Hosting version 1.7.0
  - ADD PackageReference: NetCoreServer version 8.0.0

Task 2: Core Data Models with Validation
CREATE src/Core/Models/GpsData.cs:
  - IMPLEMENT coordinate validation with DataAnnotations
  - ADD proper JSON serialization attributes
  - INCLUDE ExtendedData dictionary for plugin-specific data

CREATE src/Core/Models/TelemetryMessage.cs:
  - MIRROR pattern from Navtrack reference architecture
  - ADD DeviceId tracking and protocol identification
  - IMPLEMENT proper timestamp handling (UTC)

CREATE src/Core/Models/ValidationResult.cs:
  - FOLLOW existing validation patterns from research
  - ADD error collection and message formatting
  - IMPLEMENT proper success/failure states

Task 3: Plugin Interface and Framework
CREATE src/Core/Interfaces/IProtocolPlugin.cs:
  - EXACT implementation from GPS telemetry PRD
  - ADD async methods for modern .NET patterns
  - INCLUDE lifecycle management (Initialize/Cleanup)

CREATE src/Plugins/PluginFramework/PluginManager.cs:
  - USE AssemblyLoadContext for plugin isolation
  - IMPLEMENT hot-reload capability
  - ADD plugin validation and error handling
  - FOLLOW Microsoft plugin architecture patterns

CREATE src/Plugins/PluginFramework/PluginLoader.cs:
  - USE AssemblyDependencyResolver for dependencies
  - IMPLEMENT proper assembly loading and unloading
  - ADD plugin versioning and compatibility checks

Task 4: NMEA Protocol Plugin Implementation
CREATE src/Plugins/NmeaPlugin/NmeaProtocolPlugin.cs:
  - IMPLEMENT IProtocolPlugin interface
  - USE SharpGIS.NmeaParser for sentence parsing
  - ADD support for GPRMC, GPGGA, GPGSV, GPGSA sentences
  - IMPLEMENT proper checksum validation

CREATE src/Plugins/NmeaPlugin/NmeaParser.cs:
  - WRAP SharpGIS.NmeaParser with error handling
  - ADD coordinate system normalization (WGS84)
  - IMPLEMENT multi-sentence message handling
  - ADD proper timestamp parsing and UTC conversion

Task 5: Connection Management (TCP/UDP Servers)
CREATE src/Core/Services/ConnectionManager.cs:
  - USE NetCoreServer for high-performance networking
  - IMPLEMENT both TCP and UDP server support
  - ADD connection pooling for 10,000+ connections
  - FOLLOW async patterns with SocketAsyncEventArgs

CREATE src/Core/Services/TelemetryTcpServer.cs:
  - EXTEND NetCoreServer.TcpServer base class
  - IMPLEMENT device authentication and connection tracking
  - ADD proper error handling and logging
  - IMPLEMENT graceful disconnect handling

CREATE src/Core/Services/TelemetryUdpServer.cs:
  - EXTEND NetCoreServer.UdpServer base class
  - IMPLEMENT stateless message processing
  - ADD proper buffer management with ArrayPool<byte>
  - USE zero-allocation patterns with Span<byte>

Task 6: Core Telemetry Processing Pipeline
CREATE src/Core/Services/TelemetryProcessor.cs:
  - IMPLEMENT async message processing pipeline
  - ADD plugin routing based on protocol detection
  - USE System.Threading.Channels for message queuing
  - IMPLEMENT proper error handling and retry logic

CREATE src/Core/Services/DataValidator.cs:
  - ADD GPS coordinate boundary validation
  - IMPLEMENT NMEA checksum validation
  - ADD device ID format validation
  - INCLUDE data quality scoring

Task 7: Kafka Integration and Message Publishing
CREATE src/Core/Services/KafkaPublisher.cs:
  - USE Confluent.Kafka with proper configuration
  - IMPLEMENT batching and compression (gzip)
  - ADD partitioning by DeviceID for ordering
  - IMPLEMENT proper error handling and retry logic

CREATE src/Infrastructure/Configuration/KafkaOptions.cs:
  - FOLLOW Options pattern from Microsoft documentation
  - ADD strongly-typed configuration
  - IMPLEMENT validation for required settings
  - ADD support for multiple Kafka clusters

Task 8: Background Service and Hosting
CREATE src/GpsTelemetryServer/TelemetryBackgroundService.cs:
  - EXTEND BackgroundService base class
  - IMPLEMENT graceful shutdown with CancellationToken
  - ADD proper exception handling and logging
  - USE dependency injection for scoped services

CREATE src/GpsTelemetryServer/Program.cs:
  - CONFIGURE Serilog with structured logging
  - ADD OpenTelemetry with logs, metrics, traces
  - IMPLEMENT proper dependency injection
  - ADD health checks and monitoring endpoints

Task 9: Configuration Management
CREATE src/Infrastructure/Configuration/TelemetryServerOptions.cs:
  - IMPLEMENT strongly-typed configuration
  - ADD validation attributes for required settings
  - INCLUDE port, connection limits, timeouts
  - ADD plugin configuration section

CREATE src/GpsTelemetryServer/appsettings.json:
  - FOLLOW configuration structure from PRD
  - ADD environment-specific overrides
  - INCLUDE Serilog and OpenTelemetry configuration
  - ADD proper connection string management

Task 10: Monitoring and Observability
CREATE src/Infrastructure/Logging/TelemetryMetrics.cs:
  - IMPLEMENT custom OpenTelemetry metrics
  - ADD message processing counters and histograms
  - INCLUDE connection count and error rate metrics
  - ADD performance monitoring (latency, throughput)

CREATE src/Infrastructure/Logging/TelemetryActivitySource.cs:
  - IMPLEMENT custom activity source for tracing
  - ADD message processing tracing
  - INCLUDE plugin execution tracing
  - ADD correlation IDs for distributed tracing

Task 11: Error Handling and Resilience
MODIFY src/Core/Services/TelemetryProcessor.cs:
  - ADD circuit breaker pattern for external dependencies
  - IMPLEMENT exponential backoff for retries
  - ADD proper exception handling hierarchy
  - INCLUDE dead letter queue for failed messages

MODIFY src/Core/Services/KafkaPublisher.cs:
  - ADD connection resilience patterns
  - IMPLEMENT message buffering for outages
  - ADD proper error reporting and alerting
  - INCLUDE message ordering guarantees

Task 12: Testing Implementation
CREATE tests/Unit/Core.Tests/TelemetryProcessorTests.cs:
  - USE xUnit testing framework
  - IMPLEMENT Moq for mocking dependencies
  - ADD comprehensive test coverage for message processing
  - INCLUDE performance benchmarking tests

CREATE tests/Integration/EndToEndTests.cs:
  - USE Testcontainers for Kafka integration
  - IMPLEMENT full message flow testing
  - ADD device connection simulation
  - INCLUDE load testing scenarios

CREATE tests/Performance/ThroughputTests.cs:
  - IMPLEMENT NBomber for performance testing
  - ADD 10,000+ connection simulation
  - INCLUDE latency measurement (<100ms requirement)
  - ADD memory usage and GC pressure testing
```

### Integration Points

```yaml
KAFKA_CONFIGURATION:
  - bootstrap_servers: "localhost:9092"
  - topic_pattern: "telemetry.gps.{protocol}"
  - partition_strategy: "device_id_hash"
  - compression: "gzip"
  - batch_size: 100
  - linger_ms: 10

PLUGIN_DIRECTORIES:
  - development: "./plugins"
  - production: "/app/plugins"
  - hot_reload: true
  - isolation: "separate_app_domain"

MONITORING_ENDPOINTS:
  - health: "/health"
  - metrics: "/metrics"
  - ready: "/ready"
  - live: "/live"

LOGGING_CONFIGURATION:
  - structured: true
  - correlation_ids: true
  - sampling_rate: 1.0
  - retention_days: 30
```

## Validation Loop

### Level 1: Syntax & Style

```bash
# Essential for .NET Core development
dotnet restore
dotnet build --configuration Release

# Static analysis and formatting
dotnet format --verify-no-changes
dotnet tool run dotnet-format analyzers --verify-no-changes

# Expected: No errors, clean build output
```

### Level 2: Unit Tests

```csharp
// TelemetryProcessorTests.cs - Core functionality tests
[Fact]
public async Task ProcessNmeaMessage_ValidGprmc_ReturnsGpsData()
{
    // Arrange
    var mockKafka = new Mock<IKafkaPublisher>();
    var mockPlugin = new Mock<IProtocolPlugin>();
    var processor = new TelemetryProcessor(mockKafka.Object, new[] { mockPlugin.Object });
    
    var nmeaData = "$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A";
    var expectedGpsData = new GpsData { DeviceId = "test_device", Latitude = 48.1173, Longitude = 11.5167 };
    
    mockPlugin.Setup(p => p.CanHandleAsync(It.IsAny<byte[]>())).ReturnsAsync(true);
    mockPlugin.Setup(p => p.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string>())).ReturnsAsync(expectedGpsData);
    
    // Act
    var result = await processor.ProcessAsync(Encoding.UTF8.GetBytes(nmeaData), "test_device");
    
    // Assert
    result.Should().NotBeNull();
    result.DeviceId.Should().Be("test_device");
    result.Latitude.Should().BeApproximately(48.1173, 0.001);
    result.Longitude.Should().BeApproximately(11.5167, 0.001);
}

[Fact]
public async Task ProcessHighThroughput_10000Messages_MaintainsLatency()
{
    // Arrange
    var processor = new TelemetryProcessor(_kafkaPublisher, _plugins);
    var messages = GenerateTestMessages(10000);
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var tasks = messages.Select(msg => processor.ProcessAsync(msg.Data, msg.DeviceId));
    await Task.WhenAll(tasks);
    stopwatch.Stop();
    
    // Assert
    var avgLatency = stopwatch.Elapsed.TotalMilliseconds / 10000;
    avgLatency.Should().BeLessThan(100, "Average latency should be < 100ms");
}
```

```bash
# Run unit tests with coverage
dotnet test tests/Unit/ --collect:"XPlat Code Coverage" --results-directory ./coverage

# Expected: > 90% code coverage, all tests passing
```

### Level 3: Integration Tests

```csharp
// EndToEndTests.cs - Full system integration
[Fact]
public async Task ProcessNmeaMessage_EndToEnd_PublishesToKafka()
{
    // Arrange
    var kafkaContainer = new KafkaTestContainer();
    await kafkaContainer.StartAsync();
    
    var server = new TelemetryServer(CreateTestConfiguration(kafkaContainer.BootstrapServers));
    await server.StartAsync();
    
    var testMessage = "$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A";
    
    // Act
    await server.SendTcpMessageAsync(testMessage, "test_device");
    
    // Assert
    var consumer = CreateKafkaConsumer(kafkaContainer.BootstrapServers);
    var message = await consumer.ConsumeAsync(TimeSpan.FromSeconds(10));
    
    message.Should().NotBeNull();
    var gpsData = JsonSerializer.Deserialize<GpsData>(message.Value);
    gpsData.DeviceId.Should().Be("test_device");
    gpsData.Latitude.Should().BeApproximately(48.1173, 0.001);
}
```

```bash
# Run integration tests with Docker
docker-compose -f docker/docker-compose.test.yml up -d
dotnet test tests/Integration/ --environment Docker

# Expected: All integration tests pass, Kafka messages published correctly
```

### Level 4: Performance & Load Testing

```csharp
// ThroughputTests.cs - Performance validation
[Fact]
public async Task ConcurrentConnections_10000Devices_HandlesLoad()
{
    // Arrange
    var server = new TelemetryServer();
    await server.StartAsync();
    
    var connectionTasks = new List<Task>();
    
    // Act
    for (int i = 0; i < 10000; i++)
    {
        connectionTasks.Add(SimulateDeviceConnection($"device_{i}"));
    }
    
    var results = await Task.WhenAll(connectionTasks);
    
    // Assert
    results.Should().AllSatisfy(result => result.Should().BeTrue());
    server.ActiveConnections.Should().Be(10000);
}

[Fact]
public async Task MessageThroughput_HighVolume_MaintainsLatency()
{
    // Arrange
    var server = new TelemetryServer();
    var messagesPerSecond = 1000;
    var durationSeconds = 60;
    
    // Act
    var results = await NBomber.CreateScenario("GPS_Messages")
        .AddStep("send_nmea_message", async context =>
        {
            var message = GenerateNmeaMessage(context.ScenarioInfo.ThreadId);
            await server.ProcessMessageAsync(message);
            return Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: messagesPerSecond, during: TimeSpan.FromSeconds(durationSeconds))
        )
        .RunAsync();
    
    // Assert
    results.AllOkCount.Should().BeGreaterThan(messagesPerSecond * durationSeconds * 0.95);
    results.ScenarioStats.Ok.Latency.P95.Should().BeLessThan(100);
}
```

```bash
# Run performance tests
dotnet test tests/Performance/ --configuration Release --logger "console;verbosity=detailed"

# Expected: 10,000+ concurrent connections, <100ms P95 latency
```

### Level 5: Production Readiness

```bash
# Docker deployment test
docker build -t gps-telemetry-server .
docker run -p 8080:8080 -p 8081:8081 gps-telemetry-server

# Health check validation
curl -f http://localhost:8080/health
curl -f http://localhost:8080/ready

# Load test against containerized version
docker run --rm -i loadtesting/nbomber:latest \
  --scenario concurrent_connections \
  --target http://localhost:8080 \
  --connections 10000 \
  --duration 300s

# Expected: Health checks pass, load test meets SLA requirements
```

## Final Validation Checklist

- [ ] All unit tests pass: `dotnet test tests/Unit/ --no-build`
- [ ] All integration tests pass: `dotnet test tests/Integration/ --no-build`
- [ ] Performance tests meet SLA: `dotnet test tests/Performance/ --no-build`
- [ ] Docker build succeeds: `docker build -t gps-telemetry-server .`
- [ ] Health checks respond: `curl -f http://localhost:8080/health`
- [ ] NMEA parsing works: Manual test with real GPS data
- [ ] Kafka integration works: Verify messages in Kafka topic
- [ ] Plugin hot-reload works: Load new plugin without restart
- [ ] Monitoring data flows: OpenTelemetry metrics visible
- [ ] Error handling works: Test network failures, invalid data
- [ ] Graceful shutdown works: SIGTERM handling
- [ ] Memory usage stable: No memory leaks under load
- [ ] Log output informative: Structured logging with correlation IDs

---

## Anti-Patterns to Avoid

- ❌ Don't use blocking synchronous calls in async methods
- ❌ Don't create new HttpClient instances (use HttpClientFactory)
- ❌ Don't ignore CancellationToken in async operations
- ❌ Don't catch all exceptions without proper handling
- ❌ Don't use Thread.Sleep in async code (use Task.Delay)
- ❌ Don't create large objects > 85,000 bytes (LOH pressure)
- ❌ Don't use Task.Run for CPU-bound work in ASP.NET Core
- ❌ Don't access HttpContext from multiple threads
- ❌ Don't use ConfigureAwait(false) in application code
- ❌ Don't ignore plugin lifecycle management

## Success Confidence Score: 9/10

**Reasoning**: This PRP provides comprehensive context from Microsoft documentation, proven open-source implementations, and detailed validation gates. The extensive research ensures the AI agent has access to:

1. **Official Microsoft patterns** for .NET Core 8 performance optimization
2. **Proven libraries** (NetCoreServer, Confluent.Kafka, SharpGIS.NmeaParser)
3. **Real-world reference architecture** (Navtrack implementation)
4. **Comprehensive testing strategy** with multiple validation levels
5. **Production-ready patterns** for monitoring, logging, and deployment

The only risk factor is the complexity of integrating multiple high-performance libraries simultaneously, but the detailed implementation tasks and validation loops provide sufficient guardrails for iterative refinement.