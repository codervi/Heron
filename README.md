# Heron Rooms

A booking system for the meeting rooms of a small venue. A visitor says when and how many people, the system shows the rooms that are free for that whole time, and the visitor books one. Staff get an admin area with bookings, rooms and reports. The venue and its customers are made up.

I wrote it to practise the SQL Server side of an application: a booking can't be allowed to overlap another one, even when two people press the button at the same moment, and the reports should be calculated by the database, not in C#.

ASP.NET Core MVC (.NET 8) with Razor views, EF Core and SQL Server. The room timeline is plain JavaScript (one ES module). The views and stored procedures are SQL files you can read in `src/Heron.Web/Sql`.

## What it does

- **Find a room:** day, start time, length and number of people. Only rooms that are open, big enough and completely free are listed, smallest first, with the price for the whole booking.
- **Room page:** a timeline of the day in 30 minute slots. Click the first slot and the last one, and the booking link is filled in.
- **Booking:** needs an account. Confirm page, then the booking is saved. Customers can cancel until 2 hours before the start.
- **My bookings:** coming up and earlier.
- **Admin** (role `Admin`):
  - bookings of any day, with cancel
  - rooms: add, edit, price, opening hours, switch off
  - reports: revenue by room with a total line, how full each room was in the last 30 days, and the busiest hours of the day

## The SQL

All of it is in `src/Heron.Web/Sql`. The scripts use `CREATE OR ALTER` and are run when the app starts, so editing a script and restarting is all it takes.

| File | What it is |
|---|---|
| `usp_CreateBooking` | Saves a booking in one transaction, or returns a code that says why not (room off, bad time, too many people, room taken). |
| `usp_FindAvailableRooms` | Rooms that are open, big enough and have no overlapping booking, smallest first. |
| `usp_RevenueReport` | Bookings, hours and money per room for a range of days. `GROUP BY ROLLUP` adds the "All rooms" line. |
| `usp_PeakHours` | Minutes booked in every hour of the day. A booking from 9:30 to 11:00 counts 30 minutes in hour 9 and 60 in hour 10. |
| `vw_BookingDetails` | Booking, room and customer joined once, used by the lists. |
| `vw_RoomUtilisation` | Booked hours divided by opening hours for the last 30 days, per room. |

**Two people booking the same room at the same moment.** SQL Server has no "exclusion constraint" for time ranges, so `usp_CreateBooking` starts with `SELECT ... FROM Rooms WITH (UPDLOCK)` on the one room. The second caller waits at that line until the first transaction is finished, and then its overlap check sees the first booking and says no. Bookings of different rooms don't wait for each other.

**The overlap test.** Two bookings overlap when each one starts before the other ends: `StartAt < @EndAt AND EndAt > @StartAt`. A booking that ends at 10:00 and one that starts at 10:00 don't overlap, so back to back bookings work.

**Constraints as a safety net.** The table refuses a booking that ends before it starts, isn't on a 30 minute step, or crosses midnight (`CHECK` constraints). The procedure checks the same things first so the user gets a clear answer, but even a bug in the app can't store such a booking.

**Index.** `Bookings (RoomId, StartAt) INCLUDE (EndAt, Status)`. The overlap check asks for "this room, around this time", and with the other columns included SQL Server can answer from the index alone.

**Prices stay put.** A booking stores the hourly price from the moment it was made. Changing a room's price later doesn't change old bookings or the revenue report.

**Times.** One venue, one time zone: every time is stored as the venue's local time (`datetime2(0)`), so there are no conversions to get wrong. A system with several venues would need to store UTC and the venue's zone.

The C# side calls the procedures with `Database.SqlQuery<T>` and reads the rows into small classes (`ViewModels/Models.cs`).

## Other things worth a look

- `Services/BookingRules.cs` holds the rules that need no database (30 minute steps, no bookings in the past, 8 hours at most, 60 days ahead, the cancel limit, overlap test, price), with unit tests. The procedure repeats the ones that protect the data.
- The timeline logic is in `wwwroot/js/timeline-model.js`, separate from the DOM code so it can be tested.
- Every POST has an anti-forgery token, passwords are locked out after 5 wrong tries, there are security headers and a Content-Security-Policy (so no inline scripts; the confirm dialog on cancel is `data-confirm` handled by `site.js`).
- The public timeline only shows which slots are taken, never who took them.

## Running it

You need the .NET 8 SDK and SQL Server or LocalDB.

```
cd src/Heron.Web
dotnet dev-certs https --trust
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet run
```

The database is created on startup, then the SQL scripts are run. Open https://localhost:7250.

In Development the app adds 6 rooms and about a month of bookings (so the reports have something to show) plus a few for the coming week, and these accounts (password for all: `Demo#2024pass`):

| Email | Role |
|---|---|
| admin@heron.test | Admin (opens /admin) |
| customer@heron.test | Customer |

## Tests

```
dotnet test
```

The tests cover the booking rules. The stored procedures are not covered by automated tests; the overlap and locking behaviour is described above but not proven by a test.

## Layout

```
src/Heron.Web
  Sql/           views and stored procedures
  Controllers/   Home (search), Rooms, Bookings, Account, Admin
  Services/      BookingRules, BookingService, Fmt
  Models/        Room, Booking
  Data/          DbContext, seed data
  Views/         Razor views
  wwwroot/       css, js (timeline.js, timeline-model.js, site.js)
tests/Heron.Tests
```

## Left out on purpose

Online payment, emails, recurring bookings, several venues or time zones, a calendar export.
