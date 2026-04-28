using SmartPark.Core.Models;
using SmartPark.Core.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SmartPark.Tests;

public class ParkingFeeCalculatorTests
{
    private readonly ParkingFeeCalculator _calculator = new();

    private static DateTime Mon(int hour = 10, int minute = 0) =>
        new(2026, 3, 16, hour, minute, 0); // Monday

    private static DateTime Sat(int hour = 10, int minute = 0) =>
        new(2026, 3, 14, hour, minute, 0); // Saturday

    // ------------------------------------------------------------
    #region Grace Period
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_GracePeriod_ZeroDuration_ReturnsFree()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(0m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_GracePeriod_29Minutes_ReturnsFree()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 29);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(0m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_GracePeriod_Exactly30Minutes_ReturnsFree()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 30);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(0m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_GracePeriod_31Minutes_BillsOneHour()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 31);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(1_000m, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Basic Fee Calculation
    // ------------------------------------------------------------

    [Theory]
    [InlineData(VehicleType.Motorcycle, 2, 1_000)]
    [InlineData(VehicleType.Car, 3, 3_000)]
    [InlineData(VehicleType.SUV, 1, 1_500)]
    [InlineData(VehicleType.Car, 2, 2_000)]
    public void CalculateFee_BasicHourlyRate_ReturnsCorrectFee(
        VehicleType vehicle, int hours, decimal expectedFee)
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10 + hours, 0);

        var result = _calculator.CalculateFee(vehicle, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(expectedFee, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Duration Rounding
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_DurationRounding_1h1minPastGrace_Bills2Hours()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(11, 31);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(2_000m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_DurationRounding_ExactHour_Bills1Hour()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(11, 30);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(1_000m, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Daily Cap
    // ------------------------------------------------------------

    [Theory]
    [InlineData(VehicleType.Motorcycle, 10, 4_000)]
    [InlineData(VehicleType.Car, 12, 8_000)]
    [InlineData(VehicleType.SUV, 24, 12_000)]
    public void CalculateFee_DailyCap_LongStay_CapsAtMaximum(
        VehicleType vehicle, int hours, decimal expectedBaseFee)
    {
        var checkIn = new DateTime(2026, 3, 16, 8, 0, 0);
        var checkOut = checkIn.AddHours(hours);

        var result = _calculator.CalculateFee(vehicle, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(expectedBaseFee, result.BaseFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Overnight Fee
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_Overnight_SpansPast10PM_AddsOvernightFee()
    {
        var checkIn = Mon(20, 0);
        var checkOut = Mon(23, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(5_000m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_NoOvernight_DaytimeOnly_NoOvernightFee()
    {
        var checkIn = Mon(8, 0);
        var checkOut = Mon(17, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(8_000m, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Weekend Surcharge
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_Weekend_Saturday_Adds20PercentSurcharge()
    {
        var checkIn = Sat(10, 0);
        var checkOut = Sat(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(2_400m, result.TotalFee);
        Assert.Equal(400m, result.SurchargeAmount);
    }

    [Fact]
    public void CalculateFee_Weekday_Monday_NoSurcharge()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        Assert.Equal(0m, result.SurchargeAmount);
    }

    #endregion

    // ------------------------------------------------------------
    #region Holiday Surcharge
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_Holiday_Adds50PercentSurcharge()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut, isHoliday: true);

        Assert.Equal(3_000m, result.TotalFee);
        Assert.Equal(1_000m, result.SurchargeAmount);
    }

    [Fact]
    public void CalculateFee_Holiday_OnSaturday_UsesHolidayOnly_NoStacking()
    {
        var checkIn = Sat(10, 0);
        var checkOut = Sat(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut, isHoliday: true);

        Assert.Equal(3_000m, result.TotalFee);
        Assert.Equal(1_000m, result.SurchargeAmount);
    }

    #endregion

    // ------------------------------------------------------------
    #region Membership Discounts
    // ------------------------------------------------------------

    [Theory]
    [InlineData(MembershipTier.Guest, 2_000)]
    [InlineData(MembershipTier.Silver, 1_800)]
    [InlineData(MembershipTier.Gold, 1_500)]
    [InlineData(MembershipTier.Platinum, 1_200)]
    public void CalculateFee_MembershipDiscount_Car2Hours_CorrectTotal(
        MembershipTier membership, decimal expectedFee)
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, membership, checkIn, checkOut);

        Assert.Equal(expectedFee, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_PlatinumMember_DiscountAppliesToBaseAndSurcharge()
    {
        var checkIn = Sat(10, 0);
        var checkOut = Sat(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Platinum, checkIn, checkOut);

        Assert.Equal(1_440m, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Lost Ticket
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_LostTicket_Guest_AddsFullPenalty()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut, isLostTicket: true);

        Assert.Equal(22_000m, result.TotalFee);
        Assert.Equal(20_000m, result.LostTicketPenalty);
    }

    [Fact]
    public void CalculateFee_LostTicket_DuringGracePeriod_ReturnsPenaltyOnly()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(10, 15);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut, isLostTicket: true);

        Assert.Equal(20_000m, result.TotalFee);
    }

    [Fact]
    public void CalculateFee_LostTicket_GoldMember_PenaltyNotDiscounted()
    {
        var checkIn = Mon(10, 0);
        var checkOut = Mon(12, 0);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Gold, checkIn, checkOut, isLostTicket: true);

        Assert.Equal(21_500m, result.TotalFee);
    }

    #endregion

    // ------------------------------------------------------------
    #region Edge Cases
    // ------------------------------------------------------------

    [Fact]
    public void CalculateFee_InvalidDates_CheckOutBeforeCheckIn_ThrowsArgumentException()
    {
        var checkIn = Mon(12, 0);
        var checkOut = Mon(10, 0);

        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut));
    }

    #endregion

    // ------------------------------------------------------------
    #region Property-Based Tests (FsCheck)
    // ------------------------------------------------------------

    private static Arbitrary<(DateTime checkIn, DateTime checkOut)> ValidDateTimePairs()
    {
        var gen =
            from hours in Gen.Choose(0, 48)
            from minutes in Gen.Choose(1, 59)
            from offset in Gen.Choose(0, 365 * 2)
            let checkIn = new DateTime(2024, 1, 1).AddDays(offset)
            let checkOut = checkIn.AddHours(hours).AddMinutes(minutes)
            select (checkIn, checkOut);
        return gen.ToArbitrary();
    }

    [Property]
    public Property FeeIsNeverNegative()
    {
        return Prop.ForAll(
            ValidDateTimePairs(),
            Arb.From<VehicleType>(),
            Arb.From<MembershipTier>(),
            (pair, vehicle, membership) =>
            {
                var result = _calculator.CalculateFee(vehicle, membership, pair.checkIn, pair.checkOut);
                return result.TotalFee >= 0m;
            });
    }

    [Property]
    public Property GracePeriodIsAlwaysFree()
    {
        var shortStay =
            from minutes in Gen.Choose(0, 30)
            from offset in Gen.Choose(0, 365 * 2)
            let checkIn = new DateTime(2024, 1, 1).AddDays(offset)
            let checkOut = checkIn.AddMinutes(minutes)
            select (checkIn, checkOut);

        return Prop.ForAll(
            shortStay.ToArbitrary(),
            Arb.From<VehicleType>(),
            Arb.From<MembershipTier>(),
            (pair, vehicle, membership) =>
            {
                var result = _calculator.CalculateFee(vehicle, membership, pair.checkIn, pair.checkOut);
                return result.BaseFee == 0m;
            });
    }

    [Property]
    public Property MemberAlwaysPaysLessOrEqualToGuest()
    {
        return Prop.ForAll(
            ValidDateTimePairs(),
            Arb.From<VehicleType>(),
            Arb.From<MembershipTier>(),
            (pair, vehicle, membership) =>
            {
                var guest = _calculator.CalculateFee(vehicle, MembershipTier.Guest, pair.checkIn, pair.checkOut);
                var member = _calculator.CalculateFee(vehicle, membership, pair.checkIn, pair.checkOut);
                return member.TotalFee <= guest.TotalFee;
            });
    }

    [Property]
    public Property LostTicketAlwaysAddsExactly20000()
    {
        return Prop.ForAll(
            ValidDateTimePairs(),
            Arb.From<VehicleType>(),
            Arb.From<MembershipTier>(),
            (pair, vehicle, membership) =>
            {
                var without = _calculator.CalculateFee(vehicle, membership, pair.checkIn, pair.checkOut, isLostTicket: false);
                var with = _calculator.CalculateFee(vehicle, membership, pair.checkIn, pair.checkOut, isLostTicket: true);
                return with.TotalFee - without.TotalFee == 20_000m;
            });
    }

    [Property]
    public Property BaseFeeNeverExceedsDailyCap()
    {
        return Prop.ForAll(
            ValidDateTimePairs(),
            Arb.From<VehicleType>(),
            (pair, vehicle) =>
            {
                var result = _calculator.CalculateFee(vehicle, MembershipTier.Guest, pair.checkIn, pair.checkOut);
                decimal cap = vehicle switch
                {
                    VehicleType.Motorcycle => 4_000m,
                    VehicleType.Car => 8_000m,
                    VehicleType.SUV => 12_000m,
                    _ => decimal.MaxValue
                };
                return result.BaseFee <= cap;
            });
    }

    [Property]
    public Property HigherTierMeansHigherOrEqualDiscount()
    {
        return Prop.ForAll(
            ValidDateTimePairs(),
            Arb.From<VehicleType>(),
            (pair, vehicle) =>
            {
                var silver = _calculator.CalculateFee(vehicle, MembershipTier.Silver, pair.checkIn, pair.checkOut);
                var gold = _calculator.CalculateFee(vehicle, MembershipTier.Gold, pair.checkIn, pair.checkOut);
                var platinum = _calculator.CalculateFee(vehicle, MembershipTier.Platinum, pair.checkIn, pair.checkOut);
                return silver.DiscountAmount <= gold.DiscountAmount
                    && gold.DiscountAmount <= platinum.DiscountAmount;
            });
    }

    #endregion
}
