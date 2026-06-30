using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Rse.Bench;

// dotnet run -c Release --project bench/Rse.Bench                     -> BenchmarkDotNet suites
// dotnet run -c Release --project bench/Rse.Bench -- latency [url] [n] -> API conflict-check latency
var resultsDir = Path.Combine(Path.GetDirectoryName(ClinicData.FindRepoFile("data/clinic_schedule.json"))!, "..", "bench", "results");
Directory.CreateDirectory(resultsDir);

if (args.Length > 0 && args[0] == "latency")
{
    var url = args.Length > 1 ? args[1] : "http://localhost:25080";
    var n = args.Length > 2 ? int.Parse(args[2]) : 500;
    return await LatencyProbe.Run(url, n, Path.Combine(resultsDir, "latency.json"));
}

// Occurrence counts per workload, so throughput (occurrences/s) can be derived from the timings.
var counts = new Dictionary<string, int>();
foreach (var copies in new[] { 1, 10 })
{
    var series = ClinicData.Load(copies);
    counts[(504 * copies).ToString()] = Rse.Engine.ScheduleExpander.Expand(series, ExpansionBenchmarks.From, ExpansionBenchmarks.To, false).Count;
}
File.WriteAllText(Path.Combine(resultsDir, "occurrence_counts.json"), JsonSerializer.Serialize(counts) + "\n");

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun)
    .AddExporter(CsvExporter.Default)
    .WithArtifactsPath(Path.Combine(resultsDir, "..", "BenchmarkDotNet.Artifacts"));
var filter = args.Length > 0 ? args : ["--filter", "*"];
BenchmarkSwitcher.FromTypes([typeof(ExpansionBenchmarks), typeof(ConflictBenchmarks)]).Run(filter, config);
return 0;
