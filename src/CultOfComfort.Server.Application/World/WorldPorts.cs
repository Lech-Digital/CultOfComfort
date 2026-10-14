using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;

namespace CultOfComfort.Server.Application.World;

/// <summary>A shared world as stored: its seed (the map is generated from it) and its ledger, with a version for the optimistic check.</summary>
public sealed record WorldRecord(int Id, ulong Seed, long Version, WorldLedgerData Ledger, DateTimeOffset CreatedAtUtc);

/// <summary>Outbound port: the shared worlds, each stored as one document.</summary>
public interface IWorldRepository
{
    Task<IReadOnlyList<WorldRecord>> ListAsync(CancellationToken ct);

    Task AddAsync(WorldRecord world, CancellationToken ct);

    /// <summary>Replaces the world only if it is still at <paramref name="expectedVersion"/>; false when someone else wrote first.</summary>
    Task<bool> TryReplaceAsync(WorldRecord world, long expectedVersion, CancellationToken ct);
}

public sealed class WorldOptions
{
    public const string SectionName = "World";

    /// <summary>World 1's seed; World n uses Seed + n - 1.</summary>
    public ulong Seed { get; set; } = 20261014;

    /// <summary>How often the world tick runs (sermons ending, decay, bots, Tension pushed to villages).</summary>
    public int TickSeconds { get; set; } = 30;

    /// <summary>Bot cults a new world opens with (spec: 80), and the number of active cults bots make way for humans down to (150).</summary>
    public int Bots { get; set; } = 80;

    public int ActiveCults { get; set; } = 150;

    /// <summary>The window GET /api/world returns at most (hexes around the centre).</summary>
    public int MaxViewRadius { get; set; } = 14;
}

/// <summary>Your place in a world and the window around it, as the API returns it.</summary>
public sealed record WorldWindow(
    int WorldId,
    ulong Seed,
    WorldCult Me,
    int ViewQ,
    int ViewR,
    int ViewRadius,
    IReadOnlyList<WorldCult> Cults,
    IReadOnlyList<(int NodeId, string CultId, long Milli, bool Patron)> Holdings,
    ResourceBag Purse,
    ResourceBag OfferingsPerHour,
    int TensionPerHourMilli,
    DateTimeOffset ServerTimeUtc);

/// <summary>What a collect from the world's purse did: what reached the stores, or why nothing did; the village after.</summary>
public sealed record WorldCollect(ResourceBag Taken, string? Reason, VillageView? Village);

/// <summary>Inbound port: the shared world (spec 2026-10-14 shared world).</summary>
public interface IWorldService
{
    /// <summary>Puts the player's village on a world (the first one with room); does nothing if it is on one already. Null: no village, or every world is full.</summary>
    Task<WorldCult?> JoinAsync(Guid playerId, CancellationToken ct);

    /// <summary>The window around (q, r) (default: your village); null if the player isn't on a world.</summary>
    Task<WorldWindow?> WindowAsync(Guid playerId, int? q, int? r, int radius, CancellationToken ct);

    Task<WorldCollect?> CollectAsync(Guid playerId, CancellationToken ct);

    /// <summary>Why the village may not send this missionary (the place and distance are checked against the world), or null.</summary>
    Task<string?> MissionaryProblemAsync(Guid playerId, SendWorldMissionary send, CancellationToken ct);

    /// <summary>The village sent it (the command was accepted and stored): the world records the sermon.</summary>
    Task MissionarySentAsync(Guid playerId, BaseData village, SendWorldMissionary send, DateTimeOffset departedAt, CancellationToken ct);

    /// <summary>Runs every world up to now: sermons ending, decay, bots; pushes new Tension rates to the villages.</summary>
    Task TickAsync(CancellationToken ct);
}
