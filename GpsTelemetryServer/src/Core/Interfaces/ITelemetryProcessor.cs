using Core.Models;

namespace Core.Interfaces;

public interface ITelemetryProcessor
{
    Task<GpsData?> ProcessAsync(byte[] data, string deviceId);
    Task<bool> IsHealthyAsync();
}