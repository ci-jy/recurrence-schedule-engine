namespace Rse.Engine;

/// <summary>A booked recurring series: a recurrence set plus a fixed duration and the resource it occupies.</summary>
public sealed record ScheduleSeries(string Id, string Title, string? Resource, RecurrenceSet Recurrence, TimeSpan Duration)
{
    /// <summary>Occurrences that overlap [fromUtc, toUtc), i.e. that start before toUtc and end after fromUtc.</summary>
    public IEnumerable<TimeSlot> SlotsOverlapping(DateTime fromUtc, DateTime toUtc) =>
        Recurrence.Between(fromUtc - Duration + TimeSpan.FromTicks(1), toUtc)
            .Select(o => new TimeSlot(o.UtcStart, o.UtcStart + Duration, o.LocalStart));

    /// <summary>Cheap test that the series cannot have any occurrence in the window.</summary>
    public bool CannotOverlap(DateTime fromUtc, DateTime toUtc) =>
        Recurrence.DtStartUtc >= toUtc ||
        (Recurrence.UntilUtc is DateTime u && u + Duration <= fromUtc);
}

/// <summary>A concrete half-open interval [StartUtc, EndUtc).</summary>
public readonly record struct TimeSlot(DateTime StartUtc, DateTime EndUtc, DateTime LocalStart);
