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
}
