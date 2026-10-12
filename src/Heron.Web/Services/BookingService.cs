using Heron.Web.Data;
using Heron.Web.Models;
using Heron.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Services;

public class BookingService
{
    private readonly AppDbContext _db;

    public BookingService(AppDbContext db) => _db = db;

    // "Now" in the venue's local time (see the note on Booking.StartAt).
    private static DateTime Now => DateTime.Now;

    // ----- searching -----

    public async Task<List<AvailableRoom>> FindAvailableAsync(DateTime start, DateTime end, int attendees)
    {
        // the procedure does the real work: opening hours, capacity, and "is anything booked then"
        return await _db.Database
            .SqlQuery<AvailableRoom>($"EXEC dbo.usp_FindAvailableRooms {start}, {end}, {attendees}")
            .ToListAsync();
    }

    // ----- booking -----

    public async Task<(bool Ok, string Message, int? BookingId)> CreateAsync(
        int roomId, string userId, string title, DateTime start, DateTime end, int attendees)
    {
        // quick checks first, so the user gets a clear message
        var problem = BookingRules.Validate(start, end, attendees, Now);
        if (problem != null) return (false, problem, null);

        // the procedure checks everything again and saves the booking in one transaction
        var rows = await _db.Database
            .SqlQuery<CreateBookingResult>($"EXEC dbo.usp_CreateBooking {roomId}, {userId}, {title.Trim()}, {start}, {end}, {attendees}")
            .ToListAsync();

        var result = rows.Single();
        return (result.Result == 0, BookingRules.Message(result.Result), result.BookingId);
    }

    // ----- cancelling -----

    public async Task<string?> CancelAsync(int bookingId, string userId, bool isAdmin)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);

        // somebody else's booking looks the same as one that doesn't exist
        if (booking == null || (!isAdmin && booking.UserId != userId)) return "Booking not found.";
        if (booking.Status == BookingStatus.Cancelled) return "That booking is already cancelled.";
        if (!isAdmin && !BookingRules.CanCancel(booking.StartAt, Now))
            return $"Bookings can only be cancelled up to {BookingRules.CancelUntilHoursBefore} hours before they start.";

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return null;
    }

    // ----- lists -----

    public Task<List<BookingDetail>> ForUserAsync(string userId) =>
        _db.Database.SqlQuery<BookingDetail>($"SELECT * FROM dbo.vw_BookingDetails WHERE UserId = {userId}")
            .OrderByDescending(b => b.StartAt)
            .ToListAsync();

    public Task<List<BookingDetail>> ForDayAsync(DateTime day)
    {
        var from = day.Date;
        var to = from.AddDays(1);
        return _db.Database
            .SqlQuery<BookingDetail>($"SELECT * FROM dbo.vw_BookingDetails WHERE StartAt >= {from} AND StartAt < {to}")
            .OrderBy(b => b.StartAt)
            .ToListAsync();
    }

    // ----- timeline for one room on one day (no names, other customers' details stay private) -----

    public async Task<List<(DateTime Start, DateTime End)>> BusyAsync(int roomId, DateTime day)
    {
        var from = day.Date;
        var to = from.AddDays(1);
        var rows = await _db.Bookings.AsNoTracking()
            .Where(b => b.RoomId == roomId && b.Status == BookingStatus.Confirmed && b.StartAt >= from && b.StartAt < to)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.StartAt, b.EndAt })
            .ToListAsync();
        return rows.Select(r => (r.StartAt, r.EndAt)).ToList();
    }

    // ----- reports (admin) -----

    public async Task<AdminDashboard> DashboardAsync(DateTime from, DateTime to)
    {
        var fromDay = from.Date;
        var toDay = to.Date;

        // the three reports are separate queries on the same DbContext, so they run one after another
        var revenue = await _db.Database.SqlQuery<RevenueRow>($"EXEC dbo.usp_RevenueReport {fromDay}, {toDay}").ToListAsync();
        var peak = await _db.Database.SqlQuery<PeakHourRow>($"EXEC dbo.usp_PeakHours {fromDay}, {toDay}").ToListAsync();
        var utilisation = await _db.Database
            .SqlQuery<RoomUtilisationRow>($"SELECT * FROM dbo.vw_RoomUtilisation")
            .OrderByDescending(r => r.UtilisationPercent)
            .ToListAsync();

        var today = Now.Date;
        var tomorrow = today.AddDays(1);
        var upcomingToday = await _db.Bookings.CountAsync(b =>
            b.Status == BookingStatus.Confirmed && b.StartAt >= today && b.StartAt < tomorrow);

        return new AdminDashboard(fromDay, toDay, revenue, peak, utilisation, upcomingToday);
    }
}
