-- Rooms that are free for the whole time asked for, and big enough.
-- The smallest room that fits comes first, so a meeting of two doesn't get the 20 seat room.
CREATE OR ALTER PROCEDURE dbo.usp_FindAvailableRooms
    @StartAt   datetime2(0),
    @EndAt     datetime2(0),
    @Attendees int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.Id,
        r.Name,
        r.Description,
        r.Capacity,
        r.PricePerHour,
        CAST(ROUND(DATEDIFF(MINUTE, @StartAt, @EndAt) / 60.0 * r.PricePerHour, 2) AS decimal(10, 2)) AS TotalPrice
    FROM dbo.Rooms AS r
    WHERE r.IsActive = 1
      AND r.Capacity >= @Attendees
      AND CAST(@StartAt AS date) = CAST(@EndAt AS date)
      AND CAST(@StartAt AS time) >= r.OpenFrom
      AND CAST(@EndAt   AS time) <= r.OpenTo
      AND NOT EXISTS (SELECT 1
                      FROM dbo.Bookings AS b
                      WHERE b.RoomId = r.Id
                        AND b.Status = 'Confirmed'
                        AND b.StartAt < @EndAt
                        AND b.EndAt   > @StartAt)
    ORDER BY r.Capacity, r.PricePerHour, r.Name;
END;
