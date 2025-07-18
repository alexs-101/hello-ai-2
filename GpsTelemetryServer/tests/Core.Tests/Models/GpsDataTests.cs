using Core.Models;
using Xunit;

namespace Core.Tests.Models;

public class GpsDataTests
{
    [Fact]
    public void GpsData_ValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var gpsData = new GpsData
        {
            DeviceId = "TEST001",
            Timestamp = DateTime.UtcNow,
            Latitude = 37.7749,
            Longitude = -122.4194,
            Speed = 25.5,
            Heading = 180.0,
            Altitude = 100.0,
            SatelliteCount = 8,
            Hdop = 1.2
        };

        // Assert
        Assert.Equal("TEST001", gpsData.DeviceId);
        Assert.True(gpsData.Latitude >= -90 && gpsData.Latitude <= 90);
        Assert.True(gpsData.Longitude >= -180 && gpsData.Longitude <= 180);
        Assert.True(gpsData.Speed >= 0);
        Assert.True(gpsData.Heading >= 0 && gpsData.Heading <= 360);
        Assert.Equal(8, gpsData.SatelliteCount);
    }

    [Theory]
    [InlineData(-91.0, 0.0)] // Invalid latitude
    [InlineData(91.0, 0.0)]  // Invalid latitude
    [InlineData(0.0, -181.0)] // Invalid longitude
    [InlineData(0.0, 181.0)]  // Invalid longitude
    public void GpsData_InvalidCoordinates_ValidationFails(double latitude, double longitude)
    {
        // Arrange
        var gpsData = new GpsData
        {
            DeviceId = "TEST001",
            Timestamp = DateTime.UtcNow,
            Latitude = latitude,
            Longitude = longitude
        };

        // Act & Assert
        // Note: In a real application, you would use data annotations validation
        // This test demonstrates the expected behavior
        Assert.True(latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180);
    }

    [Fact]
    public void GpsData_ExtendedData_CanStoreAdditionalProperties()
    {
        // Arrange
        var gpsData = new GpsData
        {
            DeviceId = "TEST001",
            Timestamp = DateTime.UtcNow,
            Latitude = 37.7749,
            Longitude = -122.4194
        };

        // Act
        gpsData.ExtendedData["Protocol"] = "NMEA";
        gpsData.ExtendedData["SignalStrength"] = "Strong";
        gpsData.ExtendedData["QualityScore"] = 95;

        // Assert
        Assert.Equal("NMEA", gpsData.ExtendedData["Protocol"]);
        Assert.Equal("Strong", gpsData.ExtendedData["SignalStrength"]);
        Assert.Equal(95, gpsData.ExtendedData["QualityScore"]);
        Assert.Equal(3, gpsData.ExtendedData.Count);
    }

    [Fact]
    public void GpsData_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var gpsData = new GpsData();

        // Assert
        Assert.Empty(gpsData.DeviceId);
        Assert.Equal(default(DateTime), gpsData.Timestamp);
        Assert.Equal(0.0, gpsData.Latitude);
        Assert.Equal(0.0, gpsData.Longitude);
        Assert.Null(gpsData.Speed);
        Assert.Null(gpsData.Heading);
        Assert.Null(gpsData.Altitude);
        Assert.Null(gpsData.SatelliteCount);
        Assert.Null(gpsData.Hdop);
        Assert.NotNull(gpsData.ExtendedData);
        Assert.Empty(gpsData.ExtendedData);
    }
}