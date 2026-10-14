using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using Microsoft.Extensions.Logging;

namespace CultOfComfort.Server.Application.World;

/// <summary>
/// The shared worlds on the server (spec 2026-10-14 shared world). Holds each world's ledger in memory behind one lock
/// (one server, a few hundred players per world), stores it after every change, and talks to villages only through
/// commands: <see cref="JoinWorld"/>, <see cref="WorldUpdate"/> and <see cref="ReceiveOfferings"/>.
/// </summary>
internal sealed class WorldService(
    IWorldRepository worlds,
    IVillageRepository villages,
    BuildingTable table,
    VillageMap villageMap,
    TimeProvider clock,
    WorldOptions options,
    ILogger<WorldService> log)
    : IWorldService
{
    private const int VillageRetries = 4;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, LoadedWorld> _loaded = new();
    private bool _ready;

    private DomainTable Domain => table.Domain;

    public async Task<WorldCult?> JoinAsync(Guid playerId, CancellationToken ct)
    {
        var village = await villages.FindAsync(playerId, ct);
        if (village == null)
        {
            return null;
        }

        var id = CultId(playerId);
        var now = clock.GetUtcNow();
        WorldCult? cult;
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            cult = Find(id)?.Ledger.Cult(id);
            if (cult == null)
            {
                foreach (var world in _loaded.Values.OrderBy(w => w.Record.Id).Append(null))
                {
                    var target = world ?? await CreateAsync(_loaded.Count + 1, now, ct);
                    if (target.Ledger.Humans >= target.Map.Shape.Capacity)
                    {
                        continue;
                    }

                    cult = target.Ledger.Join(id, string.Empty, isBot: false, PlacementSeed(playerId), now);
                    if (cult != null)
                    {
                        await SaveAsync(target, ct);
                        break;
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        if (cult == null)
        {
            return null;
        }

        if (village.Domain?.World == null)
        {
            var join = cult;
            await ApplyAsync(playerId, () => new JoinWorld(Guid.NewGuid(), join.Q, join.R), ct);
        }

        return cult;
    }

    public async Task<WorldWindow?> WindowAsync(Guid playerId, int? q, int? r, int radius, CancellationToken ct)
    {
        var id = CultId(playerId);
        var now = clock.GetUtcNow();
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            var world = Find(id);
            var me = world?.Ledger.Cult(id);
            if (world == null || me == null)
            {
                return null;
            }

            var (cq, cr) = (q ?? me.Q, r ?? me.R);
            var span = Math.Clamp(radius, 1, options.MaxViewRadius);
            var ledger = world.Ledger;
            var cults = ledger.Cults.Where(c => Hex.Distance(c.Q, c.R, cq, cr) <= span).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
            var holdings = world.Map.Near(cq, cr, span)
                .SelectMany(n =>
                {
                    var patron = ledger.PatronOf(n.Id);
                    return ledger.Holders(n.Id).Select(h => (n.Id, h.Key, h.Value, h.Key == patron));
                })
                .OrderBy(h => h.Item1).ThenBy(h => h.Item2, StringComparer.Ordinal)
                .ToList();
            return new WorldWindow(world.Record.Id, world.Record.Seed, me, cq, cr, span, cults, holdings,
                ledger.Purse(id, now), ledger.OfferingsPerHour(id), ledger.TensionPerHourMilli(id), now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorldCollect?> CollectAsync(Guid playerId, CancellationToken ct)
    {
        var id = CultId(playerId);
        var now = clock.GetUtcNow();
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            var world = Find(id);
            if (world == null)
            {
                return null;
            }

            var purse = world.Ledger.Purse(id, now);
            if (purse.IsZero)
            {
                return new WorldCollect(default, "No offerings yet. The faithful are thinking about it.", null);
            }

            var (outcome, village) = await ApplyAsync(playerId, () => new ReceiveOfferings(Guid.NewGuid(), purse), ct);
            if (outcome == null || village == null)
            {
                return null;
            }

            if (outcome.Status != CommandStatus.Accepted)
            {
                return new WorldCollect(default, outcome.Reason, new VillageView(village, now));
            }

            world.Ledger.Take(id, outcome.Collected, now); // what didn't fit stays in the purse
            await SaveAsync(world, ct);
            return new WorldCollect(outcome.Collected, null, new VillageView(village, now));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> MissionaryProblemAsync(Guid playerId, SendWorldMissionary send, CancellationToken ct)
    {
        var id = CultId(playerId);
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            var world = Find(id);
            var cult = world?.Ledger.Cult(id);
            var node = world?.Map.Node(send.NodeId);
            if (world == null || cult == null)
            {
                return "Your village isn't on a shared world.";
            }

            if (node == null || node.DistanceTo(cult.Q, cult.R) != send.Distance)
            {
                return "That place isn't where you think it is.";
            }

            return null; // reach, slots, villagers and provisions are the village's own rules
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MissionarySentAsync(Guid playerId, BaseData village, SendWorldMissionary send, DateTimeOffset departedAt, CancellationToken ct)
    {
        var id = CultId(playerId);
        var bonus = BaseState.Restore(table, villageMap, village).Snapshot(departedAt).Domain?.MissionaryBonusPercent ?? 0;
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            var world = Find(id);
            if (world?.Ledger.Cult(id) == null || world.Map.Node(send.NodeId) == null)
            {
                log.LogWarning("A world missionary from {Player} went nowhere: not on a world", playerId);
                return;
            }

            world.Ledger.Dispatch(id, send.NodeId, send.Hours, bonus, departedAt);
            await SaveAsync(world, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var pushes = new List<(Guid Player, int Rate, int Spike)>();
        await _gate.WaitAsync(ct);
        try
        {
            await LoadAsync(ct);
            foreach (var world in _loaded.Values.OrderBy(w => w.Record.Id))
            {
                var changes = world.Ledger.Advance(now);
                if (changes.Count == 0)
                {
                    continue;
                }

                await SaveAsync(world, ct);
                foreach (var change in changes)
                {
                    var cult = world.Ledger.Cult(change.CultId);
                    if (cult is { IsBot: false } && Guid.TryParseExact(cult.Id, "N", out var player))
                    {
                        pushes.Add((player, world.Ledger.TensionPerHourMilli(cult.Id), change.TensionSpike));
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        foreach (var (player, rate, spike) in pushes)
        {
            var village = await villages.FindAsync(player, ct);
            if (village?.Domain?.World == null || (spike == 0 && village.Domain.World.TensionPerHourMilli == rate))
            {
                continue; // nothing new for this village
            }

            await ApplyAsync(player, () => new WorldUpdate(Guid.NewGuid(), rate, spike), ct);
        }
    }

    // ---------------- helpers ----------------

    internal static string CultId(Guid playerId) => playerId.ToString("N");

    private static ulong PlacementSeed(Guid playerId) => BitConverter.ToUInt64(playerId.ToByteArray(), 0);

    private LoadedWorld? Find(string cultId) => _loaded.Values.FirstOrDefault(w => w.Ledger.Cult(cultId) != null);

    private async Task LoadAsync(CancellationToken ct)
    {
        if (_ready)
        {
            return;
        }

        foreach (var record in await worlds.ListAsync(ct))
        {
            var map = WorldGenerator.Generate(record.Seed);
            _loaded[record.Id] = new LoadedWorld(record, map, WorldLedger.Restore(map, Domain, record.Ledger));
        }

        _ready = true;
    }

    private async Task<LoadedWorld> CreateAsync(int id, DateTimeOffset now, CancellationToken ct)
    {
        var seed = options.Seed + (ulong)(id - 1);
        var map = WorldGenerator.Generate(seed);
        var ledger = new WorldLedger(map, Domain, now);
        var record = new WorldRecord(id, seed, 1, ledger.ToData(), now);
        await worlds.AddAsync(record, ct);
        var world = new LoadedWorld(record, map, ledger);
        _loaded[id] = world;
        log.LogInformation("World {World} opened (seed {Seed})", id, seed);
        return world;
    }

    private async Task SaveAsync(LoadedWorld world, CancellationToken ct)
    {
        var next = world.Record with { Version = world.Record.Version + 1, Ledger = world.Ledger.ToData() };
        if (!await worlds.TryReplaceAsync(next, world.Record.Version, ct))
        {
            // Someone else wrote this world (a second server?): forget the cache, the next call reloads it.
            _ready = false;
            _loaded.Clear();
            throw new InvalidOperationException($"World {world.Record.Id} changed underneath this server.");
        }

        world.Record = next;
    }

    /// <summary>Applies a server-only command to a village (retrying if the player's own sync wrote first).</summary>
    private async Task<(CommandOutcome? Outcome, BaseData? Village)> ApplyAsync(Guid playerId, Func<BaseCommand> command, CancellationToken ct)
    {
        for (var attempt = 0; attempt < VillageRetries; attempt++)
        {
            var stored = await villages.FindAsync(playerId, ct);
            if (stored == null)
            {
                return (null, null);
            }

            var state = BaseState.Restore(table, villageMap, stored);
            var outcome = state.Apply(command(), clock.GetUtcNow());
            if (outcome.Status != CommandStatus.Accepted)
            {
                return (outcome, stored);
            }

            var updated = state.ToData();
            if (await villages.TryReplaceAsync(playerId, stored.Version, updated, ct))
            {
                return (outcome, updated);
            }
        }

        log.LogWarning("Gave up applying a world command to {Player}'s village after {Tries} tries", playerId, VillageRetries);
        return (null, null);
    }

    private sealed class LoadedWorld(WorldRecord record, WorldMap map, WorldLedger ledger)
    {
        public WorldRecord Record { get; set; } = record;

        public WorldMap Map { get; } = map;

        public WorldLedger Ledger { get; } = ledger;
    }
}
