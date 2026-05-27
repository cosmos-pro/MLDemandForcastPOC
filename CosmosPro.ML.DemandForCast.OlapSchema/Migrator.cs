using System.Reflection;
using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using Microsoft.Extensions.Logging;

namespace CosmosPro.ML.DemandForCast.OlapSchema;

internal sealed class Migrator(ClickHouseDataSource dataSource, ILogger<Migrator> logger)
{
    private const string MigrationsTable = "__schema_migrations";
    private const string ScriptsResourcePrefix = "CosmosPro.ML.DemandForCast.OlapSchema.Scripts.";

    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var connection = dataSource.CreateConnection();
        await connection.OpenAsync(ct);

        await EnsureMigrationsTableAsync(connection, ct);
        var applied = await GetAppliedVersionsAsync(connection, ct);
        var scripts = LoadEmbeddedScripts();
        var pending = scripts.Where(s => !applied.Contains(s.Version))
                             .OrderBy(s => s.Version, StringComparer.Ordinal)
                             .ToList();

        if (scripts.Count == 0)
        {
            logger.LogInformation("No OLAP migration scripts found. Skipping.");
            return;
        }

        if (pending.Count == 0)
        {
            logger.LogInformation("OLAP schema up-to-date ({Applied}/{Total} scripts applied).", applied.Count, scripts.Count);
            return;
        }

        logger.LogInformation("Applying {Pending} pending OLAP migration(s).", pending.Count);
        foreach (var script in pending)
        {
            logger.LogInformation("Applying {Version}.", script.Version);
            foreach (var statement in SplitStatements(script.Sql))
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = statement;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await RecordAppliedAsync(connection, script.Version, ct);
        }
        logger.LogInformation("OLAP schema migrations completed ({Applied} total applied).", applied.Count + pending.Count);
    }

    private async Task EnsureMigrationsTableAsync(ClickHouseConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {MigrationsTable}
            (
                version    String,
                applied_at DateTime DEFAULT now()
            )
            ENGINE = MergeTree
            ORDER BY version
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<HashSet<string>> GetAppliedVersionsAsync(ClickHouseConnection connection, CancellationToken ct)
    {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT version FROM {MigrationsTable}";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            applied.Add(reader.GetString(0));
        }
        return applied;
    }

    private async Task RecordAppliedAsync(ClickHouseConnection connection, string version, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {MigrationsTable} (version) VALUES ({{version:String}})";
        cmd.Parameters.Add(new ClickHouseDbParameter { ParameterName = "version", Value = version });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IReadOnlyList<Script> LoadEmbeddedScripts()
    {
        var assembly = typeof(Migrator).Assembly;
        return assembly.GetManifestResourceNames()
                       .Where(n => n.StartsWith(ScriptsResourcePrefix, StringComparison.Ordinal)
                                   && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                       .Select(n =>
                       {
                           var versionWithExt = n[ScriptsResourcePrefix.Length..];
                           var version = Path.GetFileNameWithoutExtension(versionWithExt);
                           using var stream = assembly.GetManifestResourceStream(n)!;
                           using var reader = new StreamReader(stream);
                           return new Script(version, reader.ReadToEnd());
                       })
                       .OrderBy(s => s.Version, StringComparer.Ordinal)
                       .ToList();
    }

    // Splits SQL on `;` at end-of-line. Multi-statement files devem ter cada
    // statement em sua própria linha terminando em `;`. Edge case conhecido:
    // `;` no final de uma linha dentro de string literal vai quebrar — para
    // POC, basta evitar; se virar problema, trocar por parser real.
    internal static IEnumerable<string> SplitStatements(string sql)
    {
        var statements = sql.Split(";\r\n", StringSplitOptions.RemoveEmptyEntries)
                            .SelectMany(s => s.Split(";\n", StringSplitOptions.RemoveEmptyEntries));
        foreach (var raw in statements)
        {
            var trimmed = raw.Trim().TrimEnd(';').Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private sealed record Script(string Version, string Sql);
}
