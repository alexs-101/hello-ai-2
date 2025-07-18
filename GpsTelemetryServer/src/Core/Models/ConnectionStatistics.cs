namespace Core.Models;

public class ConnectionStatistics
{
    public int ActiveTcpConnections { get; set; }
    public int ActiveUdpConnections { get; set; }
    public long TotalMessagesReceived { get; set; }
    public double MessagesPerSecond { get; set; }
    public double UptimeSeconds { get; set; }
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
}