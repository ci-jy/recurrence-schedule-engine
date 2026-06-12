using Rse.Engine;
using static Rse.Engine.Tests.TestUtil;

namespace Rse.Engine.Tests;

public class RecurrenceSetTests
{
    [Fact]
    public void ExDateRemovesOccurrenceButStillCountsTowardCount()
    {
        var occ = Local("FREQ=DAILY;COUNT=5", "2024-01-01 10:00", "UTC", 1000, "2024-01-03 10:00");
        Assert.Equal(4, occ.Count);
        Assert.DoesNotContain(L("2024-01-03 10:00"), occ);
        Assert.Equal(L("2024-01-05 10:00"), occ[^1]);
    }

    [Fact]
    public void DtStartNotMatchingRuleIsNotEmitted()
    {
        // 2024-01-02 is a Tuesday; the rule only selects Mondays.
        var occ = Local("FREQ=WEEKLY;BYDAY=MO;COUNT=2", "2024-01-02 10:00", "UTC");
        Assert.Equal([L("2024-01-08 10:00"), L("2024-01-15 10:00")], occ);
    }

    [Fact]
    public void LocalUntilIsInclusive()
    {
        var occ = Local("FREQ=DAILY;UNTIL=20240103T100000", "2024-01-01 10:00", "UTC");
        Assert.Equal(3, occ.Count);
    }

    [Fact]
    public void DateOnlyUntilCoversTheWholeDay()
    {
        var occ = Local("FREQ=DAILY;UNTIL=20240103", "2024-01-01 18:00", "UTC");
        Assert.Equal(3, occ.Count);
    }

    [Fact]
    public void ImpossibleRuleTerminates()
    {
        var occ = Local("FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30", "2024-01-01 10:00", "UTC");
        Assert.Empty(occ);
    }

    [Theory]
    [InlineData("FREQ=DAILY;INTERVAL=3", "Europe/London")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,TH;WKST=SU", "America/Toronto")]
    [InlineData("FREQ=MONTHLY;INTERVAL=5;BYDAY=-1FR", "Australia/Sydney")]
    [InlineData("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=1,-1", "America/St_Johns")]
    [InlineData("FREQ=YEARLY;INTERVAL=2;BYMONTH=3,11;BYDAY=1SU", "America/New_York")]
    [InlineData("FREQ=YEARLY;BYDAY=-10WE", "Pacific/Auckland")]
    [InlineData("FREQ=DAILY;BYMONTH=3;UNTIL=20400101T000000Z", "Europe/Berlin")]
    public void WindowQueryMatchesFullEnumeration(string rrule, string tz)
    {
        var set = new RecurrenceSet(rrule, L("2021-03-28 02:30"), tz);
        var rng = new Random(rrule.Length);
        for (var i = 0; i < 20; i++)
        {
            var from = U("2021-01-01 00:00").AddHours(rng.Next(0, 24 * 365 * 12));
            var to = from.AddDays(rng.Next(1, 400));
            var expected = set.Enumerate().SkipWhile(o => o.UtcStart < from).TakeWhile(o => o.UtcStart < to).ToList();
            Assert.Equal(expected, set.Between(from, to).ToList());
        }
    }
}
