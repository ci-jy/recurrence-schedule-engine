using System.Text;

namespace Rse.Engine;

/// <summary>Supported RRULE frequencies (DAILY and coarser).</summary>
public enum Frequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

/// <summary>A BYDAY entry such as <c>MO</c>, <c>2TU</c> or <c>-1FR</c>. Ordinal 0 means "every".</summary>
public readonly record struct WeekdayNum(int Ordinal, DayOfWeek Day)
{
    public override string ToString() =>
        (Ordinal == 0 ? "" : Ordinal.ToString()) + RRuleParser.DayCode(Day);
}

/// <summary>An immutable, validated RFC 5545 recurrence rule.</summary>
public sealed class RecurrenceRule
{
    public required Frequency Frequency { get; init; }
    public int Interval { get; init; } = 1;
    public int? Count { get; init; }

    /// <summary>UNTIL bound. When <see cref="UntilIsUtc"/> is true this is a UTC instant, otherwise a local wall-clock time.</summary>
    public DateTime? Until { get; init; }
    public bool UntilIsUtc { get; init; }

    public IReadOnlyList<WeekdayNum> ByDay { get; init; } = Array.Empty<WeekdayNum>();
    public IReadOnlyList<int> ByMonthDay { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> ByMonth { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> BySetPos { get; init; } = Array.Empty<int>();
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;

    public static RecurrenceRule Parse(string text) => RRuleParser.Parse(text);

    /// <summary>Canonical RRULE text (without the <c>RRULE:</c> prefix).</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("FREQ=").Append(Frequency.ToString().ToUpperInvariant());
        if (Interval != 1) sb.Append(";INTERVAL=").Append(Interval);
        if (Count is int c) sb.Append(";COUNT=").Append(c);
        if (Until is DateTime u) sb.Append(";UNTIL=").Append(u.ToString("yyyyMMdd'T'HHmmss")).Append(UntilIsUtc ? "Z" : "");
        if (ByMonth.Count > 0) sb.Append(";BYMONTH=").AppendJoin(',', ByMonth);
        if (ByMonthDay.Count > 0) sb.Append(";BYMONTHDAY=").AppendJoin(',', ByMonthDay);
        if (ByDay.Count > 0) sb.Append(";BYDAY=").AppendJoin(',', ByDay);
        if (BySetPos.Count > 0) sb.Append(";BYSETPOS=").AppendJoin(',', BySetPos);
        if (WeekStart != DayOfWeek.Monday) sb.Append(";WKST=").Append(RRuleParser.DayCode(WeekStart));
        return sb.ToString();
    }
}
