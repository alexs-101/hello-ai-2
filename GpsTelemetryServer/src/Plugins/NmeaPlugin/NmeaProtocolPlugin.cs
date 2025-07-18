using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using CoreValidationResult = Core.Models.ValidationResult;

namespace NmeaPlugin;

public class NmeaProtocolPlugin : IProtocolPlugin
{
    private readonly ILogger<NmeaProtocolPlugin>? _logger;
    private bool _initialized = false;
    private readonly object _lock = new();

    public string Name => "NMEA 0183 Protocol Plugin";
    public string Version => "1.0.0";
    public ProtocolType SupportedProtocol => ProtocolType.Nmea;

    public NmeaProtocolPlugin()
    {
    }

    public NmeaProtocolPlugin(ILogger<NmeaProtocolPlugin> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(IConfiguration config)
    {
        lock (_lock)
        {
            if (_initialized)
                return Task.CompletedTask;

            _initialized = true;
            _logger?.LogInformation("NMEA Protocol Plugin initialized");
            return Task.CompletedTask;
        }
    }

    public Task<bool> CanHandleAsync(byte[] data)
    {
        if (data == null || data.Length == 0)
            return Task.FromResult(false);

        try
        {
            var text = Encoding.ASCII.GetString(data).Trim();
            
            // NMEA sentences start with $ and contain comma-separated fields
            var isNmea = text.StartsWith("$") && text.Contains(",");
            
            return Task.FromResult(isNmea);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<GpsData> ProcessAsync(byte[] data, string deviceId)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        var text = Encoding.ASCII.GetString(data).Trim();
        var gpsData = new GpsData { DeviceId = deviceId, Timestamp = DateTime.UtcNow };

        try
        {
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("$"))
                    continue;

                try
                {
                    await ParseNmeaSentence(line, gpsData);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse NMEA sentence: {Sentence}", line);
                }
            }

            return gpsData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing NMEA data for device {DeviceId}", deviceId);
            throw;
        }
    }

    private async Task ParseNmeaSentence(string sentence, GpsData gpsData)
    {
        await Task.CompletedTask;

        if (!IsValidChecksum(sentence))
        {
            _logger?.LogWarning("Invalid NMEA checksum: {Sentence}", sentence);
            return;
        }

        var parts = sentence.Split(',');
        if (parts.Length < 2)
            return;

        var messageType = parts[0].Substring(1); // Remove $ prefix

        switch (messageType)
        {
            case "GPRMC":
                ParseGprmc(parts, gpsData);
                break;
            case "GPGGA":
                ParseGpgga(parts, gpsData);
                break;
            case "GPGSV":
                ParseGpgsv(parts, gpsData);
                break;
            case "GPGSA":
                ParseGpgsa(parts, gpsData);
                break;
            default:
                gpsData.ExtendedData[$"Unknown_{messageType}"] = sentence;
                break;
        }
    }

    private void ParseGprmc(string[] parts, GpsData gpsData)
    {
        // $GPRMC,time,status,lat,lat_dir,lon,lon_dir,speed,course,date,mag_var,checksum
        if (parts.Length < 12 || parts[2] != "A") // A = Active, V = Invalid
            return;

        try
        {
            if (TryParseCoordinate(parts[3], parts[4], out double lat))
                gpsData.Latitude = lat;
            
            if (TryParseCoordinate(parts[5], parts[6], out double lon))
                gpsData.Longitude = lon;
            
            if (double.TryParse(parts[7], out double speed))
                gpsData.Speed = speed * 1.852; // Convert knots to km/h
            
            if (double.TryParse(parts[8], out double heading))
                gpsData.Heading = heading;

            if (TryParseTime(parts[1], parts[9], out DateTime timestamp))
                gpsData.Timestamp = timestamp;

            gpsData.ExtendedData["MessageType"] = "GPRMC";
            gpsData.ExtendedData["Status"] = parts[2];
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing GPRMC sentence");
        }
    }

    private void ParseGpgga(string[] parts, GpsData gpsData)
    {
        // $GPGGA,time,lat,lat_dir,lon,lon_dir,quality,num_sats,hdop,altitude,alt_unit,geoid_height,checksum
        if (parts.Length < 13 || parts[6] == "0") // Quality 0 = Invalid
            return;

        try
        {
            if (TryParseCoordinate(parts[2], parts[3], out double lat))
                gpsData.Latitude = lat;
            
            if (TryParseCoordinate(parts[4], parts[5], out double lon))
                gpsData.Longitude = lon;
            
            if (int.TryParse(parts[7], out int satCount))
                gpsData.SatelliteCount = satCount;
            
            if (double.TryParse(parts[8], out double hdop))
                gpsData.Hdop = hdop;
            
            if (double.TryParse(parts[9], out double altitude))
                gpsData.Altitude = altitude;

            gpsData.ExtendedData["MessageType"] = "GPGGA";
            gpsData.ExtendedData["Quality"] = parts[6];
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing GPGGA sentence");
        }
    }

    private void ParseGpgsv(string[] parts, GpsData gpsData)
    {
        // $GPGSV,total_msgs,msg_num,sats_in_view,sat1_prn,sat1_elev,sat1_azim,sat1_snr,...
        if (parts.Length < 4)
            return;

        try
        {
            gpsData.ExtendedData["MessageType"] = "GPGSV";
            if (int.TryParse(parts[3], out int satsInView))
                gpsData.ExtendedData["SatellitesInView"] = satsInView;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing GPGSV sentence");
        }
    }

    private void ParseGpgsa(string[] parts, GpsData gpsData)
    {
        // $GPGSA,mode,fix_type,sat1,sat2,...,pdop,hdop,vdop,checksum
        if (parts.Length < 17)
            return;

        try
        {
            gpsData.ExtendedData["MessageType"] = "GPGSA";
            gpsData.ExtendedData["Mode"] = parts[1];
            gpsData.ExtendedData["FixType"] = parts[2];
            
            if (double.TryParse(parts[15], out double hdop))
                gpsData.Hdop = hdop;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error parsing GPGSA sentence");
        }
    }

    private bool TryParseCoordinate(string coordinate, string direction, out double result)
    {
        result = 0;
        
        if (string.IsNullOrEmpty(coordinate) || string.IsNullOrEmpty(direction))
            return false;

        if (!double.TryParse(coordinate, out double value))
            return false;

        // Convert DDMM.MMMM format to decimal degrees
        int degrees = (int)(value / 100);
        double minutes = value - (degrees * 100);
        result = degrees + (minutes / 60.0);

        // Apply direction
        if (direction == "S" || direction == "W")
            result = -result;

        return true;
    }

    private bool TryParseTime(string timeStr, string dateStr, out DateTime timestamp)
    {
        timestamp = DateTime.UtcNow;
        
        if (string.IsNullOrEmpty(timeStr) || string.IsNullOrEmpty(dateStr))
            return false;

        try
        {
            // Parse HHMMSS.SSS format
            if (timeStr.Length < 6)
                return false;
            
            var hours = int.Parse(timeStr.Substring(0, 2));
            var minutes = int.Parse(timeStr.Substring(2, 2));
            var seconds = int.Parse(timeStr.Substring(4, 2));
            var milliseconds = 0;
            
            if (timeStr.Length > 7 && timeStr[6] == '.')
            {
                var fractional = timeStr.Substring(7);
                if (fractional.Length >= 3)
                    milliseconds = int.Parse(fractional.Substring(0, 3));
            }

            // Parse DDMMYY format
            if (dateStr.Length < 6)
                return false;
            
            var day = int.Parse(dateStr.Substring(0, 2));
            var month = int.Parse(dateStr.Substring(2, 2));
            var year = int.Parse(dateStr.Substring(4, 2)) + 2000; // Assume 20xx

            timestamp = new DateTime(year, month, day, hours, minutes, seconds, milliseconds, DateTimeKind.Utc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidChecksum(string sentence)
    {
        if (string.IsNullOrEmpty(sentence) || !sentence.Contains("*"))
            return false;

        try
        {
            var parts = sentence.Split('*');
            if (parts.Length != 2)
                return false;

            var data = parts[0].Substring(1); // Remove $ prefix
            var checksumStr = parts[1].Substring(0, 2); // Take first 2 chars after *
            
            if (!byte.TryParse(checksumStr, System.Globalization.NumberStyles.HexNumber, null, out byte expectedChecksum))
                return false;

            byte calculatedChecksum = 0;
            foreach (char c in data)
            {
                calculatedChecksum ^= (byte)c;
            }

            return calculatedChecksum == expectedChecksum;
        }
        catch
        {
            return false;
        }
    }

    public Task<CoreValidationResult> ValidateAsync(GpsData data)
    {
        var result = new CoreValidationResult { IsValid = true };
        var context = new ValidationContext(data);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        if (!Validator.TryValidateObject(data, context, validationResults, true))
        {
            result.IsValid = false;
            result.Errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional NMEA-specific validation
        if (data.Latitude == 0 && data.Longitude == 0)
        {
            result.IsValid = false;
            result.Errors.Add("GPS coordinates cannot both be zero (invalid fix)");
        }

        if (data.Timestamp < DateTime.UtcNow.AddDays(-1) || data.Timestamp > DateTime.UtcNow.AddHours(1))
        {
            result.IsValid = false;
            result.Errors.Add("GPS timestamp is outside acceptable range");
        }

        return Task.FromResult(result);
    }

    public Task CleanupAsync()
    {
        lock (_lock)
        {
            _initialized = false;
            _logger?.LogInformation("NMEA Protocol Plugin cleaned up");
            return Task.CompletedTask;
        }
    }
}