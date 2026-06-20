using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Rse.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests(PostgresFixture db)
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Schedule", db.ConnectionString));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static object Series(string title, string resource, string rrule, string dtStart, int minutes = 60,
        string tz = "America/Toronto", string[]? exDates = null) =>
        new { title, resource, rrule, dtStart, timeZone = tz, durationMinutes = minutes, exDates = exDates ?? [] };

    private async Task<SeriesResponse> Create(object series)
    {
        var resp = await _client.PostAsJsonAsync("/api/series", series);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<SeriesResponse>())!;
    }

    [Fact]
    public async Task CreateAndFetchSeries()
    {
        var room = Unique("room");
        var created = await Create(Series("Physio", room, "RRULE:freq=weekly;byday=MO,WE", "2025-01-06T09:00:00"));
        Assert.Equal("FREQ=WEEKLY;BYDAY=MO,WE", created.RRule);

        var fetched = await _client.GetFromJsonAsync<SeriesResponse>($"/api/series/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(new DateTime(2025, 1, 6, 9, 0, 0), fetched.DtStart);

        var list = await _client.GetFromJsonAsync<List<SeriesResponse>>($"/api/series?resource={room}");
        Assert.Single(list!);
    }

    [Theory]
    [InlineData("FREQ=SOMETIMES", "2025-01-06T09:00:00", "America/Toronto", 60)]
    [InlineData("FREQ=DAILY", "2025-01-06T09:00:00", "Nowhere/Special", 60)]
    [InlineData("FREQ=DAILY", "2025-01-06T09:00:00Z", "America/Toronto", 60)]
    [InlineData("FREQ=DAILY", "2025-01-06T09:00:00", "America/Toronto", 0)]
    public async Task InvalidSeriesIsRejected(string rrule, string dtStart, string tz, int minutes)
    {
        var resp = await _client.PostAsJsonAsync("/api/series", Series("Bad", "r", rrule, dtStart, minutes, tz));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task OccurrencesInWindowHonourExDatesAndDst()
    {
        var room = Unique("room");
        await Create(Series("Dialysis", room, "FREQ=WEEKLY;BYDAY=TU,TH", "2025-03-04T08:00:00", 240, exDates: ["2025-03-13T08:00:00"]));
        var occ = await _client.GetFromJsonAsync<List<OccurrenceResponse>>(
            $"/api/occurrences?from=2025-03-03T00:00:00Z&to=2025-03-17T00:00:00Z&resource={room}");
        Assert.Equal(3, occ!.Count); // Mar 4, 6, 11 (Mar 13 excluded)
        Assert.Equal(new DateTime(2025, 3, 4, 13, 0, 0, DateTimeKind.Utc), occ[0].StartUtc); // EST
        Assert.Equal(new DateTime(2025, 3, 11, 12, 0, 0, DateTimeKind.Utc), occ[2].StartUtc); // EDT after Mar 9
        Assert.All(occ, o => Assert.Equal(8, o.LocalStart.Hour));
    }

    [Fact]
    public async Task OccurrenceWindowIsValidated()
    {
        var resp = await _client.GetAsync("/api/occurrences?from=2025-03-03T00:00:00Z&to=2027-03-17T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ConflictCheckFindsClashesOnSameResourceOnly()
    {
        var room = Unique("room");
        var other = Unique("room");
        var weekly = await Create(Series("Diabetes clinic", room, "FREQ=WEEKLY;BYDAY=MO", "2025-01-06T09:00:00", 120));
        await Create(Series("Cardiology", room, "FREQ=WEEKLY;BYDAY=TU", "2025-01-07T09:00:00", 120));
        await Create(Series("Elsewhere", other, "FREQ=WEEKLY;BYDAY=MO", "2025-01-06T09:00:00", 120));

        var request = new
        {
            series = Series("Monthly review", room, "FREQ=MONTHLY;BYDAY=1MO", "2025-01-06T10:00:00", 60),
            from = "2025-01-01T00:00:00Z",
            to = "2025-07-01T00:00:00Z",
        };
        var resp = await _client.PostAsJsonAsync("/api/conflicts/check", request);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = (await resp.Content.ReadFromJsonAsync<ConflictCheckResponse>())!;
        Assert.Equal(2, result.SeriesChecked);
        var c = Assert.Single(result.Conflicts);
        Assert.Equal(weekly.Id, c.SeriesId);
        Assert.Equal(6, c.Clashes.Count); // first Monday of each month, Jan-Jun
        Assert.All(c.Clashes, x => Assert.Equal(TimeSpan.FromHours(1), x.OverlapEndUtc - x.OverlapStartUtc));
    }

    [Fact]
    public async Task ConflictCheckWithNoClashes()
    {
        var room = Unique("room");
        await Create(Series("Morning", room, "FREQ=DAILY", "2025-01-01T08:00:00", 60));
        var request = new
        {
            series = Series("Afternoon", room, "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", "2025-01-01T14:00:00", 60),
            from = "2025-01-01T00:00:00Z",
            to = "2025-12-31T00:00:00Z",
        };
        var result = (await (await _client.PostAsJsonAsync("/api/conflicts/check", request)).Content.ReadFromJsonAsync<ConflictCheckResponse>())!;
        Assert.Equal(1, result.SeriesChecked);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task DeleteSeries()
    {
        var created = await Create(Series("Temp", Unique("room"), "FREQ=DAILY;COUNT=3", "2025-01-01T08:00:00"));
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/series/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/series/{created.Id}")).StatusCode);
    }

    [Fact]
    public void SeededClinicScheduleIsValid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "clinic_schedule.json");
        var series = JsonSerializer.Deserialize<List<SeriesRequest>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.InRange(series.Count, 450, 550);
        foreach (var s in series)
            Assert.Null(SeriesMapping.Validate(s, out _));
    }
}
