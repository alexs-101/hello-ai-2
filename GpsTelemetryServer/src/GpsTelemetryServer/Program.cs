using Core.Interfaces;
using Core.Services;
using Core.Configuration;
using GpsTelemetryServer.Services;
using GpsTelemetryServer.HealthChecks;
using PluginFramework;
using Serilog;
using OpenTelemetry.Extensions.Hosting;

// Configure Server GC for high-throughput scenarios
if (Environment.GetEnvironmentVariable("DOTNET_gcServer") != "0")
{
    Environment.SetEnvironmentVariable("DOTNET_gcServer", "1");
}

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: 
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/telemetry-server-.log", 
            rollingInterval: RollingInterval.Day,
            outputTemplate: 
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
});

// Add configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("TELEMETRY_")
    .AddCommandLine(args);

// Configure strongly-typed options
builder.Services.Configure<TelemetryServerOptions>(
    builder.Configuration.GetSection("TelemetryServer"));
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<ConnectionManagerOptions>(
    builder.Configuration.GetSection("ConnectionManager"));
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection("Monitoring"));

// Add core services
builder.Services.AddSingleton<IPluginManager, PluginManager>();
builder.Services.AddSingleton<ITelemetryProcessor, TelemetryProcessor>();
builder.Services.AddSingleton<IKafkaPublisher, KafkaPublisher>();
builder.Services.AddSingleton<ConnectionManager>();

// Add background service
builder.Services.AddHostedService<TelemetryBackgroundService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<TelemetryHealthCheck>("telemetry");

// Add OpenTelemetry (placeholder for full implementation)
builder.Services.AddOpenTelemetry();

// Configure API endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

// Health check endpoint
app.MapHealthChecks("/health");

// API endpoints
app.MapGet("/", () => new { 
    Service = "GPS Telemetry Server", 
    Version = "1.0.0", 
    Status = "Running",
    Timestamp = DateTime.UtcNow 
});

app.MapGet("/stats", (ConnectionManager connectionManager) => 
{
    var stats = connectionManager.GetStatistics();
    return Results.Ok(stats);
});

app.MapControllers();

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("GPS Telemetry Server is shutting down...");
});

try
{
    Log.Information("Starting GPS Telemetry Server...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GPS Telemetry Server failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
