using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace Rse.Api.Tests;

/// <summary>
/// Provides a PostgreSQL database for integration tests, in order of preference:
/// <list type="number">
/// <item>the connection string in <c>RSE_TEST_PG</c>;</item>
/// <item>the docker compose database on localhost:25432;</item>
/// <item>a throwaway cluster created with the local <c>initdb</c>/<c>pg_ctl</c> binaries on a free port.</item>
/// </list>
/// Each run gets its own freshly created database, which is dropped afterwards.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string ComposeServer = "Host=localhost;Port=25432;Username=schedule;Password=schedule;Timeout=3";
    private string? _dataDir;
    private string? _pgCtl;
    private string _server = "";
    private readonly string _database = "rse_test_" + Guid.NewGuid().ToString("N")[..12];

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _server = Environment.GetEnvironmentVariable("RSE_TEST_PG") is { Length: > 0 } env ? env
            : await CanConnect(ComposeServer) ? ComposeServer
            : await StartTemporaryCluster();

        await using (var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_server) { Database = "postgres" }.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {_database}", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        ConnectionString = new NpgsqlConnectionStringBuilder(_server) { Database = _database }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (_dataDir is not null)
        {
            Run(_pgCtl!, $"-D \"{_dataDir}\" -m immediate -w stop");
            Directory.Delete(_dataDir, recursive: true);
            return;
        }
        await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_server) { Database = "postgres" }.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> CanConnect(string cs)
    {
        try
        {
            await using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(cs) { Database = "postgres" }.ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch (Exception e) when (e is NpgsqlException or SocketException or TimeoutException)
        {
            return false;
        }
    }

    private async Task<string> StartTemporaryCluster()
    {
        var binDir = FindPostgresBin() ?? throw new InvalidOperationException(
            "No PostgreSQL available: set RSE_TEST_PG, run `docker compose up -d db`, or install PostgreSQL server binaries (initdb, pg_ctl).");
        _pgCtl = Path.Combine(binDir, "pg_ctl");
        _dataDir = Path.Combine(AppContext.BaseDirectory, "pgdata-" + Guid.NewGuid().ToString("N")[..8]);
        var port = FreePort();
        Run(Path.Combine(binDir, "initdb"), $"-D \"{_dataDir}\" -U schedule --auth=trust -E UTF8 --no-instructions");
        Run(_pgCtl, $"-D \"{_dataDir}\" -l \"{_dataDir}/server.log\" -w -t 60 " +
                    $"-o \"-p {port} -c listen_addresses=localhost -c unix_socket_directories='' -c fsync=off\" start");
        var cs = $"Host=localhost;Port={port};Username=schedule;Timeout=10";
        for (var i = 0; i < 50 && !await CanConnect(cs); i++) await Task.Delay(200);
        return cs;
    }

    private static string? FindPostgresBin()
    {
        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':')
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "initdb")) && File.Exists(Path.Combine(d, "pg_ctl")));
        if (onPath is not null) return onPath;
        const string root = "/usr/lib/postgresql";
        if (!Directory.Exists(root)) return null;
        return Directory.GetDirectories(root)
            .Select(d => Path.Combine(d, "bin"))
            .Where(d => File.Exists(Path.Combine(d, "initdb")))
            .OrderByDescending(d => int.TryParse(Path.GetFileName(Path.GetDirectoryName(d)), out var v) ? v : 0)
            .FirstOrDefault();
    }

    private static int FreePort()
    {
        var rng = new Random();
        for (var i = 0; i < 100; i++)
        {
            var port = rng.Next(20000, 30000);
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start();
                l.Stop();
                return port;
            }
            catch (SocketException)
            {
            }
        }
        throw new InvalidOperationException("No free port in 20000-29999");
    }

    private static void Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        var stderr = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(file)} failed: {stderr.Result}");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
