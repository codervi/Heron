-- How busy was each room over the last 30 days (today not included)?
--   BookedHours = hours of confirmed bookings inside the window
--   OpenHours   = hours the room was open in the window (same hours every day)
CREATE OR ALTER VIEW dbo.vw_RoomUtilisation
AS
WITH Period AS
(
    SELECT
        CAST(DATEADD(DAY, -30, CAST(GETDATE() AS date)) AS datetime2) AS FromAt,
        CAST(CAST(GETDATE() AS date) AS datetime2)                    AS ToAt
),
PerRoom AS
(
    SELECT
        r.Id,
        r.Name,
        -- only the minutes of each booking that fall inside the window count,
        -- so a booking that starts before it or ends after it is cut at the edge
        ISNULL(SUM(DATEDIFF(MINUTE,
            CASE WHEN b.StartAt > p.FromAt THEN b.StartAt ELSE p.FromAt END,
            CASE WHEN b.EndAt   < p.ToAt   THEN b.EndAt   ELSE p.ToAt   END)), 0) AS BookedMinutes,
        30 * DATEDIFF(MINUTE, r.OpenFrom, r.OpenTo)                              AS OpenMinutes
    FROM dbo.Rooms AS r
    CROSS JOIN Period AS p
    LEFT JOIN dbo.Bookings AS b
           ON b.RoomId = r.Id
          AND b.Status = 'Confirmed'
          AND b.StartAt < p.ToAt
          AND b.EndAt   > p.FromAt
    GROUP BY r.Id, r.Name, r.OpenFrom, r.OpenTo
)
SELECT
    Id                                              AS RoomId,
    Name                                            AS RoomName,
    CAST(BookedMinutes / 60.0 AS decimal(9, 1))     AS BookedHours,
    CAST(OpenMinutes   / 60.0 AS decimal(9, 1))     AS OpenHours,
    CAST(CASE WHEN OpenMinutes = 0 THEN 0
              ELSE 100.0 * BookedMinutes / OpenMinutes END AS decimal(5, 1)) AS UtilisationPercent
FROM PerRoom;
