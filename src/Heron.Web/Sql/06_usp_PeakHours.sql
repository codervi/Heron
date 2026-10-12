-- Which hours of the day are busiest? For every hour 0..23 it counts the bookings that touch
-- that hour and the minutes that were actually booked inside it.
-- A booking from 9:30 to 11:00 counts 30 minutes in hour 9, 60 in hour 10, and none in hour 11.
-- Hours with no bookings are left out; the page fills them in with zeros.
CREATE OR ALTER PROCEDURE dbo.usp_PeakHours
    @From date,
    @To   date
AS
BEGIN
    SET NOCOUNT ON;

    WITH Hours AS
    (
        SELECT TOP (24) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS H
        FROM sys.all_objects
    ),
    Spans AS
    (
        -- start and end as "minutes since midnight" (a booking never crosses midnight)
        SELECT
            DATEDIFF(MINUTE, CAST(StartAt AS date), StartAt) AS StartMin,
            DATEDIFF(MINUTE, CAST(StartAt AS date), EndAt)   AS EndMin
        FROM dbo.Bookings
        WHERE Status = 'Confirmed'
          AND StartAt >= @From
          AND StartAt <  DATEADD(DAY, 1, CAST(@To AS datetime2))
    )
    SELECT
        h.H                                   AS [Hour],
        COUNT(*)                              AS Bookings,
        SUM(CASE WHEN s.EndMin   < (h.H + 1) * 60 THEN s.EndMin   ELSE (h.H + 1) * 60 END
          - CASE WHEN s.StartMin > h.H * 60       THEN s.StartMin ELSE h.H * 60       END) AS BookedMinutes
    FROM Hours AS h
    JOIN Spans AS s
      ON s.StartMin < (h.H + 1) * 60
     AND s.EndMin   > h.H * 60
    GROUP BY h.H
    ORDER BY h.H;
END;
