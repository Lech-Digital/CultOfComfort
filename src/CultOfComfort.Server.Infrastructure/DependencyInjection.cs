using CultOfComfort.Server.Application;
using CultOfComfort.Server.Application.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CultOfComfort.Server.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQLite adapters for the Application ports.</summary>
    public static IServiceCollection AddVillageStorage(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton<IPlayerRepository, SqlitePlayerRepository>();
        services.AddSingleton<IVillageRepository, SqliteVillageRepository>();
        services.AddSingleton<IWorldRepository, SqliteWorldRepository>();
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
        return services;
    }

    /// <summary>Creates the tables on first start (one small schema; migrations come with the next module).</summary>
    public static async Task EnsureVillageStorageAsync(this IServiceProvider services, CancellationToken ct)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<ServerDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await UpgradeSchemaAsync(db, ct);
    }

    /// <summary>
    /// Adds what came after the first deploy to a database EnsureCreated made earlier (idempotent). A stop-gap until
    /// EF migrations arrive with the next module (ADR 0007).
    /// </summary>
    private static async Task UpgradeSchemaAsync(ServerDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(players);";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columns.Add(reader.GetString(1));
            }
        }

        // Literal statements: the names are fixed, nothing from outside goes into this SQL.
        if (!columns.Contains("RecoveryCodeHash"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE players ADD COLUMN \"RecoveryCodeHash\" TEXT NULL;", ct);
        }

        if (!columns.Contains("GoogleSubject"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE players ADD COLUMN \"GoogleSubject\" TEXT NULL;", ct);
        }

        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS worlds (\"Id\" INTEGER NOT NULL PRIMARY KEY, \"Seed\" INTEGER NOT NULL, \"Version\" INTEGER NOT NULL, " +
            "\"Json\" TEXT NOT NULL, \"CreatedAtUtc\" INTEGER NOT NULL, \"UpdatedAtUtc\" INTEGER NOT NULL);", ct);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_players_RecoveryCodeHash\" ON players (\"RecoveryCodeHash\");", ct);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_players_GoogleSubject\" ON players (\"GoogleSubject\");", ct);
    }
}
