namespace Rse.Engine;

/// <summary>
/// Maps local wall-clock times to UTC following RFC 5545 section 3.3.5: a time that falls in a DST gap
/// is interpreted with the UTC offset in force before the gap (so 02:30 on a spring-forward night
/// becomes 03:30 daylight time), and an ambiguous time in a fall-back overlap resolves to its first
/// (earlier) instance.
/// </summary>
public static class TimeZoneResolver
{
    public static TimeZoneInfo Find(string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Unknown time zone '{ianaId}'", nameof(ianaId), e);
        }
    }

    public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        if (zone == TimeZoneInfo.Utc) return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // Offsets a day either side bracket any single transition near this local time.
        var before = OffsetAt(local.AddDays(-1), zone);
        var after = OffsetAt(local.AddDays(1), zone);
        // Same offset on both sides: no transition within a day, so the wall time maps to one instant.
        if (before == after) return DateTime.SpecifyKind(local - before, DateTimeKind.Utc);

        DateTime? best = null;
        foreach (var offset in new[] { before, after })
        {
            var candidate = local - offset;
            if (candidate < DateTime.MinValue.AddDays(2) || candidate > DateTime.MaxValue.AddDays(-2)) continue;
            var utc = DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
            if (zone.GetUtcOffset(utc) == offset && (best is null || utc < best)) best = utc;
        }
        // No offset reproduces the wall time: it is inside a gap, so use the pre-gap offset.
        return best ?? DateTime.SpecifyKind(local - before, DateTimeKind.Utc);
    }

    private static TimeSpan OffsetAt(DateTime approxLocal, TimeZoneInfo zone) =>
        zone.GetUtcOffset(DateTime.SpecifyKind(approxLocal, DateTimeKind.Utc));
}
