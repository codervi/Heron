using Heron.Web.Services;
using Xunit;

namespace Heron.Tests;

public class BookingRulesTests
{
    // "now" is Monday 12 October 2026, 10:00
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0);

    private static DateTime At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0);

    // ----- Validate -----

    [Fact]
    public void A_normal_booking_is_fine()
    {
        Assert.Null(BookingRules.Validate(At(13, 9), At(13, 10, 30), 4, Now));
    }

    [Fact]
    public void Nobody_coming_is_rejected()
    {
        Assert.NotNull(BookingRules.Validate(At(13, 9), At(13, 10), 0, Now));
    }

    [Theory]
    [InlineData(10, 10)]  // zero length
    [InlineData(10, 9)]   // ends before it starts
    public void End_must_come_after_start(int startHour, int endHour)
    {
        Assert.NotNull(BookingRules.Validate(At(13, startHour), At(13, endHour), 2, Now));
    }

    [Fact]
    public void A_booking_cannot_cross_midnight()
    {
        Assert.NotNull(BookingRules.Validate(At(13, 20), At(14, 1), 2, Now));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    public void Times_must_be_on_the_half_hour(int minute)
    {
        Assert.NotNull(BookingRules.Validate(At(13, 9, minute), At(13, 11), 2, Now));
        Assert.NotNull(BookingRules.Validate(At(13, 9), At(13, 11, minute), 2, Now));
    }

    [Fact]
    public void The_past_is_rejected_and_so_is_right_now()
    {
        Assert.NotNull(BookingRules.Validate(At(12, 8), At(12, 9), 2, Now));
        Assert.NotNull(BookingRules.Validate(At(12, 10), At(12, 11), 2, Now));
        Assert.Null(BookingRules.Validate(At(12, 10, 30), At(12, 11), 2, Now));
    }

    [Fact]
    public void Eight_hours_is_the_longest_booking()
    {
        Assert.Null(BookingRules.Validate(At(13, 9), At(13, 17), 2, Now));
        Assert.NotNull(BookingRules.Validate(At(13, 9), At(13, 17, 30), 2, Now));
    }

    [Fact]
    public void Sixty_days_ahead_is_the_limit()
    {
        var last = Now.Date.AddDays(60);
        Assert.Null(BookingRules.Validate(last.AddHours(9), last.AddHours(10), 2, Now));
        var tooFar = Now.Date.AddDays(61);
        Assert.NotNull(BookingRules.Validate(tooFar.AddHours(9), tooFar.AddHours(10), 2, Now));
    }

    // ----- cancelling -----

    [Fact]
    public void Cancelling_is_allowed_up_to_two_hours_before()
    {
        Assert.True(BookingRules.CanCancel(At(12, 12), Now));       // exactly 2 hours: still fine
        Assert.False(BookingRules.CanCancel(At(12, 11, 30), Now));  // 1.5 hours: too late
        Assert.True(BookingRules.CanCancel(At(14, 9), Now));
    }

    // ----- overlap -----

    [Theory]
    [InlineData(9, 11, 10, 12, true)]   // overlaps the start
    [InlineData(9, 11, 8, 10, true)]    // overlaps the end
    [InlineData(9, 12, 10, 11, true)]   // inside the other one
    [InlineData(10, 11, 9, 12, true)]   // the other one is inside this one
    [InlineData(9, 11, 9, 11, true)]    // identical
    [InlineData(9, 10, 10, 11, false)]  // one ends exactly when the next starts
    [InlineData(11, 12, 9, 10, false)]  // completely apart
    public void Overlap_rules(int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        Assert.Equal(expected, BookingRules.Overlaps(At(13, aStart), At(13, aEnd), At(13, bStart), At(13, bEnd)));
    }

    // ----- price -----

    [Theory]
    [InlineData(9, 0, 10, 0, 30, 30.00)]
    [InlineData(9, 0, 10, 30, 30, 45.00)]
    [InlineData(9, 0, 9, 30, 18, 9.00)]
    [InlineData(9, 0, 11, 30, 7.50, 18.75)]
    public void Price_is_hours_times_the_hourly_rate(int sh, int sm, int eh, int em, double rate, double expected)
    {
        Assert.Equal((decimal)expected, BookingRules.Price(At(13, sh, sm), At(13, eh, em), (decimal)rate));
    }

    // ----- messages -----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(99)]
    public void Every_result_code_has_a_message(int code)
    {
        Assert.False(string.IsNullOrWhiteSpace(BookingRules.Message(code)));
    }
}
