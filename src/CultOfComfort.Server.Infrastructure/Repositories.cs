using System.Text.Json;
using CultOfComfort.Contracts;
using CultOfComfort.Server.Application;
using CultOfComfort.Simulation.Base;
using Microsoft.EntityFrameworkCore;

namespace CultOfComfort.Server.Infrastructure;

internal sealed class SqlitePlayerRepository(IDbContextFactory<ServerDbContext> contexts) : IPlayerRepository
{
    public async Task AddAsync(Player player, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        db.Players.Add(new PlayerRow { Id = player.Id, TokenHash = player.TokenHash, CreatedAtUtc = player.CreatedAtUtc });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Players.Where(p => p.TokenHash == tokenHash).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
    }
}

internal sealed class SqliteVillageRepository(IDbContextFactory<ServerDbContext> contexts, TimeProvider clock) : IVillageRepository
{
    public async Task<BaseData?> FindAsync(Guid playerId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var json = await db.Villages.Where(v => v.PlayerId == playerId).Select(v => v.Json).FirstOrDefaultAsync(ct);
        return json == null ? null : VillageJson.Read(json);
    }

    public async Task AddAsync(Guid playerId, BaseData village, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        db.Villages.Add(new VillageRow { PlayerId = playerId, Version = village.Version, Json = VillageJson.Write(village), UpdatedAtUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryReplaceAsync(Guid playerId, long expectedVersion, BaseData village, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var json = VillageJson.Write(village);
        var now = clock.GetUtcNow();
        // One statement: the version check and the write can't be split by a concurrent request.
        var changed = await db.Villages
            .Where(v => v.PlayerId == playerId && v.Version == expectedVersion)
            .ExecuteUpdateAsync(set => set.SetProperty(v => v.Json, json).SetProperty(v => v.Version, village.Version).SetProperty(v => v.UpdatedAtUtc, now), ct);
        return changed == 1;
    }
}

/// <summary>The village document as stored: the same JSON the device saves (shared <see cref="VillageSaveDto"/>).</summary>
public static class VillageJson
{
    public static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static string Write(BaseData village) => JsonSerializer.Serialize(VillageSaveMapping.ToDto(village), Options);

    public static BaseData Read(string json) => VillageSaveMapping.FromDto(JsonSerializer.Deserialize<VillageSaveDto>(json, Options));
}
