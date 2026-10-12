using Heron.Web.Data;
using Heron.Web.Models;
using Heron.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Controllers;

public class RoomsController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public RoomsController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    public async Task<IActionResult> Index()
    {
        var rooms = await _db.Rooms.AsNoTracking().Where(r => r.IsActive)
            .OrderBy(r => r.Capacity).ThenBy(r => r.Name).ToListAsync();
        return View(rooms);
    }

    // A page for one room with a day timeline. The timeline is drawn by wwwroot/js/timeline.js
    // from the JSON that Day() returns.
    public async Task<IActionResult> Details(int id, DateTime? date)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (room == null) return NotFound();

        ViewBag.Date = (date ?? DateTime.Today.AddDays(1)).Date;
        return View(room);
    }

    // JSON for the timeline. Only the times that are taken, no names or titles.
    [HttpGet("rooms/{id:int}/day")]
    public async Task<IActionResult> Day(int id, DateTime date)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (room == null) return NotFound();

        var busy = await _bookings.BusyAsync(id, date);
        return Json(new
        {
            open = room.OpenFrom.ToString("HH:mm"),
            close = room.OpenTo.ToString("HH:mm"),
            slotMinutes = BookingRules.SlotMinutes,
            busy = busy.Select(b => new { start = b.Start.ToString("HH:mm"), end = b.End.ToString("HH:mm") }),
        });
    }
}
