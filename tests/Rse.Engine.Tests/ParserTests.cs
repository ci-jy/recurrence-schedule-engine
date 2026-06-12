using Rse.Engine;

namespace Rse.Engine.Tests;

public class ParserTests
{
    [Fact]
    public void ParsesAllSupportedParts()
    {
        var r = RecurrenceRule.Parse("RRULE:FREQ=MONTHLY;INTERVAL=2;COUNT=10;BYDAY=2TU,-1FR,MO;BYMONTHDAY=1,-1;BYMONTH=3,1;BYSETPOS=-1;WKST=SU");
        Assert.Equal(Frequency.Monthly, r.Frequency);
        Assert.Equal(2, r.Interval);
        Assert.Equal(10, r.Count);
        Assert.Equal([new WeekdayNum(2, DayOfWeek.Tuesday), new WeekdayNum(-1, DayOfWeek.Friday), new WeekdayNum(0, DayOfWeek.Monday)], r.ByDay);
        Assert.Equal([1, -1], r.ByMonthDay);
        Assert.Equal([1, 3], r.ByMonth);
        Assert.Equal([-1], r.BySetPos);
        Assert.Equal(DayOfWeek.Sunday, r.WeekStart);
    }

    [Fact]
    public void ParsesUtcUntil()
    {
        var r = RecurrenceRule.Parse("FREQ=DAILY;UNTIL=20250101T120000Z");
        Assert.True(r.UntilIsUtc);
        Assert.Equal(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), r.Until);
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE")]
    [InlineData("FREQ=MONTHLY;COUNT=5;BYMONTHDAY=-1")]
    [InlineData("FREQ=YEARLY;UNTIL=20300101T000000Z;BYMONTH=11;BYDAY=1SU")]
    public void ToStringRoundTrips(string text)
    {
        var r = RecurrenceRule.Parse(text);
        Assert.Equal(r.ToString(), RecurrenceRule.Parse(r.ToString()).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("INTERVAL=2")]
    [InlineData("FREQ=FORTNIGHTLY")]
    [InlineData("FREQ=HOURLY")]
    [InlineData("FREQ=DAILY;COUNT=3;UNTIL=20250101T000000Z")]
    [InlineData("FREQ=DAILY;INTERVAL=0")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=32")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=0")]
    [InlineData("FREQ=YEARLY;BYMONTH=13")]
    [InlineData("FREQ=MONTHLY;BYDAY=0MO")]
    [InlineData("FREQ=MONTHLY;BYDAY=XX")]
    [InlineData("FREQ=DAILY;BYSETPOS=1")]
    [InlineData("FREQ=DAILY;BYHOUR=9")]
    [InlineData("FREQ=DAILY;FREQ=WEEKLY")]
    [InlineData("FREQ=DAILY;UNTIL=tomorrow")]
    [InlineData("FREQ=DAILY;WKST=XY")]
    public void RejectsInvalidOrUnsupportedRules(string text) =>
        Assert.Throws<RRuleParseException>(() => RecurrenceRule.Parse(text));
}
