using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Rse.Api.Tests;

/// <summary>Starts the API against an empty database with the clinic seed file, as docker compose does.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SeedTests(PostgresFixture db)
{
    [Fact]
    public async Task SeedsClinicScheduleIntoEmptyDatabase()
    {
        var name = "rse_seed_" + Guid.NewGuid().ToString("N")[..10];
        var admin = new NpgsqlConnectionStringBuilder(db.ConnectionString) { Database = "postgres" }.ConnectionString;
        await using (var conn = new NpgsqlConnection(admin))
        {
            await conn.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {name}", conn).ExecuteNonQueryAsync();
        }
        try
        {
            var cs = new NpgsqlConnectionStringBuilder(db.ConnectionString) { Database = name }.ConnectionString;
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Schedule", cs);
                b.UseSetting("Seed:File", Path.Combine(AppContext.BaseDirectory, "clinic_schedule.json"));
            });
            using var client = factory.CreateClient();
            var all = await client.GetFromJsonAsync<List<SeriesResponse>>("/api/series");
            Assert.InRange(all!.Count, 450, 550);

            var week = await client.GetFromJsonAsync<List<OccurrenceResponse>>(
                "/api/occurrences?from=2026-03-02T00:00:00Z&to=2026-03-09T00:00:00Z");
            Assert.True(week!.Count > 500, $"expected a busy clinic week, got {week.Count}");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var conn = new NpgsqlConnection(admin);
            await conn.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", conn).ExecuteNonQueryAsync();
        }
    }
}
