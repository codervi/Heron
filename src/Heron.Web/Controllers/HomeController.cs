using Heron.Web.Services;
using Heron.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Heron.Web.Controllers;

public class HomeController : Controller
{
    private readonly BookingService _bookings;

    public HomeController(BookingService bookings) => _bookings = bookings;

    // The search form sends its values in the address (GET), so a result page can be bookmarked or shared.
    public async Task<IActionResult> Index(SearchForm form)
    {
        var searched = Request.Query.ContainsKey("Date");
        form.Date ??= DateTime.Today.AddDays(1);

        if (!searched || !ModelState.IsValid)
            return View(new SearchPage(form, false, null, null, null, new()));

        if (!TimeOnly.TryParse(form.Start, out var time))
            return View(new SearchPage(form, true, null, null, "Pick a start time.", new()));

        var start = form.Date.Value.Date.Add(time.ToTimeSpan());
        var end = start.AddMinutes(form.Minutes);

        var problem = BookingRules.Validate(start, end, form.People, DateTime.Now);
        if (problem != null)
            return View(new SearchPage(form, true, start, end, problem, new()));

        var rooms = await _bookings.FindAvailableAsync(start, end, form.People);
        return View(new SearchPage(form, true, start, end, null, rooms));
    }

    [Route("error")]
    public IActionResult Error() => View();
}
