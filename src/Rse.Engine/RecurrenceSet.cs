namespace Rse.Engine;

/// <summary>A single occurrence: its local wall-clock start and the corresponding UTC instant.</summary>
public readonly record struct Occurrence(DateTime LocalStart, DateTime UtcStart);

/// <summary>DTSTART + RRULE + EXDATE in one IANA time zone: the recurrence set of RFC 5545 section 3.8.5.</summary>
public sealed class RecurrenceSet
{
    private readonly HashSet<DateTime> _exDates;

    public RecurrenceRule Rule { get; }
    public DateTime DtStart { get; }
    public TimeZoneInfo Zone { get; }
    public IReadOnlyCollection<DateTime> ExDates => _exDates;

    public RecurrenceSet(RecurrenceRule rule, DateTime dtStartLocal, TimeZoneInfo zone, IEnumerable<DateTime>? exDatesLocal = null)
    {
        Rule = rule;
        DtStart = DateTime.SpecifyKind(dtStartLocal, DateTimeKind.Unspecified);
        Zone = zone;
        _exDates = new HashSet<DateTime>((exDatesLocal ?? []).Select(d => DateTime.SpecifyKind(d, DateTimeKind.Unspecified)));
    }

    public RecurrenceSet(string rrule, DateTime dtStartLocal, string timeZoneId, IEnumerable<DateTime>? exDatesLocal = null)
        : this(RecurrenceRule.Parse(rrule), dtStartLocal, TimeZoneResolver.Find(timeZoneId), exDatesLocal)
    {
    }

    public DateTime DtStartUtc => TimeZoneResolver.ToUtc(DtStart, Zone);

    /// <summary>Upper bound on the UTC start of any occurrence, or null if the set is unbounded or COUNT-limited.</summary>
    public DateTime? UntilUtc => Rule.Until is not DateTime u ? null
        : Rule.UntilIsUtc ? u : TimeZoneResolver.ToUtc(u, Zone);

    /// <summary>All occurrences in order (lazy; unbounded rules end only at year 9999).</summary>
    public IEnumerable<Occurrence> Enumerate() => Enumerate(null);

    /// <summary>Occurrences whose UTC start lies in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), produced lazily.</summary>
    public IEnumerable<Occurrence> Between(DateTime fromUtc, DateTime toUtc)
    {
        foreach (var occ in Enumerate(fromUtc))
        {
            if (occ.UtcStart >= toUtc) yield break;
            if (occ.UtcStart >= fromUtc) yield return occ;
        }
    }

    private IEnumerable<Occurrence> Enumerate(DateTime? fromUtc)
    {
        DateTime? hint = null;
        if (fromUtc is DateTime f && Rule.Count is null && f > DateTime.MinValue.AddDays(3))
        {
            // Two days of slack covers any UTC offset and DST shift.
            hint = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(f, DateTimeKind.Utc), Zone).AddDays(-2);
        }

        var until = Rule.Until;
        var produced = 0;
        foreach (var local in RuleExpander.Expand(Rule, DtStart, hint))
        {
            var utc = TimeZoneResolver.ToUtc(local, Zone);
            if (until is DateTime u && (Rule.UntilIsUtc ? utc > u : local > u)) yield break;
            if (Rule.Count is int c && produced++ >= c) yield break;
            if (_exDates.Contains(local)) continue;
            yield return new Occurrence(local, utc);
        }
    }
}
