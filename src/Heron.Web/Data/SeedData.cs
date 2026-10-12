using Heron.Web.Models;
using Heron.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Data;

public static class SeedData
{
    public static async Task RunAsync(IServiceProvider services, bool development)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await RunSqlScriptsAsync(db);

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

        if (!development || await db.Rooms.AnyAsync()) return;

        var password = sp.GetRequiredService<IConfiguration>()["SeedUsers:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var admin = await CreateUserAsync(users, "admin@heron.test", "Heron Admin", Roles.Admin, password);
        var customers = new[]
        {
            await CreateUserAsync(users, "customer@heron.test", "Sam Carter", Roles.Customer, password),
            await CreateUserAsync(users, "priya@heron.test", "Priya Nair", Roles.Customer, password),
            await CreateUserAsync(users, "tomas@heron.test", "Tomas Berg", Roles.Customer, password),
        };

        Room R(string name, string text, int cap, decimal price, int open, int close, bool active = true) => new()
        {
            Name = name, Description = text, Capacity = cap, PricePerHour = price, IsActive = active,
            OpenFrom = new TimeOnly(open, 0), OpenTo = new TimeOnly(close, 0),
        };

        var rooms = new[]
        {
            R("Pod", "A quiet booth for calls and one-to-ones.", 2, 8m, 7, 21),
            R("Kingfisher", "Small room with a screen and a whiteboard.", 4, 18m, 8, 20),
            R("Reed", "Round table, good for workshops.", 8, 30m, 8, 20),
            R("Willow", "Boardroom with video conferencing.", 12, 42m, 8, 20),
            R("Heron Hall", "Our biggest room, set up theatre style.", 20, 60m, 9, 18),
            R("Old Studio", "Closed for decorating.", 6, 22m, 8, 20, active: false),
        };
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        // Demo bookings: the last 30 days (so the reports have something to show) and the next week.
        // Slots are laid out one after another from the opening time, so nothing overlaps.
        var rng = new Random(42);
        var titles = new[] { "Team sync", "Client call", "Workshop", "Interview", "Planning", "Design review", "Training", "1:1" };
        var today = DateTime.Today;

        foreach (var room in rooms.Where(r => r.IsActive))
        {
            for (var offset = -30; offset <= 7; offset++)
            {
                var day = today.AddDays(offset);
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                if (offset >= 0 && rng.Next(3) == 0) continue; // the future is a bit emptier

                var cursor = day.Add(room.OpenFrom.ToTimeSpan());
                var close = day.Add(room.OpenTo.ToTimeSpan());
                var count = rng.Next(1, offset < 0 ? 5 : 3);

                for (var i = 0; i < count; i++)
                {
                    cursor = cursor.AddMinutes(30 * rng.Next(0, 4));         // gap before it
                    var end = cursor.AddMinutes(30 * rng.Next(2, 7));        // 1 to 3 hours
                    if (end > close) break;
                    // a booking that starts today but is already over is fine (history), one in the future too

                    var customer = customers[rng.Next(customers.Length)];
                    db.Bookings.Add(new Booking
                    {
                        RoomId = room.Id,
                        UserId = customer.Id,
                        Title = titles[rng.Next(titles.Length)],
                        StartAt = cursor,
                        EndAt = end,
                        Attendees = rng.Next(1, room.Capacity + 1),
                        PricePerHour = room.PricePerHour,
                        TotalPrice = BookingRules.Price(cursor, end, room.PricePerHour),
                        Status = rng.Next(12) == 0 ? BookingStatus.Cancelled : BookingStatus.Confirmed,
                        CreatedAtUtc = cursor.AddDays(-rng.Next(1, 10)).ToUniversalTime(),
                    });
                    cursor = end;
                }
            }
        }
        await db.SaveChangesAsync();
    }

    // The views and stored procedures live in Sql/*.sql as CREATE OR ALTER, so running them on
    // every start is safe, and changing a script is just editing a file.
    private static async Task RunSqlScriptsAsync(AppDbContext db)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sql");
        foreach (var file in Directory.GetFiles(folder, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
            await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(file));
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> users, string email, string name, string role, string password)
    {
        var user = new AppUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException("Demo user failed: " + result.Errors.First().Description);
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
