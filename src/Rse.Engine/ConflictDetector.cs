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
    /// <summary>All slots overlapping [fromUtc, toUtc), ordered by UTC start and then by series id.</summary>
    public static List<(ScheduleSeries Series, TimeSlot Slot)> Expand(
        IReadOnlyList<ScheduleSeries> series, DateTime fromUtc, DateTime toUtc, bool parallel)
    {
        // Rank series by id once, so ties sort on an int instead of a string comparison.
        var byId = Enumerable.Range(0, series.Count).ToArray();
        Array.Sort(byId, (a, b) => string.CompareOrdinal(series[a].Id, series[b].Id));
        var rank = new int[series.Count];
        for (var r = 0; r < byId.Length; r++) rank[byId[r]] = r;

        if (!parallel || series.Count < 2)
        {
            var (keys, items) = SortedRun(series, rank, 0, 1, fromUtc, toUtc);
            return [.. items];
        }

        // Each partition expands and sorts its own series; a k-way merge then combines the sorted runs,
        // so the O(n log n) sorting work is spread across cores instead of done once at the end.
        var partitions = Math.Min(series.Count, Environment.ProcessorCount * 2);
        var runs = new (SortKey[] Keys, (ScheduleSeries, TimeSlot)[] Items)[partitions];
        Parallel.For(0, partitions, p => runs[p] = SortedRun(series, rank, p, partitions, fromUtc, toUtc));
        return Merge(runs);
    }

    private readonly record struct SortKey(long Ticks, int Rank) : IComparable<SortKey>
    {
        public int CompareTo(SortKey other) =>
            Ticks != other.Ticks ? Ticks.CompareTo(other.Ticks) : Rank.CompareTo(other.Rank);
    }

    private static (SortKey[] Keys, (ScheduleSeries, TimeSlot)[] Items) SortedRun(
        IReadOnlyList<ScheduleSeries> series, int[] rank, int first, int stride, DateTime fromUtc, DateTime toUtc)
    {
        var keys = new List<SortKey>();
        var items = new List<(ScheduleSeries, TimeSlot)>();
        for (var i = first; i < series.Count; i += stride)
            foreach (var slot in series[i].SlotsOverlapping(fromUtc, toUtc))
            {
                keys.Add(new SortKey(slot.StartUtc.Ticks, rank[i]));
                items.Add((series[i], slot));
            }
        var k = keys.ToArray();
        var v = items.ToArray();
        Array.Sort(k, v);
        return (k, v);
    }

    private static List<(ScheduleSeries Series, TimeSlot Slot)> Merge((SortKey[] Keys, (ScheduleSeries, TimeSlot)[] Items)[] runs)
    {
        var merged = new List<(ScheduleSeries, TimeSlot)>(runs.Sum(r => r.Items.Length));
        var heads = new PriorityQueue<int, SortKey>(runs.Length);
        var positions = new int[runs.Length];
        for (var r = 0; r < runs.Length; r++)
            if (runs[r].Keys.Length > 0) heads.Enqueue(r, runs[r].Keys[0]);
        while (heads.TryDequeue(out var r, out _))
        {
            merged.Add(runs[r].Items[positions[r]]);
            if (++positions[r] < runs[r].Keys.Length) heads.Enqueue(r, runs[r].Keys[positions[r]]);
        }
        return merged;
    }
}
