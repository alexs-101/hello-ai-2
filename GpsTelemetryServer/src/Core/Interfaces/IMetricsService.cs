namespace Core.Interfaces;

public interface IMetricsService
{
    void RecordMessageReceived(string protocol, long sizeBytes);
    void RecordMessageProcessed(string protocol, double durationMs);
    void RecordMessagePublished(string protocol, double durationMs);
    void RecordMessageFailed(string protocol, string errorType);
    void RecordConnectionOpened(string connectionType);
    void RecordConnectionClosed(string connectionType);
    void UpdateActiveConnections(int count);
}