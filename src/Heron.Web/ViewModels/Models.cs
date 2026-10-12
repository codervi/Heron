using System.ComponentModel.DataAnnotations;

namespace Heron.Web.ViewModels;

// ----- rows that come back from stored procedures and views -----
// Column names here match the SELECT lists in /Sql, that is how EF fills them in.

public class CreateBookingResult
{
    public int Result { get; set; }
    public int? BookingId { get; set; }
}

public class AvailableRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Capacity { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal TotalPrice { get; set; }
}

public class BookingDetail
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public string UserId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal Hours { get; set; }
    public int Attendees { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}

public class RoomUtilisationRow
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public decimal BookedHours { get; set; }
    public decimal OpenHours { get; set; }
    public decimal UtilisationPercent { get; set; }
}

public class RevenueRow
{
    public string RoomName { get; set; } = "";
    public int Bookings { get; set; }
    public decimal Hours { get; set; }
    public decimal Revenue { get; set; }
    public bool IsTotal { get; set; }
}

public class PeakHourRow
{
    public int Hour { get; set; }
    public int Bookings { get; set; }
    public int BookedMinutes { get; set; }
}

// ----- pages -----

public class SearchForm
{
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    // "09:30"
    public string Start { get; set; } = "09:00";

    // length in minutes
    public int Minutes { get; set; } = 60;

    [Range(1, 100)]
    public int People { get; set; } = 4;
}

public record SearchPage(SearchForm Form, bool Searched, DateTime? StartAt, DateTime? EndAt, string? Problem, List<AvailableRoom> Rooms);

public class ConfirmBooking
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int Attendees { get; set; }
    public decimal Total { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    [Display(Name = "What is the booking for?")]
    public string Title { get; set; } = "";
}

public class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class RegisterForm
{
    [Required, StringLength(100), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
}

public class RoomForm
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [StringLength(300)] public string Description { get; set; } = "";
    [Range(1, 200)] public int Capacity { get; set; } = 6;
    [Range(0, 10000), Display(Name = "Price per hour")] public decimal PricePerHour { get; set; } = 20;
    [Required, Display(Name = "Opens")] public string OpenFrom { get; set; } = "08:00";
    [Required, Display(Name = "Closes")] public string OpenTo { get; set; } = "20:00";
    [Display(Name = "Can be booked")] public bool IsActive { get; set; } = true;
}

public record AdminDashboard(
    DateTime From, DateTime To,
    List<RevenueRow> Revenue, List<PeakHourRow> Peak, List<RoomUtilisationRow> Utilisation,
    int UpcomingToday);
