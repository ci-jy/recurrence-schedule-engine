using System.Globalization;
using Rse.Engine;

namespace Rse.Engine.Tests;

internal static class TestUtil
{
    public static DateTime L(string s) =>
        DateTime.ParseExact(s, ["yyyyMMdd'T'HHmmss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None);

    public static DateTime U(string s) => DateTime.SpecifyKind(L(s), DateTimeKind.Utc);

    public static List<DateTime> Local(string rrule, string dtStart, string tz = "America/New_York", int take = 1000, params string[] exDates) =>
        new RecurrenceSet(rrule, L(dtStart), tz, exDates.Select(L)).Enumerate().Take(take).Select(o => o.LocalStart).ToList();
}
