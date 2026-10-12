using Heron.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Heron.Web.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Room>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.PricePerHour).HasPrecision(10, 2);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Rooms_Capacity", "[Capacity] > 0");
                t.HasCheckConstraint("CK_Rooms_Hours", "[OpenTo] > [OpenFrom]");
            });
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.StartAt).HasColumnType("datetime2(0)");
            e.Property(x => x.EndAt).HasColumnType("datetime2(0)");
            e.Property(x => x.PricePerHour).HasPrecision(10, 2);
            e.Property(x => x.TotalPrice).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);

            e.HasOne(x => x.Room).WithMany(r => r.Bookings).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

            // The overlap check looks up "bookings of this room around this time", so the room
            // and the start time lead the index. EndAt and Status ride along (INCLUDE), which
            // lets SQL Server answer the check from the index alone without touching the table.
            e.HasIndex(x => new { x.RoomId, x.StartAt }).IncludeProperties(x => new { x.EndAt, x.Status });
            e.HasIndex(x => new { x.UserId, x.StartAt });

            // Last line of defence. The stored procedure checks all of this first and gives a
            // friendly error code, but even a bug in the app can't store a booking like this.
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Bookings_Range", "[EndAt] > [StartAt]");
                t.HasCheckConstraint("CK_Bookings_Slots",
                    "DATEPART(MINUTE,[StartAt]) IN (0,30) AND DATEPART(MINUTE,[EndAt]) IN (0,30) " +
                    "AND DATEPART(SECOND,[StartAt]) = 0 AND DATEPART(SECOND,[EndAt]) = 0 " +
                    "AND CAST([StartAt] AS date) = CAST([EndAt] AS date)");
                t.HasCheckConstraint("CK_Bookings_Attendees", "[Attendees] > 0");
            });
        });
    }
}
