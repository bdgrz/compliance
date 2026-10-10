namespace Bdgrz.Compliance.Features.Work;

sealed record WorkDigestScheduleWindow(DateOnly WeekOf, string TimeZoneId,
    DateTimeOffset ScheduledAt);

/// <summary>Resolves the current weekly digest window using the member's local calendar.</summary>
static class WorkDigestSchedule
{
    public static bool TryGetDue(DateTimeOffset now, string? timeZoneId,
        out WorkDigestScheduleWindow window)
    {
        var zone = Resolve(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var weekOf = localDate.AddDays(-(((int)localDate.DayOfWeek + 6) % 7));
        var scheduledLocal = DateTime.SpecifyKind(weekOf.ToDateTime(new TimeOnly(9, 0)),
            DateTimeKind.Unspecified);
        var scheduledAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(scheduledLocal, zone),
            TimeSpan.Zero);
        window = new WorkDigestScheduleWindow(weekOf, zone.Id, scheduledAt);
        return now >= scheduledAt;
    }

    public static bool IsValidTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return true;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static string NormalizeTimeZoneId(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) ? TimeZoneInfo.Utc.Id : Resolve(timeZoneId).Id;

    public static DateTimeOffset NextWeekStartUtc(DateOnly weekOf, string? timeZoneId)
    {
        var zone = Resolve(timeZoneId);
        var localBoundary = DateTime.SpecifyKind(
            weekOf.AddDays(7).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        for (var minute = 0; zone.IsInvalidTime(localBoundary) && minute < 48 * 60; minute++)
            localBoundary = localBoundary.AddMinutes(1);
        if (zone.IsInvalidTime(localBoundary))
            throw new InvalidOperationException("The next local work-week boundary is invalid.");

        var offset = zone.IsAmbiguousTime(localBoundary)
            ? zone.GetAmbiguousTimeOffsets(localBoundary).Max()
            : zone.GetUtcOffset(localBoundary);
        return new DateTimeOffset(localBoundary, offset).ToUniversalTime();
    }

    public static bool IsCurrentWeek(DateOnly weekOf, string? timeZoneId, DateTimeOffset now)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Resolve(timeZoneId)).DateTime);
        var currentWeek = localDate.AddDays(-(((int)localDate.DayOfWeek + 6) % 7));
        return currentWeek == weekOf;
    }

    static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
