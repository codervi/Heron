-- One row per booking with the room and the customer already joined in.
-- The admin list and "My bookings" read from this, so the joins live in one place.
CREATE OR ALTER VIEW dbo.vw_BookingDetails
AS
SELECT
    b.Id,
    b.RoomId,
    r.Name                                   AS RoomName,
    b.UserId,
    u.FullName                               AS CustomerName,
    u.Email                                  AS CustomerEmail,
    b.Title,
    b.StartAt,
    b.EndAt,
    CAST(DATEDIFF(MINUTE, b.StartAt, b.EndAt) / 60.0 AS decimal(5, 1)) AS Hours,
    b.Attendees,
    b.TotalPrice,
    b.Status,
    b.CreatedAtUtc,
    b.CancelledAtUtc
FROM dbo.Bookings AS b
JOIN dbo.Rooms        AS r ON r.Id = b.RoomId
JOIN dbo.AspNetUsers  AS u ON u.Id = b.UserId;
