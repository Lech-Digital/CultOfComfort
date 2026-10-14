using System.Text.Json;
using CultOfComfort.Contracts;
using CultOfComfort.Server.Application;
using CultOfComfort.Server.Application.World;
using CultOfComfort.Simulation.World;
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

    public async Task SetTokenHashAsync(Guid playerId, string tokenHash, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Players.Where(p => p.Id == playerId).ExecuteUpdateAsync(set => set.SetProperty(p => p.TokenHash, tokenHash), ct);
    }

    public async Task SetRecoveryCodeHashAsync(Guid playerId, string codeHash, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        await db.Players.Where(p => p.Id == playerId).ExecuteUpdateAsync(set => set.SetProperty(p => p.RecoveryCodeHash, codeHash), ct);
    }

    public async Task<Guid?> FindByRecoveryCodeHashAsync(string codeHash, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Players.Where(p => p.RecoveryCodeHash == codeHash).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> FindByGoogleSubjectAsync(string subject, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Players.Where(p => p.GoogleSubject == subject).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> LinkGoogleSubjectAsync(Guid playerId, string subject, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (await db.Players.AnyAsync(p => p.GoogleSubject == subject, ct))
        {
            return false;
        }

        var changed = await db.Players.Where(p => p.Id == playerId && p.GoogleSubject == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.GoogleSubject, subject), ct);
        return changed == 1;
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

internal sealed class SqliteWorldRepository(IDbContextFactory<ServerDbContext> contexts, TimeProvider clock) : IWorldRepository
{
    public async Task<IReadOnlyList<WorldRecord>> ListAsync(CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.Worlds.OrderBy(w => w.Id).ToListAsync(ct);
        return rows.Select(w => new WorldRecord(w.Id, unchecked((ulong)w.Seed), w.Version, WorldJson.Read(w.Json), w.CreatedAtUtc)).ToList();
    }

    public async Task AddAsync(WorldRecord world, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        db.Worlds.Add(new WorldRow
        {
            Id = world.Id, Seed = unchecked((long)world.Seed), Version = world.Version, Json = WorldJson.Write(world.Ledger),
            CreatedAtUtc = world.CreatedAtUtc, UpdatedAtUtc = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryReplaceAsync(WorldRecord world, long expectedVersion, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var json = WorldJson.Write(world.Ledger);
        var now = clock.GetUtcNow();
        var changed = await db.Worlds
            .Where(w => w.Id == world.Id && w.Version == expectedVersion)
            .ExecuteUpdateAsync(set => set.SetProperty(w => w.Json, json).SetProperty(w => w.Version, world.Version).SetProperty(w => w.UpdatedAtUtc, now), ct);
        return changed == 1;
    }
}

/// <summary>A world's ledger as stored (server only: the client never sees the whole ledger).</summary>
public static class WorldJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Write(WorldLedgerData ledger) => JsonSerializer.Serialize(ledger, Options);

    public static WorldLedgerData Read(string json) => JsonSerializer.Deserialize<WorldLedgerData>(json, Options)
        ?? throw new InvalidDataException("empty world document");
}

/// <summary>The village document as stored: the same JSON the device saves (shared <see cref="VillageSaveDto"/>).</summary>
public static class VillageJson
{
    public static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static string Write(BaseData village) => JsonSerializer.Serialize(VillageSaveMapping.ToDto(village), Options);

    public static BaseData Read(string json) => VillageSaveMapping.FromDto(JsonSerializer.Deserialize<VillageSaveDto>(json, Options));
}
