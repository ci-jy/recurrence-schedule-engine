using System.Globalization;
using System.Text.Json;
using Rse.Engine;

namespace Rse.Bench;

public sealed record SeedSeries(string Title, string Resource, string RRule, string DtStart, string TimeZone, int DurationMinutes, List<string> ExDates);

public static class ClinicData
{
    public static string FindRepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    public static List<SeedSeries> LoadSeed() =>
        JsonSerializer.Deserialize<List<SeedSeries>>(File.ReadAllText(FindRepoFile("data/clinic_schedule.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    /// <summary>The clinic schedule, replicated <paramref name="copies"/> times (each copy shifted by a day).</summary>
    public static List<ScheduleSeries> Load(int copies = 1)
    {
        var seed = LoadSeed();
        var list = new List<ScheduleSeries>(seed.Count * copies);
        for (var c = 0; c < copies; c++)
            for (var i = 0; i < seed.Count; i++)
            {
                var s = seed[i];
                var start = Parse(s.DtStart).AddDays(c);
                var set = new RecurrenceSet(s.RRule, start, s.TimeZone, s.ExDates.Select(d => Parse(d).AddDays(c)));
                list.Add(new ScheduleSeries($"{c}-{i}", s.Title, $"{s.Resource} #{c}", set, TimeSpan.FromMinutes(s.DurationMinutes)));
            }
        return list;
    }

    private static DateTime Parse(string s) => DateTime.ParseExact(s, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
