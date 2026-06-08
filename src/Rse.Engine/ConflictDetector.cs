using System.Collections.Concurrent;

namespace Rse.Engine;

public readonly record struct SlotConflict(TimeSlot First, TimeSlot Second)
{
    public DateTime OverlapStartUtc => First.StartUtc > Second.StartUtc ? First.StartUtc : Second.StartUtc;
    public DateTime OverlapEndUtc => First.EndUtc < Second.EndUtc ? First.EndUtc : Second.EndUtc;
}

public sealed record SeriesConflicts(ScheduleSeries Existing, IReadOnlyList<SlotConflict> Conflicts);

/// <summary>
/// Finds clashes between recurring series by merging their occurrence streams lazily, like the merge
/// step of merge sort. Neither series is expanded beyond the window, and a pair stops being expanded
/// as soon as either stream runs out or the per-pair limit is reached.
/// </summary>
public static class ConflictDetector
{
    /// <summary>
    /// Overlapping pairs between two streams sorted by start time. Each stream's slots must not overlap
    /// one another (true for any series whose duration is shorter than its shortest gap).
    /// </summary>
    public static IEnumerable<SlotConflict> Overlaps(IEnumerable<TimeSlot> first, IEnumerable<TimeSlot> second)
    {
        using var a = first.GetEnumerator();
        using var b = second.GetEnumerator();
        if (!a.MoveNext() || !b.MoveNext()) yield break;
        while (true)
        {
            var x = a.Current;
            var y = b.Current;
            if (x.StartUtc < y.EndUtc && y.StartUtc < x.EndUtc) yield return new SlotConflict(x, y);
            // Advance whichever slot finishes first: it cannot overlap anything later in the other stream.
            if (x.EndUtc <= y.EndUtc)
            {
                if (!a.MoveNext()) yield break;
            }
            else if (!b.MoveNext()) yield break;
        }
    }

    /// <summary>
    /// Checks a proposed series against existing ones in a UTC window. Only series that share the
    /// proposal's resource are compared (all series when the proposal has no resource).
    /// </summary>
    public static IReadOnlyList<SeriesConflicts> Check(
        ScheduleSeries proposed,
        IEnumerable<ScheduleSeries> existing,
        DateTime fromUtc,
        DateTime toUtc,
        int maxConflictsPerSeries = 50,
        bool parallel = true)
    {
        if (proposed.CannotOverlap(fromUtc, toUtc)) return [];
        var candidates = existing
            .Where(s => s.Id != proposed.Id)
            .Where(s => proposed.Resource is null || string.Equals(s.Resource, proposed.Resource, StringComparison.OrdinalIgnoreCase))
            .Where(s => !s.CannotOverlap(fromUtc, toUtc))
            .ToList();

        SeriesConflicts? Compare(ScheduleSeries other)
        {
            var hits = Overlaps(proposed.SlotsOverlapping(fromUtc, toUtc), other.SlotsOverlapping(fromUtc, toUtc))
                .Take(maxConflictsPerSeries)
                .ToList();
            return hits.Count == 0 ? null : new SeriesConflicts(other, hits);
        }

        IEnumerable<SeriesConflicts?> results = parallel && candidates.Count > 8
            ? candidates.AsParallel().AsOrdered().Select(Compare)
            : candidates.Select(Compare);
        return results.Where(r => r is not null).Select(r => r!).ToList();
    }
}

/// <summary>Expands many series over a window, either sequentially or across all cores.</summary>
public static class ScheduleExpander
{
    public static List<(ScheduleSeries Series, TimeSlot Slot)> Expand(
        IReadOnlyList<ScheduleSeries> series, DateTime fromUtc, DateTime toUtc, bool parallel)
    {
        if (!parallel)
        {
            var list = new List<(ScheduleSeries, TimeSlot)>();
            foreach (var s in series)
                foreach (var slot in s.SlotsOverlapping(fromUtc, toUtc))
                    list.Add((s, slot));
            list.Sort(BySlotThenId);
            return list;
        }

        var bag = new ConcurrentBag<List<(ScheduleSeries, TimeSlot)>>();
        Parallel.ForEach(Partitioner.Create(0, series.Count), range =>
        {
            var local = new List<(ScheduleSeries, TimeSlot)>();
            for (var i = range.Item1; i < range.Item2; i++)
                foreach (var slot in series[i].SlotsOverlapping(fromUtc, toUtc))
                    local.Add((series[i], slot));
            bag.Add(local);
        });
        var merged = new List<(ScheduleSeries, TimeSlot)>(bag.Sum(l => l.Count));
        foreach (var l in bag) merged.AddRange(l);
        merged.Sort(BySlotThenId);
        return merged;
    }

    private static int BySlotThenId((ScheduleSeries Series, TimeSlot Slot) p, (ScheduleSeries Series, TimeSlot Slot) q)
    {
        var c = p.Slot.StartUtc.CompareTo(q.Slot.StartUtc);
        return c != 0 ? c : string.CompareOrdinal(p.Series.Id, q.Series.Id);
    }
}
