using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class GpsData
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;
    
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