using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rse.Bench;

/// <summary>Measures end-to-end latency of POST /api/conflicts/check against a running API.</summary>
public static class LatencyProbe
{
    public static async Task<int> Run(string baseUrl, int requests, string output)
    {
        var resources = ClinicData.LoadSeed().Select(s => s.Resource).Distinct().Order().ToList();
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var rng = new Random(7);
        string[] patterns = ["FREQ=WEEKLY;BYDAY=MO,WE", "FREQ=WEEKLY;INTERVAL=2;BYDAY=TH", "FREQ=MONTHLY;BYDAY=2TU", "FREQ=DAILY"];

        object Request(int i) => new
        {
            series = new
            {
                title = $"Probe {i}",
                resource = resources[rng.Next(resources.Count)],
                rrule = patterns[rng.Next(patterns.Length)],
                dtStart = $"2026-01-{rng.Next(1, 28):00}T{rng.Next(7, 18):00}:{rng.Next(0, 4) * 15:00}:00",
                timeZone = rng.Next(2) == 0 ? "America/Toronto" : "Europe/London",
                durationMinutes = 60,
            },
            from = "2026-01-01T00:00:00Z",
            to = "2026-07-01T00:00:00Z",
        };

        for (var i = 0; i < 30; i++) (await http.PostAsJsonAsync("/api/conflicts/check", Request(i))).EnsureSuccessStatusCode();

        var samples = new List<double>(requests);
        long clashes = 0;
        for (var i = 0; i < requests; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await http.PostAsJsonAsync("/api/conflicts/check", Request(i));
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
            clashes += body.GetProperty("totalClashes").GetInt32();
        }
        samples.Sort();
        double P(double q) => samples[(int)Math.Min(samples.Count - 1, Math.Ceiling(q * samples.Count) - 1)];
        var result = new
        {
            endpoint = "POST /api/conflicts/check",
            window_days = 181,
            requests,
            p50_ms = Math.Round(P(0.50), 2),
            p95_ms = Math.Round(P(0.95), 2),
            p99_ms = Math.Round(P(0.99), 2),
            mean_ms = Math.Round(samples.Average(), 2),
            total_clashes_found = clashes,
        };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(output, json + "\n");
        Console.WriteLine(json);
        return 0;
    }
}
