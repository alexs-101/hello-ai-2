using Core.Models;
using Microsoft.Extensions.Configuration;

namespace Core.Interfaces;

public enum ProtocolType
{
    Nmea,
    Ublox,
    Taip,
    Other
}

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