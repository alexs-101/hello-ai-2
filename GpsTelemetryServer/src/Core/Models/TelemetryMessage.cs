using System.Net;

namespace Core.Models;

public class TelemetryMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public string Protocol { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public IPEndPoint? RemoteEndPoint { get; set; }
}