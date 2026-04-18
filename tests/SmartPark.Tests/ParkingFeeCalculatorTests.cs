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
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);
        // Act
        var result = _calculator.CalculateFee(VehicleType.Motorcycle, MembershipTier.Guest, checkIn, checkOut);
        // Assert
        Assert.Equal(1000m, result.TotalFee); // 500 * 2 = 1000
        
    }
    [Fact]
    public void CalculateFee_MotorcycleDailyCap_10Hours_Returns4000(){
         // Arrange — 10 hours: 500 × 10 = 5,000, but capped at 4,000
        var checkIn = Mon(8 , 0);
        var checkOut = Mon(18 , 0);

        //act
        var result = _calculator.CalculateFee(VehicleType.Motorcycle, MembershipTier.Guest , checkIn , checkOut);

        Assert.Equal(4000m , result.TotalFee);
    }
    [Fact]
    public void CalculateFee_Weekend_Saturday_Adds20PercentSurcharge()
    {
        // Car, Saturday, 2h → base=2,000; +20% surcharge=400 → total=2,400
        var checkIn  = Sat(10, 0);
        var checkOut = Sat(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(2_400m, result.TotalFee);
        Assert.Equal(400m,   result.SurchargeAmount);
    }
    [Fact]
    public void CalculateFee_GoldMember_Car2Hours_Returns1500()
    {
        // base=2,000; Gold 25% → discount=500; total=1,500
        var checkIn  = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Gold, checkIn, checkOut);

        Assert.Equal(1_500m, result.TotalFee);
    }
    [Fact]
    public void CalculateFee_LostTicket_AddsExact20000Penalty()
    {
        // Car, 2h, lost → 2,000 fee + 20,000 penalty = 22,000
        var checkIn  = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(
            VehicleType.Car, MembershipTier.Guest, checkIn, checkOut, isLostTicket: true);

        Assert.Equal(22_000m, result.TotalFee);
        Assert.Equal(20_000m, result.LostTicketPenalty);
    }
}
