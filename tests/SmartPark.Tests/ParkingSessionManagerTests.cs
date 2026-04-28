using Moq;
using SmartPark.Core.Interfaces;
using SmartPark.Core.Models;
using SmartPark.Core.Services;

namespace SmartPark.Tests;

public class ParkingSessionManagerTests
{
    private readonly Mock<IPaymentGateway> _paymentStub = new();
    private readonly Mock<INotificationService> _notificationStub = new();
    private readonly Mock<IMembershipService> _membershipStub = new();
    private readonly Mock<IParkingRepository> _repoStub = new();
    private readonly Mock<IDateTimeProvider> _dateTimeStub = new();
    private readonly ParkingFeeCalculator _feeCalculator = new();
    private readonly ParkingSessionManager _manager;

    private static readonly DateTime Monday10Am = new(2026, 3, 16, 10, 0, 0);
    private static readonly DateTime Monday12Pm = new(2026, 3, 16, 12, 0, 0);

    public ParkingSessionManagerTests()
    {
        _manager = new ParkingSessionManager(
            _feeCalculator,
            _paymentStub.Object,
            _notificationStub.Object,
            _membershipStub.Object,
            _repoStub.Object,
            _dateTimeStub.Object);
    }

    #region CheckIn Happy Path

    [Fact]
    public async Task CheckInAsync_NewVehicle_LooksUpMembershipAndSavesTicket()
    {
        _membershipStub.Setup(m => m.GetMembershipTier("PP-1111")).Returns(MembershipTier.Gold);
        _repoStub.Setup(r => r.GetActiveTicketByPlateAsync("PP-1111")).ReturnsAsync((ParkingTicket?)null);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday10Am);

        var ticket = await _manager.CheckInAsync("PP-1111", VehicleType.Car);

        Assert.Equal("PP-1111", ticket.Vehicle.LicensePlate);
        Assert.Equal(VehicleType.Car, ticket.Vehicle.Type);
        Assert.Equal(MembershipTier.Gold, ticket.Vehicle.Membership);
        Assert.Equal(Monday10Am, ticket.CheckInTime);
        _membershipStub.Verify(m => m.GetMembershipTier("PP-1111"), Times.Once);
        _repoStub.Verify(r => r.SaveTicketAsync(It.IsAny<ParkingTicket>()), Times.Once);
    }

    #endregion

    #region CheckIn Validation

    [Fact]
    public async Task CheckInAsync_DuplicatePlate_ThrowsAndDoesNotSave()
    {
        var existing = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-2222", Type = VehicleType.Car },
            CheckInTime = Monday10Am
        };
        _membershipStub.Setup(m => m.GetMembershipTier("PP-2222")).Returns(MembershipTier.Guest);
        _repoStub.Setup(r => r.GetActiveTicketByPlateAsync("PP-2222")).ReturnsAsync(existing);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday10Am);

        var act = () => _manager.CheckInAsync("PP-2222", VehicleType.Car);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
        _repoStub.Verify(r => r.SaveTicketAsync(It.IsAny<ParkingTicket>()), Times.Never);
    }

    #endregion

    #region CheckOut Happy Path

    [Fact]
    public async Task CheckOutAsync_HappyPath_PaymentAndReceiptAndUpdate()
    {
        var ticket = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-3333", Type = VehicleType.Car, Membership = MembershipTier.Guest },
            CheckInTime = Monday10Am
        };
        _repoStub.Setup(r => r.GetTicketByIdAsync(ticket.TicketId)).ReturnsAsync(ticket);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday12Pm);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticket.TicketId, 2_000m)).ReturnsAsync(true);

        var result = await _manager.CheckOutAsync(ticket.TicketId, "012-000-001");

        Assert.Equal(2_000m, result.TotalFee);
        _paymentStub.Verify(p => p.ProcessPaymentAsync(ticket.TicketId, 2_000m), Times.Once);
        _repoStub.Verify(r => r.UpdateTicketAsync(It.IsAny<ParkingTicket>()), Times.Once);
        _notificationStub.Verify(n => n.SendReceiptAsync("012-000-001", It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region CheckOut Payment Failure

    [Fact]
    public async Task CheckOutAsync_PaymentFails_ThrowsAndTicketNotUpdated()
    {
        var ticket = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-4444", Type = VehicleType.Car, Membership = MembershipTier.Guest },
            CheckInTime = Monday10Am
        };
        _repoStub.Setup(r => r.GetTicketByIdAsync(ticket.TicketId)).ReturnsAsync(ticket);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday12Pm);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticket.TicketId, 2_000m)).ReturnsAsync(false);

        var act = () => _manager.CheckOutAsync(ticket.TicketId, "012-000-002");

        await Assert.ThrowsAsync<Exception>(act);
        _repoStub.Verify(r => r.UpdateTicketAsync(It.IsAny<ParkingTicket>()), Times.Never);
        _notificationStub.Verify(n => n.SendReceiptAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region CheckOut Notification Failure

    [Fact]
    public async Task CheckOutAsync_NotificationFails_CheckoutStillSucceeds()
    {
        var ticket = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-5555", Type = VehicleType.Car, Membership = MembershipTier.Guest },
            CheckInTime = Monday10Am
        };
        _repoStub.Setup(r => r.GetTicketByIdAsync(ticket.TicketId)).ReturnsAsync(ticket);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday12Pm);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticket.TicketId, 2_000m)).ReturnsAsync(true);
        _notificationStub.Setup(n => n.SendReceiptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMS down"));

        var result = await _manager.CheckOutAsync(ticket.TicketId, "012-000-003");

        Assert.Equal(2_000m, result.TotalFee);
        _repoStub.Verify(r => r.UpdateTicketAsync(It.IsAny<ParkingTicket>()), Times.Once);
    }

    #endregion

    #region CheckOut Validation

    [Fact]
    public async Task CheckOutAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        _repoStub.Setup(r => r.GetTicketByIdAsync("FAKE")).ReturnsAsync((ParkingTicket?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _manager.CheckOutAsync("FAKE", "012-000-000"));
    }

    [Fact]
    public async Task CheckOutAsync_AlreadyCheckedOut_ThrowsInvalidOperationException()
    {
        var ticket = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-6666", Type = VehicleType.Car },
            CheckInTime = Monday10Am,
            CheckOutTime = Monday12Pm
        };
        _repoStub.Setup(r => r.GetTicketByIdAsync(ticket.TicketId)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _manager.CheckOutAsync(ticket.TicketId, "012-000-000"));
    }

    #endregion

    #region Verify Interaction Order

    [Fact]
    public async Task CheckOutAsync_InteractionOrder_PaymentThenUpdateThenReceipt()
    {
        var callOrder = new List<string>();

        var ticket = new ParkingTicket
        {
            Vehicle = new Vehicle { LicensePlate = "PP-7777", Type = VehicleType.Car, Membership = MembershipTier.Guest },
            CheckInTime = Monday10Am
        };
        _repoStub.Setup(r => r.GetTicketByIdAsync(ticket.TicketId)).ReturnsAsync(ticket);
        _dateTimeStub.Setup(d => d.Now).Returns(Monday12Pm);

        _paymentStub
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(true)
            .Callback(() => callOrder.Add("payment"));

        _repoStub
            .Setup(r => r.UpdateTicketAsync(It.IsAny<ParkingTicket>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("update"));

        _notificationStub
            .Setup(n => n.SendReceiptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("receipt"));

        await _manager.CheckOutAsync(ticket.TicketId, "012-000-000");

        Assert.Equal(new[] { "payment", "update", "receipt" }, callOrder);
    }

    #endregion
}
