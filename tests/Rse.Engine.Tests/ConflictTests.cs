using Rse.Engine;
using static Rse.Engine.Tests.TestUtil;

namespace Rse.Engine.Tests;

public class ConflictTests
{
    private static ScheduleSeries S(string id, string rrule, string start, int minutes, string? resource = "Room 1", string tz = "America/Toronto") =>
        new(id, id, resource, new RecurrenceSet(rrule, L(start), tz), TimeSpan.FromMinutes(minutes));

    [Fact]
    public void BiweeklyOverlapWithWeeklySlot()
    {
        var weekly = S("a", "FREQ=WEEKLY;BYDAY=MO", "2024-01-01 09:00", 60);
        var biweekly = S("b", "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO", "2024-01-01 09:30", 60);
        var result = ConflictDetector.Check(biweekly, [weekly], U("2024-01-01 00:00"), U("2024-03-01 00:00"));
        var c = Assert.Single(result);
        Assert.Equal("a", c.Existing.Id);
        Assert.Equal(5, c.Conflicts.Count); // Jan 1, 15, 29, Feb 12, 26
        Assert.All(c.Conflicts, x => Assert.Equal(TimeSpan.FromMinutes(30), x.OverlapEndUtc - x.OverlapStartUtc));
    }

    [Fact]
    public void BackToBackSlotsDoNotConflict()
    {
        var a = S("a", "FREQ=DAILY", "2024-01-01 09:00", 60);
        var b = S("b", "FREQ=DAILY", "2024-01-01 10:00", 30);
        Assert.Empty(ConflictDetector.Check(b, [a], U("2024-01-01 00:00"), U("2024-02-01 00:00")));
    }

    [Fact]
    public void DifferentResourcesDoNotConflict()
    {
        var a = S("a", "FREQ=DAILY", "2024-01-01 09:00", 60, "Room 1");
        var b = S("b", "FREQ=DAILY", "2024-01-01 09:00", 60, "Room 2");
        Assert.Empty(ConflictDetector.Check(b, [a], U("2024-01-01 00:00"), U("2024-02-01 00:00")));
    }

    [Fact]
    public void ConflictAcrossTimeZonesUsesInstants()
    {
        // 14:00 London == 09:00 Toronto (both in standard time in January).
        var toronto = S("a", "FREQ=WEEKLY;BYDAY=TU", "2024-01-02 09:00", 30, tz: "America/Toronto");
        var london = S("b", "FREQ=WEEKLY;BYDAY=TU", "2024-01-02 14:15", 30, tz: "Europe/London");
        var c = Assert.Single(ConflictDetector.Check(london, [toronto], U("2024-01-01 00:00"), U("2024-02-01 00:00")));
        Assert.Equal(5, c.Conflicts.Count);
    }

    [Fact]
    public void DstShiftCreatesTemporaryConflict()
    {
        // North America springs forward three weeks before Europe, so 09:00 Toronto collides with
        // 14:00 London's 13:00-local slot only while the offsets differ by four hours.
        var toronto = S("a", "FREQ=WEEKLY;BYDAY=WE", "2024-01-03 09:00", 60, tz: "America/Toronto");
        var london = S("b", "FREQ=WEEKLY;BYDAY=WE", "2024-01-03 13:00", 60, tz: "Europe/London");
        var c = Assert.Single(ConflictDetector.Check(london, [toronto], U("2024-01-01 00:00"), U("2024-12-31 00:00")));
        Assert.Equal(new[] { "2024-03-13", "2024-03-20", "2024-03-27", "2024-10-30" },
            c.Conflicts.Select(x => x.Second.LocalStart.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public void ConflictsOnlyReportedInsideWindow()
    {
        var a = S("a", "FREQ=DAILY", "2020-01-01 09:00", 60);
        var b = S("b", "FREQ=DAILY", "2020-01-01 09:30", 60);
        var c = Assert.Single(ConflictDetector.Check(b, [a], U("2030-06-01 00:00"), U("2030-06-08 00:00")));
        Assert.Equal(7, c.Conflicts.Count);
    }

    [Fact]
    public void FinishedSeriesIsSkipped()
    {
        var a = S("a", "FREQ=DAILY;COUNT=3", "2024-01-01 09:00", 60);
        var b = S("b", "FREQ=DAILY", "2024-01-01 09:00", 60);
        Assert.Empty(ConflictDetector.Check(b, [a], U("2024-02-01 00:00"), U("2024-03-01 00:00")));
    }

    [Fact]
    public void SweepMatchesBruteForce()
    {
        var rng = new Random(42);
        for (var trial = 0; trial < 200; trial++)
        {
            List<TimeSlot> Stream()
            {
                var list = new List<TimeSlot>();
                var t = U("2024-01-01 00:00").AddMinutes(rng.Next(0, 600));
                var len = TimeSpan.FromMinutes(rng.Next(5, 120));
                for (var i = 0; i < 40; i++)
                {
                    list.Add(new TimeSlot(t, t + len, t));
                    t += len + TimeSpan.FromMinutes(rng.Next(0, 300));
                }
                return list;
            }
            var a = Stream();
            var b = Stream();
            var brute = (from x in a from y in b where x.StartUtc < y.EndUtc && y.StartUtc < x.EndUtc select (x.StartUtc, y.StartUtc)).ToHashSet();
            var swept = ConflictDetector.Overlaps(a, b).Select(c => (c.First.StartUtc, c.Second.StartUtc)).ToList();
            Assert.Equal(brute.Count, swept.Count);
            Assert.True(brute.SetEquals(swept));
        }
    }

    [Fact]
    public void ParallelAndSequentialExpansionAgree()
    {
        var series = Enumerable.Range(0, 60)
            .Select(i => S($"s{i:00}", i % 3 == 0 ? "FREQ=WEEKLY;BYDAY=MO,WE,FR" : i % 3 == 1 ? "FREQ=MONTHLY;BYDAY=-1FR" : "FREQ=DAILY;INTERVAL=2",
                $"2024-01-{1 + i % 20:00} {8 + i % 9:00}:00", 45))
            .ToList();
        var from = U("2024-03-01 00:00");
        var to = U("2024-06-01 00:00");
        var seq = ScheduleExpander.Expand(series, from, to, parallel: false);
        var par = ScheduleExpander.Expand(series, from, to, parallel: true);
        Assert.Equal(seq.Select(p => (p.Series.Id, p.Slot)), par.Select(p => (p.Series.Id, p.Slot)));
        Assert.NotEmpty(seq);
    }

    [Fact]
    public void ParallelAndSequentialConflictChecksAgree()
    {
        var existing = Enumerable.Range(0, 40)
            .Select(i => S($"s{i:00}", "FREQ=WEEKLY;BYDAY=" + (i % 2 == 0 ? "TU" : "TH"), $"2024-01-01 {8 + i % 8:00}:00", 50))
            .ToList();
        var proposed = S("p", "FREQ=WEEKLY;BYDAY=TU,TH", "2024-01-01 10:30", 60);
        var from = U("2024-01-01 00:00");
        var to = U("2024-04-01 00:00");
        var seq = ConflictDetector.Check(proposed, existing, from, to, parallel: false);
        var par = ConflictDetector.Check(proposed, existing, from, to, parallel: true);
        Assert.Equal(seq.Select(c => (c.Existing.Id, c.Conflicts.Count)), par.Select(c => (c.Existing.Id, c.Conflicts.Count)));
        Assert.NotEmpty(seq);
    }
}
