using Rse.Api.Data;
using Rse.Engine;

namespace Rse.Api;

public sealed record SeriesRequest(
    string Title,
    string? Resource,
    string RRule,
    DateTime DtStart,
    string TimeZone,
    int DurationMinutes,
    List<DateTime>? ExDates);

public sealed record SeriesResponse(
    Guid Id, string Title, string? Resource, string RRule, DateTime DtStart, string TimeZone, int DurationMinutes, List<DateTime> ExDates)
{
    public static SeriesResponse From(SeriesEntity e) =>
        new(e.Id, e.Title, e.Resource, e.RRule, e.DtStart, e.TimeZone, e.DurationMinutes, e.ExDates);
}

public sealed record OccurrenceResponse(
    Guid SeriesId, string Title, string? Resource, DateTime StartUtc, DateTime EndUtc, DateTime LocalStart, string TimeZone);

public sealed record ConflictCheckRequest(SeriesRequest Series, DateTimeOffset From, DateTimeOffset To, int? MaxPerSeries);

public sealed record ClashResponse(DateTime ProposedStartUtc, DateTime ExistingStartUtc, DateTime OverlapStartUtc, DateTime OverlapEndUtc);

public sealed record SeriesConflictResponse(Guid SeriesId, string Title, string? Resource, string RRule, string TimeZone, List<ClashResponse> Clashes);

public sealed record ConflictCheckResponse(int SeriesChecked, int ConflictingSeries, int TotalClashes, List<SeriesConflictResponse> Conflicts);

public static class SeriesMapping
{
    /// <summary>Validates a request and builds the engine's view of it. Returns an error message on failure.</summary>
    public static string? Validate(SeriesRequest r, out ScheduleSeries? series, string id = "proposed")
    {
        series = null;
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 200) return "title is required (max 200 characters)";
        if (r.Resource is { Length: > 100 }) return "resource must be at most 100 characters";
        if (r.DurationMinutes is < 1 or > 24 * 60) return "durationMinutes must be between 1 and 1440";
        if (r.DtStart.Kind != DateTimeKind.Unspecified)
            return "dtStart must be a local wall-clock time without a UTC offset (it is interpreted in timeZone)";
        if (r.ExDates?.Any(d => d.Kind != DateTimeKind.Unspecified) == true)
            return "exDates must be local wall-clock times without a UTC offset";
        try
        {
            var set = new RecurrenceSet(r.RRule, r.DtStart, r.TimeZone, r.ExDates);
            series = new ScheduleSeries(id, r.Title, r.Resource, set, TimeSpan.FromMinutes(r.DurationMinutes));
            return null;
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return e.Message;
        }
    }

    public static ScheduleSeries ToEngine(SeriesEntity e) =>
        new(e.Id.ToString(), e.Title, e.Resource,
            new RecurrenceSet(e.RRule, e.DtStart, e.TimeZone, e.ExDates), TimeSpan.FromMinutes(e.DurationMinutes));

    public static SeriesEntity ToEntity(SeriesRequest r, ScheduleSeries s) => new()
    {
        Id = Guid.NewGuid(),
        Title = r.Title.Trim(),
        Resource = r.Resource?.Trim(),
        RRule = s.Recurrence.Rule.ToString(),
        DtStart = DateTime.SpecifyKind(r.DtStart, DateTimeKind.Unspecified),
        TimeZone = r.TimeZone,
        DurationMinutes = r.DurationMinutes,
        ExDates = (r.ExDates ?? []).Select(d => DateTime.SpecifyKind(d, DateTimeKind.Unspecified)).ToList(),
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
