using System.Security.Claims;
using Heron.Web.Data;
using Heron.Web.Models;
using Heron.Web.Services;
using Heron.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public BookingsController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Step 2 of booking: the user has picked a room and a time, now they confirm it.
    [HttpGet]
    public async Task<IActionResult> Create(int roomId, DateTime start, DateTime end, int people)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive);
        if (room == null) return NotFound();

        var problem = BookingRules.Validate(start, end, people, DateTime.Now);
        if (problem != null)
        {
            TempData["Error"] = problem;
            return RedirectToAction("Details", "Rooms", new { id = roomId, date = start.Date.ToString("yyyy-MM-dd") });
        }

        return View(ToConfirm(room, start, end, people, ""));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ConfirmBooking form)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == form.RoomId);
        if (room == null) return NotFound();

        // price and room name come from the database, never from what the browser sent back
        var shown = ToConfirm(room, form.StartAt, form.EndAt, form.Attendees, form.Title);
        if (!ModelState.IsValid) return View(shown);

        var (ok, message, id) = await _bookings.CreateAsync(room.Id, UserId, form.Title, form.StartAt, form.EndAt, form.Attendees);
        if (!ok)
        {
            ModelState.AddModelError("", message);
            return View(shown);
        }

        TempData["Message"] = $"Booked {room.Name} on {form.StartAt:ddd d MMM} from {form.StartAt:HH:mm} to {form.EndAt:HH:mm}.";
        return RedirectToAction(nameof(My));
    }

    private static ConfirmBooking ToConfirm(Room room, DateTime start, DateTime end, int people, string title) => new()
    {
        RoomId = room.Id,
        RoomName = room.Name,
        StartAt = start,
        EndAt = end,
        Attendees = people,
        Total = BookingRules.Price(start, end, room.PricePerHour),
        Title = title,
    };

    public async Task<IActionResult> My() => View(await _bookings.ForUserAsync(UserId));

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var problem = await _bookings.CancelAsync(id, UserId, isAdmin: false);
        TempData[problem == null ? "Message" : "Error"] = problem ?? "Booking cancelled.";
        return RedirectToAction(nameof(My));
    }
}
