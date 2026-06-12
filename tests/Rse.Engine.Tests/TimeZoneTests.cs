using Rse.Engine;
using static Rse.Engine.Tests.TestUtil;

namespace Rse.Engine.Tests;

public class TimeZoneTests
{
    [Fact]
    public void GapTimeUsesOffsetBeforeTheGap()
    {
        // 2024-03-10 02:30 does not exist in Toronto; RFC 5545 maps it to 03:30 EDT (07:30Z).
        var set = new RecurrenceSet("FREQ=DAILY;COUNT=2", L("2024-03-10 02:30"), "America/Toronto");
        var occ = set.Enumerate().ToList();
        Assert.Equal(U("2024-03-10 07:30"), occ[0].UtcStart);
        Assert.Equal(U("2024-03-11 06:30"), occ[1].UtcStart);
    }

    [Fact]
    public void AmbiguousTimeResolvesToFirstInstance()
    {
        var set = new RecurrenceSet("FREQ=WEEKLY;COUNT=2", L("2024-10-27 01:30"), "Europe/London");
        var occ = set.Enumerate().ToList();
        Assert.Equal(U("2024-10-27 00:30"), occ[0].UtcStart); // 01:30 BST, not GMT
        Assert.Equal(U("2024-11-03 01:30"), occ[1].UtcStart);
    }

    [Fact]
    public void WallClockTimeIsKeptAcrossDstChange()
    {
        var occ = new RecurrenceSet("FREQ=WEEKLY;COUNT=3", L("2024-03-03 09:00"), "America/Toronto").Enumerate().ToList();
        Assert.All(occ, o => Assert.Equal(9, o.LocalStart.Hour));
        Assert.Equal(14, occ[0].UtcStart.Hour);
        Assert.Equal(13, occ[1].UtcStart.Hour);
    }

    [Fact]
    public void HalfHourDstShiftOnLordHowe()
    {
        // Lord Howe springs forward 02:00 -> 02:30 on 2024-10-06; 02:15 is in the gap (+10:30 before).
        var utc = TimeZoneResolver.ToUtc(L("2024-10-06 02:15"), TimeZoneResolver.Find("Australia/Lord_Howe"));
        Assert.Equal(U("2024-10-05 15:45"), utc);
    }

    [Fact]
    public void UnknownZoneIsRejected() =>
        Assert.Throws<ArgumentException>(() => TimeZoneResolver.Find("Mars/Olympus_Mons"));

    [Fact]
    public void UtcUntilIsComparedAsInstant()
    {
        // 09:00 Toronto = 13:00Z in summer; UNTIL at 13:00Z on the 3rd day is inclusive.
        var occ = new RecurrenceSet("FREQ=DAILY;UNTIL=20240703T130000Z", L("2024-07-01 09:00"), "America/Toronto").Enumerate().ToList();
        Assert.Equal(3, occ.Count);
    }
}
