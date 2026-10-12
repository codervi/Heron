-- Creates a booking, or says why it can't.
--
-- Result codes (first column of the one row that comes back):
--   0  booked          BookingId has the new id
--   1  room not found, or switched off
--   2  bad time        not on a 30 minute step, not on one day, end before start, or outside opening hours
--   3  too many people for the room
--   4  the room is already taken at that time
--
-- The danger with bookings is two people asking for the same room at the same moment:
-- both run the overlap check, both see "free", both insert. To stop that, the first thing
-- the procedure does is lock the ROOM row (UPDLOCK) until the transaction ends. The second
-- caller waits there, and when it gets its turn the first booking is already saved,
-- so its overlap check finds it. Different rooms don't block each other.
CREATE OR ALTER PROCEDURE dbo.usp_CreateBooking
    @RoomId    int,
    @UserId    nvarchar(450),
    @Title     nvarchar(100),
    @StartAt   datetime2(0),
    @EndAt     datetime2(0),
    @Attendees int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- any error rolls the whole transaction back

    DECLARE @Capacity int, @Price decimal(10, 2), @OpenFrom time, @OpenTo time, @Active bit;

    BEGIN TRANSACTION;

    SELECT @Capacity = Capacity, @Price = PricePerHour, @OpenFrom = OpenFrom, @OpenTo = OpenTo, @Active = IsActive
    FROM dbo.Rooms WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @RoomId;

    IF @Capacity IS NULL OR @Active = 0
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT 1 AS Result, CAST(NULL AS int) AS BookingId;
        RETURN;
    END;

    IF @EndAt <= @StartAt
       OR CAST(@StartAt AS date) <> CAST(@EndAt AS date)
       OR DATEPART(MINUTE, @StartAt) NOT IN (0, 30) OR DATEPART(MINUTE, @EndAt) NOT IN (0, 30)
       OR DATEPART(SECOND, @StartAt) <> 0           OR DATEPART(SECOND, @EndAt) <> 0
       OR CAST(@StartAt AS time) < @OpenFrom
       OR CAST(@EndAt   AS time) > @OpenTo
       OR @Attendees < 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT 2 AS Result, CAST(NULL AS int) AS BookingId;
        RETURN;
    END;

    IF @Attendees > @Capacity
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT 3 AS Result, CAST(NULL AS int) AS BookingId;
        RETURN;
    END;

    -- Two time ranges overlap when each one starts before the other ends.
    -- Touching ranges (one ends at 10:00, the next starts at 10:00) do NOT overlap.
    IF EXISTS (SELECT 1
               FROM dbo.Bookings
               WHERE RoomId = @RoomId
                 AND Status = 'Confirmed'
                 AND StartAt < @EndAt
                 AND EndAt   > @StartAt)
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT 4 AS Result, CAST(NULL AS int) AS BookingId;
        RETURN;
    END;

    INSERT INTO dbo.Bookings
        (RoomId, UserId, Title, StartAt, EndAt, Attendees, PricePerHour, TotalPrice, Status, CreatedAtUtc)
    VALUES
        (@RoomId, @UserId, @Title, @StartAt, @EndAt, @Attendees, @Price,
         CAST(ROUND(DATEDIFF(MINUTE, @StartAt, @EndAt) / 60.0 * @Price, 2) AS decimal(10, 2)),
         'Confirmed', SYSUTCDATETIME());

    DECLARE @NewId int = CAST(SCOPE_IDENTITY() AS int);

    COMMIT TRANSACTION;
    SELECT 0 AS Result, @NewId AS BookingId;
END;
