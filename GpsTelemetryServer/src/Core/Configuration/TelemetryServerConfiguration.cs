using Core.Services;

namespace Core.Configuration;

public class TelemetryServerConfiguration
{
    public TelemetryServerOptions TelemetryServer { get; set; } = new();
    public KafkaOptions Kafka { get; set; } = new();
    public PluginSettings PluginSettings { get; set; } = new();
    public ConnectionManagerOptions ConnectionManager { get; set; } = new();
    public MonitoringOptions Monitoring { get; set; } = new();
}

public class TelemetryServerOptions
{
    public bool TcpEnabled { get; set; } = true;
    public int TcpPort { get; set; } = 8080;
    public bool UdpEnabled { get; set; } = true;
    public int UdpPort { get; set; } = 8081;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int MaxConcurrentConnections { get; set; } = 10000;
    public int BufferSize { get; set; } = 4096;
    public bool UseServerGC { get; set; } = true;
}

public class PluginSettings
{
    public string Directory { get; set; } = "./plugins";
    public bool EnableHotReload { get; set; } = false;
    public int ReloadDelayMs { get; set; } = 100;
    public string[] PluginPaths { get; set; } = Array.Empty<string>();
}

public class MonitoringOptions
{
    public bool EnableOpenTelemetry { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public string ServiceName { get; set; } = "gps-telemetry-server";
    public string ServiceVersion { get; set; } = "1.0.0";
    public OpenTelemetryExporters Exporters { get; set; } = new();
}

public class OpenTelemetryExporters
{
    public ConsoleExporter Console { get; set; } = new();
    public OtlpExporter Otlp { get; set; } = new();
    public PrometheusExporter Prometheus { get; set; } = new();
}

public class ConsoleExporter
{
    public bool Enabled { get; set; } = true;
}

public class OtlpExporter
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = "http://localhost:4317";
    public Dictionary<string, string> Headers { get; set; } = new();
}

public class PrometheusExporter
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = "/metrics";
    public int Port { get; set; } = 9090;
}