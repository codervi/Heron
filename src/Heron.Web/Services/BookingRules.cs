namespace Heron.Web.Services;

// The booking rules that don't need a database. The stored procedure checks the same things
// again (opening hours, capacity, overlap) because the database has the last word, but checking
// here first gives the user a clear message without a round trip.
public static class BookingRules
{
    public const int SlotMinutes = 30;
    public const int MaxHours = 8;
    public const int MaxDaysAhead = 60;
    public const int CancelUntilHoursBefore = 2;

    // Returns a message for the user, or null when the request is fine.
    public static string? Validate(DateTime start, DateTime end, int attendees, DateTime now)
    {
        if (attendees < 1) return "Say how many people are coming.";
        if (end <= start) return "The booking must end after it starts.";
        if (start.Date != end.Date) return "A booking has to start and end on the same day.";
        if (!OnSlot(start) || !OnSlot(end)) return "Times must be on the hour or the half hour.";
        if (start <= now) return "That time has already passed.";
        if (start.Date > now.Date.AddDays(MaxDaysAhead)) return $"You can book up to {MaxDaysAhead} days ahead.";
        if ((end - start).TotalHours > MaxHours) return $"A booking can be at most {MaxHours} hours.";
        return null;
    }

    public static bool OnSlot(DateTime t) => t.Minute % SlotMinutes == 0 && t.Second == 0 && t.Millisecond == 0;

    // A customer can cancel until 2 hours before the start. Admins are not limited by this.
    public static bool CanCancel(DateTime start, DateTime now) =>
        start - now >= TimeSpan.FromHours(CancelUntilHoursBefore);

    public static decimal Price(DateTime start, DateTime end, decimal pricePerHour) =>
        Math.Round((decimal)(end - start).TotalMinutes / 60m * pricePerHour, 2, MidpointRounding.AwayFromZero);

    // Do two time ranges overlap? Each one has to start before the other ends.
    // Ranges that only touch (10:00 end, 10:00 start) don't overlap.
    public static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && endA > startB;

    // What the stored procedure's result code means for a person.
    public static string Message(int resultCode) => resultCode switch
    {
        0 => "Booked.",
        1 => "That room can't be booked right now.",
        2 => "That time isn't possible for this room (check the opening hours).",
        3 => "That room is too small for so many people.",
        4 => "Sorry, someone just booked that room for that time. Pick another room or time.",
        _ => "The booking could not be saved.",
    };
}
