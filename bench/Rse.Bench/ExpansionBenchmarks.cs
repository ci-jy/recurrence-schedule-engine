using BenchmarkDotNet.Attributes;
using Rse.Engine;

namespace Rse.Bench;

/// <summary>Expands every series of the clinic schedule (optionally replicated) over one year.</summary>
[MemoryDiagnoser]
public class ExpansionBenchmarks
{
    public static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime To = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private List<ScheduleSeries> _series = [];

    [Params(504, 5040)]
    public int Series { get; set; }

    [GlobalSetup]
    public void Setup() => _series = ClinicData.Load(Series / 504);

    [Benchmark(Baseline = true)]
    public int SingleThreaded() => ScheduleExpander.Expand(_series, From, To, parallel: false).Count;

    [Benchmark]
    public int Parallel() => ScheduleExpander.Expand(_series, From, To, parallel: true).Count;
}

/// <summary>Checks a proposed series against every stored series over six months.</summary>
[MemoryDiagnoser]
public class ConflictBenchmarks
{
    private List<ScheduleSeries> _existing = [];
    private ScheduleSeries _proposed = null!;
    private readonly DateTime _from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _to = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [GlobalSetup]
    public void Setup()
    {
        _existing = ClinicData.Load();
        var set = new RecurrenceSet("FREQ=WEEKLY;BYDAY=MO,WE,FR", new DateTime(2026, 1, 5, 10, 0, 0), "America/Toronto");
        _proposed = new ScheduleSeries("proposed", "Proposed", null, set, TimeSpan.FromMinutes(60));
    }

    [Benchmark]
    public int AllSeriesSequential() => ConflictDetector.Check(_proposed, _existing, _from, _to, parallel: false).Count;

    [Benchmark]
    public int AllSeriesParallel() => ConflictDetector.Check(_proposed, _existing, _from, _to, parallel: true).Count;
}
