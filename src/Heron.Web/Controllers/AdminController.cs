using Heron.Web.Data;
using Heron.Web.Models;
using Heron.Web.Services;
using Heron.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public AdminController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    // ----- reports -----

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (start > end) (start, end) = (end, start);
        if ((end - start).TotalDays > 366) start = end.AddDays(-366);

        return View(await _bookings.DashboardAsync(start, end));
    }

    // ----- bookings of one day -----

    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        ViewBag.Date = day;
        return View(await _bookings.ForDayAsync(day));
    }

    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(int id, DateTime? date)
    {
        var problem = await _bookings.CancelAsync(id, userId: "", isAdmin: true);
        TempData[problem == null ? "Message" : "Error"] = problem ?? "Booking cancelled.";
        return RedirectToAction(nameof(Bookings), new { date = (date ?? DateTime.Today).ToString("yyyy-MM-dd") });
    }

    // ----- rooms -----

    [HttpGet("rooms")]
    public async Task<IActionResult> Rooms() =>
        View(await _db.Rooms.AsNoTracking().OrderByDescending(r => r.IsActive).ThenBy(r => r.Capacity).ToListAsync());

    [HttpGet("rooms/new")]
    public IActionResult NewRoom() => View("EditRoom", new RoomForm());

    [HttpGet("rooms/{id:int}")]
    public async Task<IActionResult> EditRoom(int id)
    {
        var r = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        return View(new RoomForm
        {
            Id = r.Id, Name = r.Name, Description = r.Description, Capacity = r.Capacity,
            PricePerHour = r.PricePerHour, IsActive = r.IsActive,
            OpenFrom = r.OpenFrom.ToString("HH:mm"), OpenTo = r.OpenTo.ToString("HH:mm"),
        });
    }

    [HttpPost("rooms/save")]
    public async Task<IActionResult> SaveRoom(RoomForm form)
    {
        if (!TimeOnly.TryParse(form.OpenFrom, out var from) || !TimeOnly.TryParse(form.OpenTo, out var to))
            ModelState.AddModelError("", "Opening times must look like 08:00.");
        else if (to <= from)
            ModelState.AddModelError("", "The room must close after it opens.");
        else if (from.Minute % 30 != 0 || to.Minute % 30 != 0)
            ModelState.AddModelError("", "Opening times must be on the hour or the half hour.");

        if (await _db.Rooms.AnyAsync(r => r.Name == form.Name.Trim() && r.Id != form.Id))
            ModelState.AddModelError(nameof(form.Name), "There is already a room with that name.");

        if (!ModelState.IsValid) return View("EditRoom", form);

        Room room;
        if (form.Id == 0)
        {
            room = new Room();
            _db.Rooms.Add(room);
        }
        else
        {
            room = await _db.Rooms.FindAsync(form.Id) ?? throw new InvalidOperationException();
        }

        room.Name = form.Name.Trim();
        room.Description = form.Description?.Trim() ?? "";
        room.Capacity = form.Capacity;
        room.PricePerHour = form.PricePerHour;
        room.OpenFrom = from;
        room.OpenTo = to;
        room.IsActive = form.IsActive;
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Saved {room.Name}. Existing bookings keep their price.";
        return RedirectToAction(nameof(Rooms));
    }
}
