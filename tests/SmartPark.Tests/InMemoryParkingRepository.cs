using SmartPark.Core.Interfaces;
using SmartPark.Core.Models;

namespace SmartPark.Tests;

public class InMemoryParkingRepository : IParkingRepository
{
    private readonly Dictionary<string, ParkingTicket> _store = new();

    public Task SaveTicketAsync(ParkingTicket ticket)
    {
        _store[ticket.TicketId] = ticket;
        return Task.CompletedTask;
    }

    public Task<ParkingTicket?> GetTicketByIdAsync(string ticketId)
    {
        _store.TryGetValue(ticketId, out var ticket);
        return Task.FromResult(ticket);
    }

    public Task<ParkingTicket?> GetActiveTicketByPlateAsync(string licensePlate)
    {
        var ticket = _store.Values
            .FirstOrDefault(t => t.Vehicle.LicensePlate == licensePlate && t.IsActive);
        return Task.FromResult(ticket);
    }

    public Task UpdateTicketAsync(ParkingTicket ticket)
    {
        _store[ticket.TicketId] = ticket;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ParkingTicket>> GetAllActiveTicketsAsync()
    {
        var active = _store.Values.Where(t => t.IsActive);
        return Task.FromResult(active);
    }
}
