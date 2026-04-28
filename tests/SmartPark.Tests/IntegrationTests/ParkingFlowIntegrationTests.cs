using Moq;
using SmartPark.Core.Interfaces;
using SmartPark.Core.Models;
using SmartPark.Core.Services;
using TestInMemoryParkingRepository = SmartPark.Tests.InMemoryParkingRepository;

namespace SmartPark.Tests.IntegrationTests;

public class ParkingFlowIntegrationTests
{
    private readonly ParkingFeeCalculator _feeCalculator = new();
    private readonly TestInMemoryParkingRepository _repository = new();
    private readonly Mock<IPaymentGateway> _paymentStub = new();
    private readonly Mock<INotificationService> _notificationStub = new();
    private readonly ParkingSessionManager _manager;

    private DateTime _currentTime = new(2026, 3, 16, 10, 0, 0);

    public ParkingFlowIntegrationTests()
    {
        var dateTimeStub = new Mock<IDateTimeProvider>();
        dateTimeStub.Setup(d => d.Now).Returns(() => _currentTime);

        var membershipStub = new Mock<IMembershipService>();
        membershipStub.Setup(m => m.GetMembershipTier(It.IsAny<string>())).Returns(MembershipTier.Guest);

        _paymentStub
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(true);

        _manager = new ParkingSessionManager(
            _feeCalculator,
            _paymentStub.Object,
            _notificationStub.Object,
            membershipStub.Object,
            _repository,
            dateTimeStub.Object);
    }

    #region Full Parking Flow

    [Fact]
    public async Task FullFlow_Car2Hours_CalculatesCorrectFee()
    {
        _currentTime = new DateTime(2026, 3, 16, 10, 0, 0);
        var ticket = await _manager.CheckInAsync("CAR-001", VehicleType.Car);

        _currentTime = new DateTime(2026, 3, 16, 12, 0, 0);
        var result = await _manager.CheckOutAsync(ticket.TicketId, "012-000-001");

        Assert.Equal(2_000m, result.TotalFee);
        var updated = await _repository.GetTicketByIdAsync(ticket.TicketId);
        Assert.NotNull(updated!.CheckOutTime);
    }

    [Fact]
    public async Task FullFlow_GracePeriod_2Minutes_FeeIsZero()
    {
        _currentTime = new DateTime(2026, 3, 16, 10, 0, 0);
        var ticket = await _manager.CheckInAsync("CAR-002", VehicleType.Car);

        _currentTime = new DateTime(2026, 3, 16, 10, 2, 0);
        var result = await _manager.CheckOutAsync(ticket.TicketId, "012-000-002");

        Assert.Equal(0m, result.TotalFee);
    }

    #endregion

    #region Multiple Vehicles

    [Fact]
    public async Task MultipleVehicles_CheckOut1Of3_2RemainActive()
    {
        _currentTime = new DateTime(2026, 3, 16, 10, 0, 0);
        var t1 = await _manager.CheckInAsync("CAR-A", VehicleType.Car);
        var t2 = await _manager.CheckInAsync("CAR-B", VehicleType.Car);
        var t3 = await _manager.CheckInAsync("CAR-C", VehicleType.Car);

        _currentTime = new DateTime(2026, 3, 16, 12, 0, 0);
        await _manager.CheckOutAsync(t1.TicketId, "012-A");

        var active = (await _repository.GetAllActiveTicketsAsync()).ToList();
        Assert.Equal(2, active.Count);
        Assert.Contains(active, t => t.TicketId == t2.TicketId);
        Assert.Contains(active, t => t.TicketId == t3.TicketId);
    }

    #endregion

    #region Error Recovery

    [Fact]
    public async Task ErrorRecovery_DuplicateCheckIn_OriginalTicketStillActive()
    {
        _currentTime = new DateTime(2026, 3, 16, 10, 0, 0);
        var ticket = await _manager.CheckInAsync("DUP-001", VehicleType.Car);

        var act = () => _manager.CheckInAsync("DUP-001", VehicleType.Car);
        await Assert.ThrowsAsync<InvalidOperationException>(act);

        var active = await _repository.GetActiveTicketByPlateAsync("DUP-001");
        Assert.NotNull(active);
        Assert.Equal(ticket.TicketId, active!.TicketId);
    }

    [Fact]
    public async Task ErrorRecovery_PaymentFails_TicketRemainsActive()
    {
        _currentTime = new DateTime(2026, 3, 16, 10, 0, 0);
        var ticket = await _manager.CheckInAsync("PAY-FAIL", VehicleType.Car);

        _currentTime = new DateTime(2026, 3, 16, 12, 0, 0);
        _paymentStub
            .Setup(p => p.ProcessPaymentAsync(ticket.TicketId, 2_000m))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<Exception>(
            () => _manager.CheckOutAsync(ticket.TicketId, "012-000-000"));

        var retrieved = await _repository.GetTicketByIdAsync(ticket.TicketId);
        Assert.True(retrieved!.IsActive);
    }

    #endregion

    #region Edge-to-Edge Scenarios

    [Fact]
    public async Task EdgeToEdge_LostTicket_GoldMember_PenaltyNotDiscounted()
    {
        var goldMembership = new Mock<IMembershipService>();
        goldMembership.Setup(m => m.GetMembershipTier("GOLD-001")).Returns(MembershipTier.Gold);

        var dtStub = new Mock<IDateTimeProvider>();
        var time = new DateTime(2026, 3, 16, 10, 0, 0);
        dtStub.Setup(d => d.Now).Returns(() => time);

        var localRepo = new TestInMemoryParkingRepository();
        _paymentStub.Setup(p => p.ProcessPaymentAsync(It.IsAny<string>(), 21_500m)).ReturnsAsync(true);

        var localManager = new ParkingSessionManager(
            _feeCalculator,
            _paymentStub.Object,
            _notificationStub.Object,
            goldMembership.Object,
            localRepo,
            dtStub.Object);

        var ticket = await localManager.CheckInAsync("GOLD-001", VehicleType.Car);
        time = new DateTime(2026, 3, 16, 12, 0, 0);

        var result = await localManager.CheckOutAsync(ticket.TicketId, "012-999", isLostTicket: true);

        Assert.Equal(21_500m, result.TotalFee);
        Assert.Equal(20_000m, result.LostTicketPenalty);
    }

    [Fact]
    public async Task EdgeToEdge_Overnight_Weekend_GoldMember_AllModifiersCombine()
    {
        var goldMembership = new Mock<IMembershipService>();
        goldMembership.Setup(m => m.GetMembershipTier("GOLD-SAT")).Returns(MembershipTier.Gold);

        var dtStub = new Mock<IDateTimeProvider>();
        var time = new DateTime(2026, 3, 14, 20, 0, 0);
        dtStub.Setup(d => d.Now).Returns(() => time);

        var localRepo = new TestInMemoryParkingRepository();
        _paymentStub.Setup(p => p.ProcessPaymentAsync(It.IsAny<string>(), 4_700m)).ReturnsAsync(true);

        var localManager = new ParkingSessionManager(
            _feeCalculator,
            _paymentStub.Object,
            _notificationStub.Object,
            goldMembership.Object,
            localRepo,
            dtStub.Object);

        var ticket = await localManager.CheckInAsync("GOLD-SAT", VehicleType.Car);
        time = new DateTime(2026, 3, 14, 23, 0, 0);

        var result = await localManager.CheckOutAsync(ticket.TicketId, "012-999");

        Assert.Equal(4_700m, result.TotalFee);
    }

    #endregion
}
