using CultOfComfort.Server.Application;
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
        return services;
    }

    /// <summary>Creates the tables on first start (one small schema; migrations come with the next module).</summary>
    public static async Task EnsureVillageStorageAsync(this IServiceProvider services, CancellationToken ct)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<ServerDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }
}
