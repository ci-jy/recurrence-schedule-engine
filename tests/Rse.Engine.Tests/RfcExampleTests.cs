using static Rse.Engine.Tests.TestUtil;

namespace Rse.Engine.Tests;

/// <summary>Examples from RFC 5545 section 3.8.5.3 (DTSTART in America/New_York).</summary>
public class RfcExampleTests
{
    [Fact]
    public void DailyForTenOccurrences()
    {
        var occ = Local("FREQ=DAILY;COUNT=10", "19970902T090000");
        Assert.Equal(10, occ.Count);
        Assert.Equal(L("19970911T090000"), occ[^1]);
    }

    [Fact]
    public void EveryOtherDayForever()
    {
        var occ = Local("FREQ=DAILY;INTERVAL=2", "19970902T090000", take: 4);
        Assert.Equal([L("19970902T090000"), L("19970904T090000"), L("19970906T090000"), L("19970908T090000")], occ);
    }

    [Fact]
    public void WeeklyOnTuesdayAndThursdayUntilUtc()
    {
        var occ = Local("FREQ=WEEKLY;UNTIL=19971007T000000Z;WKST=SU;BYDAY=TU,TH", "19970902T090000");
        Assert.Equal(10, occ.Count);
        Assert.Equal(L("19970902T090000"), occ[0]);
        Assert.Equal(L("19971002T090000"), occ[^1]);
    }

    [Fact]
    public void MonthlyOnFirstFriday()
    {
        var occ = Local("FREQ=MONTHLY;COUNT=10;BYDAY=1FR", "19970905T090000");
        Assert.Equal(new[] { "19970905", "19971003", "19971107", "19971205", "19980102", "19980206", "19980306", "19980403", "19980501", "19980605" },
            occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void MonthlyOnSecondToLastMonday()
    {
        var occ = Local("FREQ=MONTHLY;COUNT=6;BYDAY=-2MO", "19970922T090000");
        Assert.Equal(new[] { "19970922", "19971020", "19971117", "19971222", "19980119", "19980216" },
            occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void LastWeekdayOfTheMonth()
    {
        var occ = Local("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1", "19970930T090000", take: 4);
        Assert.Equal(new[] { "19970930", "19971031", "19971128", "19971231" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void ThirdInstanceOfTueWedThuForThreeMonths()
    {
        var occ = Local("FREQ=MONTHLY;COUNT=3;BYDAY=TU,WE,TH;BYSETPOS=3", "19970904T090000");
        Assert.Equal(new[] { "19970904", "19971007", "19971106" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void EveryFridayThe13th()
    {
        var occ = Local("FREQ=MONTHLY;BYDAY=FR;BYMONTHDAY=13", "19970902T090000", take: 5);
        Assert.Equal(new[] { "19980213", "19980313", "19981113", "19990813", "20001013" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void YearlyOnTwentiethMonday()
    {
        var occ = Local("FREQ=YEARLY;BYDAY=20MO", "19970519T090000", take: 3);
        Assert.Equal(new[] { "19970519", "19980518", "19990517" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void YearlyInJuneAndJuly()
    {
        var occ = Local("FREQ=YEARLY;COUNT=10;BYMONTH=6,7", "19970610T090000");
        Assert.Equal(new[] { "19970610", "19970710", "19980610", "19980710" }, occ.Take(4).Select(d => d.ToString("yyyyMMdd")));
        Assert.Equal(10, occ.Count);
    }

    [Fact]
    public void InvalidDatesAreSkipped()
    {
        var occ = Local("FREQ=MONTHLY;BYMONTHDAY=15,30;COUNT=5", "20070115T090000");
        Assert.Equal(new[] { "20070115", "20070130", "20070215", "20070315", "20070330" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void LeapDayOnlyInLeapYears()
    {
        var occ = Local("FREQ=YEARLY;COUNT=3", "20240229T090000");
        Assert.Equal(new[] { "20240229", "20280229", "20320229" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Theory]
    [InlineData("MO", new[] { "19970805", "19970810", "19970819", "19970824" })]
    [InlineData("SU", new[] { "19970805", "19970817", "19970819", "19970831" })]
    public void WeekStartChangesBiweeklyExpansion(string wkst, string[] expected)
    {
        var occ = Local($"FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST={wkst}", "19970805T090000");
        Assert.Equal(expected, occ.Select(d => d.ToString("yyyyMMdd")));
    }

    [Fact]
    public void LastDayOfMonthWithNegativeMonthDay()
    {
        var occ = Local("FREQ=MONTHLY;BYMONTHDAY=-1;COUNT=4", "20240115T080000");
        Assert.Equal(new[] { "20240131", "20240229", "20240331", "20240430" }, occ.Select(d => d.ToString("yyyyMMdd")));
    }
}
