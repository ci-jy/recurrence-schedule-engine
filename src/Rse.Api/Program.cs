using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rse.Api;
using Rse.Api.Data;
using Rse.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ScheduleDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Schedule")));
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration["Seed:File"] is { Length: > 0 } seedFile && !await db.Series.AnyAsync())
    {
        var requests = JsonSerializer.Deserialize<List<SeriesRequest>>(await File.ReadAllTextAsync(seedFile), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        foreach (var r in requests)
        {
            if (SeriesMapping.Validate(r, out var s) is string error) throw new InvalidOperationException($"Bad seed series '{r.Title}': {error}");
            db.Series.Add(SeriesMapping.ToEntity(r, s!));
        }
        await db.SaveChangesAsync();
        app.Logger.LogInformation("Seeded {Count} series from {File}", requests.Count, seedFile);
    }
}

var maxWindow = TimeSpan.FromDays(366);
var api = app.MapGroup("/api");

api.MapPost("/series", async (SeriesRequest request, ScheduleDbContext db) =>
{
    if (SeriesMapping.Validate(request, out var series) is string error)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["series"] = [error] });
    var entity = SeriesMapping.ToEntity(request, series!);
    db.Series.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/api/series/{entity.Id}", SeriesResponse.From(entity));
});

api.MapGet("/series", async (string? resource, ScheduleDbContext db) =>
{
    var query = db.Series.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(resource)) query = query.Where(s => s.Resource == resource);
    var list = await query.OrderBy(s => s.Resource).ThenBy(s => s.Title).ToListAsync();
    return Results.Ok(list.Select(SeriesResponse.From));
});

api.MapGet("/series/{id:guid}", async (Guid id, ScheduleDbContext db) =>
    await db.Series.FindAsync(id) is { } e ? Results.Ok(SeriesResponse.From(e)) : Results.NotFound());

api.MapDelete("/series/{id:guid}", async (Guid id, ScheduleDbContext db) =>
    await db.Series.Where(s => s.Id == id).ExecuteDeleteAsync() == 1 ? Results.NoContent() : Results.NotFound());

api.MapGet("/occurrences", async (DateTimeOffset from, DateTimeOffset to, string? resource, ScheduleDbContext db) =>
{
    if (to <= from || to - from > maxWindow)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["window"] = ["'to' must be after 'from' and within 366 days"] });
    var query = db.Series.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(resource)) query = query.Where(s => s.Resource == resource);
    var entities = await query.ToListAsync();
    var byId = entities.ToDictionary(e => e.Id.ToString());
    var expanded = ScheduleExpander.Expand(entities.Select(SeriesMapping.ToEngine).ToList(), from.UtcDateTime, to.UtcDateTime, parallel: true);
    return Results.Ok(expanded.Select(p => new OccurrenceResponse(
        Guid.Parse(p.Series.Id), p.Series.Title, p.Series.Resource, p.Slot.StartUtc, p.Slot.EndUtc, p.Slot.LocalStart,
        byId[p.Series.Id].TimeZone)));
});

api.MapPost("/conflicts/check", async (ConflictCheckRequest request, ScheduleDbContext db) =>
{
    if (request.To <= request.From || request.To - request.From > maxWindow)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["window"] = ["'to' must be after 'from' and within 366 days"] });
    if (SeriesMapping.Validate(request.Series, out var proposed) is string error)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["series"] = [error] });

    var query = db.Series.AsNoTracking();
    if (proposed!.Resource is { } res) query = query.Where(s => s.Resource == res);
    var entities = await query.ToListAsync();
    var byId = entities.ToDictionary(e => e.Id.ToString());
    var result = ConflictDetector.Check(proposed, entities.Select(SeriesMapping.ToEngine),
        request.From.UtcDateTime, request.To.UtcDateTime, Math.Clamp(request.MaxPerSeries ?? 50, 1, 1000));

    var conflicts = result.Select(c => new SeriesConflictResponse(
        Guid.Parse(c.Existing.Id), c.Existing.Title, c.Existing.Resource, byId[c.Existing.Id].RRule, byId[c.Existing.Id].TimeZone,
        c.Conflicts.Select(x => new ClashResponse(x.First.StartUtc, x.Second.StartUtc, x.OverlapStartUtc, x.OverlapEndUtc)).ToList()))
        .ToList();
    return Results.Ok(new ConflictCheckResponse(entities.Count, conflicts.Count, conflicts.Sum(c => c.Clashes.Count), conflicts));
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
