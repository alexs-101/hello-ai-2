using Core.Models;

namespace Core.Interfaces;

public interface IKafkaPublisher
{
    Task PublishAsync(GpsData gpsData);
    Task<bool> IsHealthyAsync();
    Task FlushAsync(TimeSpan timeout);
}