using SmartPark.Core.Models;
using SmartPark.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SmartPark.Tests;

public class ParkingFeeCalculatorTests
{
    private readonly ParkingFeeCalculator _calculator = new();

    // Helper: Monday 10 AM (weekday, no overnight, clean baseline)
    private static DateTime Mon(int hour = 10, int minute = 0) =>
        new(2026, 3, 16, hour, minute, 0);
    // Helper: Saturday 10 AM
    private static DateTime Sat(int hour = 10, int minute = 0) =>
        new(2026, 3, 14, hour, minute, 0); // March 14 2026 = Saturday

    [Fact]
    public void CalculateFee_GracePeriod_Exactly30Minutes_ReturnsFree()
    /*
    Logic: Short visits are free.
    Rule: If the total stay is 30 minutes or less, the fee is 0.
    */
    // use AAA pattern
    {
        // Arrange
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 30);

        // Act
        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        /*
        We use 0m (decimal) because the system handles currency,
        and the spec says the first 30 minutes are free.
        */
        Assert.Equal(0m, result.TotalFee);
    }
    [Fact]
    public void CalculateFee_Motorcycle_2Hours_Returns1000() {
        // Arrange
        var checkIn = Sat(10, 0);
        var checkOut = Sat(12, 0);
        // Act
        var result = _calculator.CalculateFee(VehicleType.Motorcycle, MembershipTier.Guest, checkIn, checkOut);
        // Assert
        Assert.Equal(1000m, result.TotalFee); // 500 * 2 = 1000
        
    }
}
