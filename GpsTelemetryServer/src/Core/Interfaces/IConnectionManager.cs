namespace Core.Interfaces;

public interface IConnectionManager
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    int ActiveConnections { get; }
    bool IsListening { get; }
    Task<bool> IsHealthyAsync();
}