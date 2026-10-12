using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Heron.Web.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public class Room
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = "";

    [StringLength(300)]
    public string Description { get; set; } = "";

    public int Capacity { get; set; }

    public decimal PricePerHour { get; set; }

    // Opening hours, venue local time. Bookings must fit inside them.
    public TimeOnly OpenFrom { get; set; }
    public TimeOnly OpenTo { get; set; }

    // a room that is switched off keeps its old bookings but can't be booked any more
    public bool IsActive { get; set; } = true;

    public List<Booking> Bookings { get; set; } = new();
}

public enum BookingStatus { Confirmed, Cancelled }

public class Booking
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public string UserId { get; set; } = "";
    public AppUser? User { get; set; }

    [StringLength(100)]
    public string Title { get; set; } = "";

    // All times are the venue's local time. One venue, one time zone, so there is no
    // conversion to get wrong, and "9:00" in the database is "9:00" on the door.
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int Attendees { get; set; }

    // copied from the room at booking time, so a later price change doesn't touch old bookings
    public decimal PricePerHour { get; set; }
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
