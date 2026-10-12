-- Bookings, hours and money per room for a range of days (both ends included), with a total row.
-- ROLLUP adds the "All rooms" line: GROUPING(r.Name) is 1 only on that line.
-- With no bookings at all the total line still comes back, with zeros (that is what ISNULL is for).
CREATE OR ALTER PROCEDURE dbo.usp_RevenueReport
    @From date,
    @To   date
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CASE WHEN GROUPING(r.Name) = 1 THEN N'All rooms' ELSE r.Name END AS RoomName,
        COUNT(*)                                                        AS Bookings,
        CAST(ISNULL(SUM(DATEDIFF(MINUTE, b.StartAt, b.EndAt)), 0) / 60.0 AS decimal(9, 1)) AS Hours,
        CAST(ISNULL(SUM(b.TotalPrice), 0) AS decimal(12, 2))            AS Revenue,
        CAST(GROUPING(r.Name) AS bit)                                   AS IsTotal
    FROM dbo.Bookings AS b
    JOIN dbo.Rooms    AS r ON r.Id = b.RoomId
    WHERE b.Status = 'Confirmed'
      AND b.StartAt >= @From
      AND b.StartAt <  DATEADD(DAY, 1, CAST(@To AS datetime2))
    GROUP BY ROLLUP (r.Name)
    ORDER BY GROUPING(r.Name), SUM(b.TotalPrice) DESC;
END;
