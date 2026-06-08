using System.Globalization;
using System.Text.RegularExpressions;

namespace Rse.Engine;

/// <summary>Parses RFC 5545 RRULE values. Unsupported rule parts are rejected rather than silently ignored.</summary>
public static partial class RRuleParser
{
    private static readonly string[] Codes = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];

    [GeneratedRegex(@"^([+-]?\d{1,2})?(SU|MO|TU|WE|TH|FR|SA)$")]
    private static partial Regex ByDayRegex();

    public static string DayCode(DayOfWeek day) => Codes[(int)day];

    public static RecurrenceRule Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new RRuleParseException("RRULE is empty");
        var body = text.Trim();
        if (body.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)) body = body[6..];

        Frequency? freq = null;
        int interval = 1;
        int? count = null;
        DateTime? until = null;
        bool untilUtc = false;
        var byDay = new List<WeekdayNum>();
        var byMonthDay = new List<int>();
        var byMonth = new List<int>();
        var bySetPos = new List<int>();
        var wkst = DayOfWeek.Monday;
        var seen = new HashSet<string>();

        foreach (var part in body.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) throw new RRuleParseException($"Malformed rule part '{part}'");
            var key = part[..eq].Trim().ToUpperInvariant();
            var value = part[(eq + 1)..].Trim().ToUpperInvariant();
            if (!seen.Add(key)) throw new RRuleParseException($"Duplicate rule part {key}");

            switch (key)
            {
                case "FREQ":
                    freq = value switch
                    {
                        "DAILY" => Frequency.Daily,
                        "WEEKLY" => Frequency.Weekly,
                        "MONTHLY" => Frequency.Monthly,
                        "YEARLY" => Frequency.Yearly,
                        "HOURLY" or "MINUTELY" or "SECONDLY" =>
                            throw new RRuleParseException($"FREQ={value} is not supported (DAILY or coarser only)"),
                        _ => throw new RRuleParseException($"Unknown FREQ '{value}'"),
                    };
                    break;
                case "INTERVAL":
                    interval = ParseInt(key, value, 1, 10_000);
                    break;
                case "COUNT":
                    count = ParseInt(key, value, 1, 1_000_000);
                    break;
                case "UNTIL":
                    (until, untilUtc) = ParseUntil(value);
                    break;
                case "BYDAY":
                    foreach (var item in value.Split(','))
                    {
                        var m = ByDayRegex().Match(item);
                        if (!m.Success) throw new RRuleParseException($"Invalid BYDAY value '{item}'");
                        var ord = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                        if (m.Groups[1].Success && (ord == 0 || Math.Abs(ord) > 53))
                            throw new RRuleParseException($"BYDAY ordinal out of range in '{item}'");
                        byDay.Add(new WeekdayNum(ord, (DayOfWeek)Array.IndexOf(Codes, m.Groups[2].Value)));
                    }
                    break;
                case "BYMONTHDAY":
                    byMonthDay.AddRange(ParseList(key, value, 31, allowNegative: true));
                    break;
                case "BYMONTH":
                    byMonth.AddRange(ParseList(key, value, 12, allowNegative: false));
                    break;
                case "BYSETPOS":
                    bySetPos.AddRange(ParseList(key, value, 366, allowNegative: true));
                    break;
                case "WKST":
                    var idx = Array.IndexOf(Codes, value);
                    if (idx < 0) throw new RRuleParseException($"Invalid WKST '{value}'");
                    wkst = (DayOfWeek)idx;
                    break;
                case "BYSECOND" or "BYMINUTE" or "BYHOUR" or "BYYEARDAY" or "BYWEEKNO":
                    throw new RRuleParseException($"{key} is not supported");
                default:
                    throw new RRuleParseException($"Unknown rule part '{key}'");
            }
        }

        if (freq is null) throw new RRuleParseException("FREQ is required");
        if (count is not null && until is not null) throw new RRuleParseException("COUNT and UNTIL must not both be present");
        if (bySetPos.Count > 0 && byDay.Count == 0 && byMonthDay.Count == 0 && byMonth.Count == 0)
            throw new RRuleParseException("BYSETPOS requires another BYxxx rule part");

        return new RecurrenceRule
        {
            Frequency = freq.Value,
            Interval = interval,
            Count = count,
            Until = until,
            UntilIsUtc = untilUtc,
            ByDay = byDay.Distinct().ToArray(),
            ByMonthDay = byMonthDay.Distinct().ToArray(),
            ByMonth = byMonth.Distinct().Order().ToArray(),
            BySetPos = bySetPos.Distinct().ToArray(),
            WeekStart = wkst,
        };
    }

    private static int ParseInt(string key, string value, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || n < min || n > max)
            throw new RRuleParseException($"{key} must be an integer in [{min}, {max}], got '{value}'");
        return n;
    }

    private static IEnumerable<int> ParseList(string key, string value, int maxAbs, bool allowNegative)
    {
        foreach (var item in value.Split(','))
        {
            var n = ParseInt(key, item, allowNegative ? -maxAbs : 1, maxAbs);
            if (n == 0) throw new RRuleParseException($"{key} values must be non-zero");
            yield return n;
        }
    }

    private static (DateTime, bool) ParseUntil(string value)
    {
        var utc = value.EndsWith('Z');
        var v = utc ? value[..^1] : value;
        if (DateTime.TryParseExact(v, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return (utc ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt, utc);
        // A date-only UNTIL is treated as inclusive of that whole local day.
        if (!utc && DateTime.TryParseExact(v, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return (d.AddDays(1).AddTicks(-1), false);
        throw new RRuleParseException($"Invalid UNTIL '{value}'");
    }
}
