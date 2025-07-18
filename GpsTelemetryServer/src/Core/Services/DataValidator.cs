using Core.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using CoreValidationResult = Core.Models.ValidationResult;

namespace Core.Services;

public class DataValidator
{
    private readonly ILogger<DataValidator> _logger;

    public DataValidator(ILogger<DataValidator> logger)
    {
        _logger = logger;
    }

    public CoreValidationResult ValidateGpsData(GpsData data)
    {
        var result = new CoreValidationResult { IsValid = true };
        
        try
        {
            // Basic data annotation validation
            var context = new ValidationContext(data);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            if (!Validator.TryValidateObject(data, context, validationResults, true))
            {
                result.IsValid = false;
                result.Errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
            }

            // Additional GPS-specific validation
            ValidateCoordinates(data, result);
            ValidateTimestamp(data, result);
            ValidateSpeed(data, result);
            ValidateHeading(data, result);
            ValidateSatelliteData(data, result);

            if (!result.IsValid)
            {
                _logger.LogWarning("GPS data validation failed for device {DeviceId}: {Errors}", 
                    data.DeviceId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating GPS data for device {DeviceId}", data.DeviceId);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    private void ValidateCoordinates(GpsData data, CoreValidationResult result)
    {
        // Check for null island (0,0) coordinates
        if (Math.Abs(data.Latitude) < 0.0001 && Math.Abs(data.Longitude) < 0.0001)
        {
            result.IsValid = false;
            result.Errors.Add("Coordinates cannot be at null island (0,0)");
        }

        // Check for reasonable coordinate ranges
        if (data.Latitude < -90 || data.Latitude > 90)
        {
            result.IsValid = false;
            result.Errors.Add($"Latitude {data.Latitude} is outside valid range (-90 to 90)");
        }

        if (data.Longitude < -180 || data.Longitude > 180)
        {
            result.IsValid = false;
            result.Errors.Add($"Longitude {data.Longitude} is outside valid range (-180 to 180)");
        }

        // Check for precision issues (too many decimal places might indicate bad data)
        var latString = data.Latitude.ToString("F10");
        var lonString = data.Longitude.ToString("F10");
        
        if (latString.EndsWith("00000") || lonString.EndsWith("00000"))
        {
            // This might indicate low precision data, log as warning but don't fail
            _logger.LogTrace("Low precision coordinates detected for device {DeviceId}: {Lat}, {Lon}", 
                data.DeviceId, data.Latitude, data.Longitude);
        }
    }

    private void ValidateTimestamp(GpsData data, CoreValidationResult result)
    {
        var now = DateTime.UtcNow;
        var timeDiff = Math.Abs((now - data.Timestamp).TotalHours);

        // Allow up to 24 hours in the past or 1 hour in the future
        if (timeDiff > 24)
        {
            if (data.Timestamp > now)
            {
                result.IsValid = false;
                result.Errors.Add($"Timestamp {data.Timestamp:yyyy-MM-dd HH:mm:ss} is too far in the future");
            }
            else
            {
                // Old timestamps might be acceptable for historical data, but log warning
                _logger.LogWarning("Old timestamp detected for device {DeviceId}: {Timestamp}", 
                    data.DeviceId, data.Timestamp);
            }
        }

        // Check for default/invalid timestamps
        if (data.Timestamp == default || data.Timestamp.Year < 2000)
        {
            result.IsValid = false;
            result.Errors.Add($"Invalid timestamp: {data.Timestamp}");
        }
    }

    private void ValidateSpeed(GpsData data, CoreValidationResult result)
    {
        if (data.Speed.HasValue)
        {
            // Check for reasonable speed limits (1000 km/h = ~620 mph, faster than most aircraft)
            if (data.Speed.Value < 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Speed cannot be negative: {data.Speed.Value}");
            }
            else if (data.Speed.Value > 1000)
            {
                result.IsValid = false;
                result.Errors.Add($"Speed {data.Speed.Value} km/h exceeds reasonable limit (1000 km/h)");
            }
            else if (data.Speed.Value > 300)
            {
                // High speed warning (300 km/h = ~186 mph)
                _logger.LogWarning("High speed detected for device {DeviceId}: {Speed} km/h", 
                    data.DeviceId, data.Speed.Value);
            }
        }
    }

    private void ValidateHeading(GpsData data, CoreValidationResult result)
    {
        if (data.Heading.HasValue)
        {
            if (data.Heading.Value < 0 || data.Heading.Value >= 360)
            {
                result.IsValid = false;
                result.Errors.Add($"Heading {data.Heading.Value} must be between 0 and 359.99 degrees");
            }
        }
    }

    private void ValidateSatelliteData(GpsData data, CoreValidationResult result)
    {
        if (data.SatelliteCount.HasValue)
        {
            if (data.SatelliteCount.Value < 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Satellite count cannot be negative: {data.SatelliteCount.Value}");
            }
            else if (data.SatelliteCount.Value > 50)
            {
                result.IsValid = false;
                result.Errors.Add($"Satellite count {data.SatelliteCount.Value} exceeds reasonable limit (50)");
            }
            else if (data.SatelliteCount.Value < 4)
            {
                // Low satellite count warning (typically need 4+ for 3D fix)
                _logger.LogTrace("Low satellite count for device {DeviceId}: {Count}", 
                    data.DeviceId, data.SatelliteCount.Value);
            }
        }

        if (data.Hdop.HasValue)
        {
            if (data.Hdop.Value < 0)
            {
                result.IsValid = false;
                result.Errors.Add($"HDOP cannot be negative: {data.Hdop.Value}");
            }
            else if (data.Hdop.Value > 50)
            {
                result.IsValid = false;
                result.Errors.Add($"HDOP {data.Hdop.Value} exceeds reasonable limit (50)");
            }
            else if (data.Hdop.Value > 10)
            {
                // Poor accuracy warning (HDOP > 10 indicates poor accuracy)
                _logger.LogTrace("Poor GPS accuracy for device {DeviceId}: HDOP {Hdop}", 
                    data.DeviceId, data.Hdop.Value);
            }
        }
    }

    public double CalculateGpsQualityScore(GpsData data)
    {
        double score = 100.0; // Start with perfect score

        try
        {
            // Deduct points for missing data
            if (!data.SatelliteCount.HasValue) score -= 10;
            if (!data.Hdop.HasValue) score -= 10;
            if (!data.Speed.HasValue) score -= 5;
            if (!data.Heading.HasValue) score -= 5;
            if (!data.Altitude.HasValue) score -= 5;

            // Deduct points based on satellite count
            if (data.SatelliteCount.HasValue)
            {
                if (data.SatelliteCount.Value < 4) score -= 30;
                else if (data.SatelliteCount.Value < 6) score -= 15;
                else if (data.SatelliteCount.Value < 8) score -= 5;
            }

            // Deduct points based on HDOP
            if (data.Hdop.HasValue)
            {
                if (data.Hdop.Value > 10) score -= 40;
                else if (data.Hdop.Value > 5) score -= 20;
                else if (data.Hdop.Value > 2) score -= 10;
            }

            // Deduct points for timestamp issues
            var timeDiff = Math.Abs((DateTime.UtcNow - data.Timestamp).TotalMinutes);
            if (timeDiff > 60) score -= 20;
            else if (timeDiff > 10) score -= 10;

            return Math.Max(0, score);
        }
        catch
        {
            return 0;
        }
    }
}