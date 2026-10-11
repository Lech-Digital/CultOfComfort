using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>
/// The village aggregate (ADR 0003): resources are worked out on read from rates and timestamps, construction timers
/// resolve lazily, and the only way in is <see cref="Apply"/> with a command. Time is always passed in.
/// </summary>
public sealed class BaseState
{
    private const int RememberedCommands = 64;
    private const int HearthId = 0;

    private readonly BuildingTable _table;
    private readonly VillageMap _map;
    private readonly List<Building> _buildings = new();
    private readonly Queue<Guid> _recentOrder = new();
    private readonly HashSet<Guid> _recent = new();
    private ResourceBag _stock;
    private int _nextId = 1;
    private long _tensionMilli;
    private DateTimeOffset _tensionAsOf;
    private DateTimeOffset? _shieldEndsAt;
    private ScheduledRaid? _raid;
    private int _nextRaidId = 1;
    private NightPlan? _night;
    private DateTimeOffset _nightStartedAt;
    private NightReport? _unseenReport;
    private readonly List<GridPosition> _cleared = new(); // boulders, gone for good
    private readonly Dictionary<GridPosition, DateTimeOffset> _treeCuts = new();
    private DateTimeOffset _now;

    private BaseState(BuildingTable table, VillageMap map)
    {
        _table = table;
        _map = map;
    }

    public long Version { get; private set; }

    public int LastNightRecorded { get; private set; }

    public int HearthLevel => Hearth.Level;

    private Building Hearth => _buildings[0];

    /// <summary>What the builder is working on (saplings grow by themselves and don't count).</summary>
    private Building? Constructing => _buildings.FirstOrDefault(b => b.IsUpgrading && _table.Spec(b.Type).UsesBuilder);

    public static BaseState CreateNew(BuildingTable table, VillageMap map, DateTimeOffset nowUtc)
    {
        var state = new BaseState(table, map) { _stock = table.StartingStock, _tensionAsOf = nowUtc };
        state._buildings.Add(new Building(HearthId, BuildingType.Hearth, map.Hearth, 1, nowUtc));
        return state;
    }

    public static BaseState Restore(BuildingTable table, VillageMap map, BaseData data)
    {
        var hearths = data.Buildings.Count(b => b.Type == BuildingType.Hearth);
        if (hearths != 1)
        {
            throw new InvalidBaseDataException($"A village needs exactly one Hearth (found {hearths}).");
        }

        var state = new BaseState(table, map)
        {
            Version = data.Version,
            _stock = data.Stock,
            _nextId = data.NextId,
            LastNightRecorded = data.LastNightRecorded,
            _tensionMilli = data.TensionMilli,
            // A save from before Tension existed has no Tension clock: start it at the save's latest moment, not in year 1.
            _tensionAsOf = data.TensionAsOfUtcTicks > 0
                ? new DateTimeOffset(data.TensionAsOfUtcTicks, TimeSpan.Zero)
                : new DateTimeOffset(data.Buildings.Select(b => b.AsOfUtcTicks).DefaultIfEmpty(0).Max(), TimeSpan.Zero),
            _shieldEndsAt = data.ShieldEndsAtUtcTicks == null ? null : new DateTimeOffset(data.ShieldEndsAtUtcTicks.Value, TimeSpan.Zero),
            _nextRaidId = data.NextRaidId,
            _raid = data.Raid == null ? null : new ScheduledRaid(data.Raid.Id, data.Raid.Seed, new DateTimeOffset(data.Raid.WarningEndsAtUtcTicks, TimeSpan.Zero)),
            _night = data.NightInProgress,
            _nightStartedAt = new DateTimeOffset(data.NightStartedAtUtcTicks, TimeSpan.Zero),
            _unseenReport = data.UnseenReport,
        };
        foreach (var b in data.Buildings.OrderBy(b => b.Type == BuildingType.Hearth ? -1 : b.Id))
        {
            if (!table.Specs.TryGetValue(b.Type, out var spec) || b.Level < 0 || b.Level > spec.MaxLevel)
            {
                throw new InvalidBaseDataException($"Building {b.Id} has an impossible type or level.");
            }

            state._buildings.Add(new Building(b.Id, b.Type, new GridPosition(b.X, b.Y), b.Level, new DateTimeOffset(b.AsOfUtcTicks, TimeSpan.Zero))
            {
                BufferMilli = b.BufferMilli,
                UpgradeEndsAtUtc = b.UpgradeEndsAtUtcTicks == null ? null : new DateTimeOffset(b.UpgradeEndsAtUtcTicks.Value, TimeSpan.Zero),
                Wrecked = b.Wrecked,
            });
        }

        foreach (var id in data.RecentCommands)
        {
            state.Remember(id);
        }

        foreach (var tile in data.ClearedTerrain ?? Array.Empty<GridPosition>())
        {
            if (!state._cleared.Contains(tile))
            {
                state._cleared.Add(tile);
            }
        }

        foreach (var cut in data.TreeCuts ?? Array.Empty<TreeCutData>())
        {
            state._treeCuts[new GridPosition(cut.X, cut.Y)] = new DateTimeOffset(cut.CutAtUtcTicks, TimeSpan.Zero);
        }

        state.Validate();
        return state;
    }

    /// <summary>Rejects a save that no sequence of commands could have produced (hand-edited or corrupted).</summary>
    private void Validate()
    {
        void Fail(string reason) => throw new InvalidBaseDataException(reason);

        if (_tensionMilli < 0 || _tensionMilli > _table.Tension.Max * 1000L || _nextRaidId < 1 || (_raid != null && _raid.Id >= _nextRaidId))
        {
            Fail("impossible Tension or raid");
        }

        if (_stock.Timber < 0 || _stock.Peat < 0 || _stock.Scrap < 0 || _stock.Devotion < 0)
        {
            Fail("negative stock");
        }

        if (_buildings.Select(b => b.Id).Distinct().Count() != _buildings.Count || _buildings.Any(b => b.Id >= _nextId && b.Id != HearthId))
        {
            Fail("building ids collide");
        }

        if (Hearth.Position != _map.Hearth || Hearth.Level < 1)
        {
            Fail("the Hearth is not where (or what) it should be");
        }

        if (_buildings.Count(b => b.IsUpgrading && _table.Spec(b.Type).UsesBuilder) > 1)
        {
            Fail("two constructions at once");
        }

        var tiles = new HashSet<GridPosition>();
        foreach (var b in _buildings)
        {
            var spec = _table.Spec(b.Type);
            var minLevel = b.Type == BuildingType.Hearth || spec.IsTower ? 1 : 0;
            if (b.Level < minLevel || b.Level > spec.MaxLevel || b.BufferMilli < 0
                || (b.Level == 0 && !b.IsUpgrading) || (b.IsUpgrading && (spec.IsTower || b.Level >= spec.MaxLevel)))
            {
                Fail($"building {b.Id} is in an impossible state");
            }

            if (!tiles.Add(b.Position) || (b.Type != BuildingType.Hearth
                && (!_map.IsOpen(b.Position, HearthLevel) || b.Position == _map.Spawn || b.Position == _map.Hearth
                    || _map.IsBedrock(b.Position) || (_map.IsBoulder(b.Position) && !_cleared.Contains(b.Position)))))
            {
                Fail($"building {b.Id} stands somewhere it cannot");
            }
        }

        try
        {
            ToBattleLayout().Validate();
        }
        catch (InvalidLayoutException e)
        {
            Fail("the road is sealed: " + e.Message);
        }
    }

    public BaseData ToData() => new(
        Version, _stock, _nextId, LastNightRecorded,
        _buildings.Select(b => new BuildingData(b.Id, b.Type, b.Level, b.Position.X, b.Position.Y, b.UpgradeEndsAtUtc?.UtcTicks, b.BufferMilli, b.AsOfUtc.UtcTicks, b.Wrecked)).ToList(),
        _recentOrder.ToList(),
        _tensionMilli,
        _tensionAsOf.UtcTicks,
        _shieldEndsAt?.UtcTicks,
        _nextRaidId,
        _raid == null ? null : new RaidData(_raid.Id, _raid.Seed, _raid.WarningEndsAtUtc.UtcTicks),
        _night,
        _nightStartedAt.UtcTicks,
        _unseenReport,
        _cleared.ToList(),
        _treeCuts.Select(c => new TreeCutData(c.Key.X, c.Key.Y, c.Value.UtcTicks)).ToList());

    public CommandOutcome Apply(BaseCommand command, DateTimeOffset nowUtc)
    {
        if (_recent.Contains(command.CommandId))
        {
            return new CommandOutcome(CommandStatus.Duplicate, Version);
        }

        Advance(nowUtc);
        _now = nowUtc;
        if (_night != null && command is PlaceBuilding or UpgradeBuilding or UpgradeHearth or DemolishBuilding or RepairBuilding or FinishConstructionNow or RecordNightResult or ClearTerrain)
        {
            return CommandOutcome.Reject("Not now — there's a Night on.") with { Version = Version };
        }

        var outcome = command switch
        {
            BeginNight _ => Begin(nowUtc),
            ResolveNight resolve => Resolve(resolve, nowUtc),
            AcknowledgeReport _ => Acknowledge(),
            ClearTerrain clear => Clear(clear.Tile),
            PlaceBuilding place => Place(place, nowUtc),
            UpgradeBuilding upgrade => Upgrade(Find(upgrade.BuildingId), nowUtc),
            UpgradeHearth _ => Upgrade(Hearth, nowUtc),
            DemolishBuilding demolish => Demolish(demolish.BuildingId),
            Collect collect => DoCollect(collect.BuildingId, nowUtc),
            RecordNightResult night => RecordNight(night, nowUtc),
            FinishConstructionNow _ => FinishNow(nowUtc),
            DebugRaiseTension raise => RaiseTension(raise.Amount, nowUtc),
            DebugSkipWarning _ => SkipWarning(nowUtc),
            RepairBuilding repair => Repair(repair.BuildingId, nowUtc),
            _ => CommandOutcome.Reject("Unknown order."),
        };

        if (outcome.Status == CommandStatus.Accepted)
        {
            Version++;
            Remember(command.CommandId);
        }

        return outcome with { Version = Version };
    }

    public BaseSnapshot Snapshot(DateTimeOffset nowUtc)
    {
        Advance(nowUtc);
        _now = nowUtc;
        var views = _buildings.Select(b => new BuildingView(
            b.Id, b.Type, b.Level, b.Position, (int)(Buffer(b, nowUtc) / 1000), (int)(CapacityMilli(b) / 1000), Rate(b), b.UpgradeEndsAtUtc, b.Wrecked)).ToList();
        var tension = TensionValue(nowUtc);
        return new BaseSnapshot(Version, nowUtc, HearthLevel, _stock, Caps, views, SluiceBonus, LastNightRecorded,
            tension, _table.Tension.StateOf(tension), _raid?.WarningEndsAtUtc, _shieldEndsAt > nowUtc ? _shieldEndsAt : null, _night, _unseenReport, _cleared.Concat(_treeCuts.Keys.Where(t => !IsWildTreeStanding(t))).ToList());
    }

    public ResourceBag Caps
    {
        get
        {
            var cap = _table.HearthStorageCaps[HearthLevel - 1];
            foreach (var b in _buildings)
            {
                if (b.Level > 0 && !b.Wrecked)
                {
                    cap += _table.Spec(b.Type).Levels[b.Level - 1].StorageBonus;
                }
            }

            return new ResourceBag(cap, cap, cap, cap);
        }
    }

    public int SluiceBonus => _buildings.Where(b => b.Level > 0 && !b.Wrecked).Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].SluiceCharges);

    /// <summary>The locked layout for a Night: towers fight, every other building is in the way.</summary>
    public BattleLayout ToBattleLayout()
    {
        var buildingTiles = new HashSet<GridPosition>(_buildings.Select(b => b.Position));
        return _map.ToBattleLayout(
            _buildings.Where(b => _table.Spec(b.Type).IsTower).Select(b => new PlacedTower(_table.Spec(b.Type).Tower!.Value, b.Position)),
            _buildings.Where(b => b.Type != BuildingType.Hearth && !_table.Spec(b.Type).IsTower).Select(b => b.Position),
            _buildings.Where(b => b.Type != BuildingType.Hearth && _table.StructureHealth(b.Type, b.Level) > 0).Select(b =>
            {
                var health = _table.StructureHealth(b.Type, b.Level);
                return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health);
            }),
            HearthLevel,
            _cleared);
    }

    // (VillageMap.ToBattleLayout(snapshot, table) is the same rule from a snapshot, for gateways.)

    // ---------------- commands ----------------

    private CommandOutcome Place(PlaceBuilding command, DateTimeOffset now)
    {
        var spec = _table.Spec(command.Type);
        if (command.Type == BuildingType.Hearth)
        {
            return CommandOutcome.Reject("There is only one Hearth.");
        }

        if (HearthLevel < spec.UnlockHearth)
        {
            return CommandOutcome.Reject($"{spec.Name} unlocks at Hearth {spec.UnlockHearth}.");
        }

        if (_buildings.Count(b => b.Type == command.Type) >= spec.MaxCount(HearthLevel))
        {
            return CommandOutcome.Reject($"No room for another {spec.Name} until the Hearth grows.");
        }

        var instant = spec.IsTower || spec.Levels[0].BuildSeconds == 0;
        if (!instant && spec.UsesBuilder && Constructing != null)
        {
            return CommandOutcome.Reject("The builder is busy. One thing at a time; he's a cultist, not a crane.");
        }

        var reason = PlacementProblem(command.Position);
        if (reason != null)
        {
            return CommandOutcome.Reject(reason);
        }

        var cost = spec.Levels[0].Cost;
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        var building = new Building(_nextId++, command.Type, command.Position, instant ? 1 : 0, now);
        if (!instant)
        {
            building.UpgradeEndsAtUtc = now.AddSeconds(spec.Levels[0].BuildSeconds);
        }

        _buildings.Add(building);
        Advance(now); // zero-second builds complete at once
        return CommandOutcome.Accept();
    }

    private CommandOutcome Upgrade(Building? building, DateTimeOffset now)
    {
        if (building == null)
        {
            return CommandOutcome.Reject("That building is gone.");
        }

        var spec = _table.Spec(building.Type);
        if (spec.IsTower || building.Level >= spec.MaxLevel)
        {
            return CommandOutcome.Reject($"{spec.Name} is as good as it gets.");
        }

        if (building.Wrecked)
        {
            return CommandOutcome.Reject("Wrecked — repair it first.");
        }

        if (building.IsUpgrading || Constructing != null)
        {
            return CommandOutcome.Reject("The builder is busy. One thing at a time; he's a cultist, not a crane.");
        }

        if (building.Type != BuildingType.Hearth && building.Level + 1 > HearthLevel)
        {
            return CommandOutcome.Reject($"Grow the Hearth to level {building.Level + 1} first.");
        }

        var cost = spec.Levels[building.Level].Cost;
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        Materialize(building, now);
        building.UpgradeEndsAtUtc = now.AddSeconds(spec.Levels[building.Level].BuildSeconds);
        return CommandOutcome.Accept();
    }

    private CommandOutcome Demolish(int id)
    {
        var building = Find(id);
        if (building == null || building.Type == BuildingType.Hearth)
        {
            return CommandOutcome.Reject(building == null ? "That building is gone." : "Demolish the Hearth? The cult would riot.");
        }

        var spec = _table.Spec(building.Type);
        if (building.IsUpgrading && spec.UsesBuilder)
        {
            return CommandOutcome.Reject("Let the builder finish first.");
        }

        var gained = default(ResourceBag);
        if (building.Type == BuildingType.Tree)
        {
            // Cutting a grown tree gives timber (a slow tree farm); a sapling gives nothing.
            var timber = building.Level >= 1 ? Math.Min(_table.WildTreeTimber, Math.Max(0, Caps.Timber - _stock.Timber)) : 0;
            gained = new ResourceBag(timber, 0, 0, 0);
            _stock += gained;
        }
        else if (!building.Wrecked && building.Level >= 1)
        {
            var cost = spec.Levels[building.Level - 1].Cost;
            var full = spec.IsTower || building.Type == BuildingType.Stone; // stone: exactly what it cost
            _stock += full ? cost : cost.Half(); // a wreck is worth nothing (else demolish-and-rebuild is a free repair)
        }

        _buildings.Remove(building);
        return CommandOutcome.Accept(gained);
    }

    private CommandOutcome DoCollect(int? id, DateTimeOffset now)
    {
        var targets = id == null ? _buildings.Where(b => _table.Spec(b.Type).Produces != null).ToList() : new List<Building?> { Find(id.Value) }.Where(b => b != null).Select(b => b!).ToList();
        if (targets.Count == 0)
        {
            return CommandOutcome.Reject("That building is gone.");
        }

        var gained = default(ResourceBag);
        var caps = Caps;
        foreach (var building in targets)
        {
            var resource = _table.Spec(building.Type).Produces;
            if (resource == null)
            {
                continue;
            }

            Materialize(building, now);
            var space = Math.Max(0, caps.Get(resource.Value) - (_stock + gained).Get(resource.Value));
            var take = (int)Math.Min(space, building.BufferMilli / 1000);
            building.BufferMilli -= take * 1000L;
            gained += ResourceBag.Of(resource.Value, take);
        }

        if (gained.IsZero)
        {
            return CommandOutcome.Reject(targets.Any(b => Buffer(b, now) >= 1000) ? "Storage is full. Build or upgrade a Storage Hut." : "Nothing to collect yet.");
        }

        _stock += gained;
        return CommandOutcome.Accept(gained);
    }

    private CommandOutcome RecordNight(RecordNightResult night, DateTimeOffset now)
    {
        if (night.Night <= LastNightRecorded)
        {
            return CommandOutcome.Reject("That Night is already in the books.");
        }

        var salvage = Math.Min(_table.Salvage(night.Stars), Math.Max(0, Caps.Scrap - _stock.Scrap));
        _stock += new ResourceBag(0, 0, salvage, 0);
        LastNightRecorded = night.Night;
        foreach (var id in night.WreckedBuildingIds ?? Array.Empty<int>())
        {
            var building = Find(id);
            if (building != null && building.Type != BuildingType.Hearth)
            {
                Materialize(building, now);
                building.Wrecked = true;
            }
        }
        return CommandOutcome.Accept(new ResourceBag(0, 0, salvage, 0));
    }

    private CommandOutcome Repair(int id, DateTimeOffset now)
    {
        var building = Find(id);
        if (building == null || !building.Wrecked)
        {
            return CommandOutcome.Reject(building == null ? "That building is gone." : "It isn't broken. Yet.");
        }

        var cost = _table.RepairCost(building.Type, building.Level);
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        Materialize(building, now); // nothing accrued while wrecked; production restarts from now
        building.Wrecked = false;
        return CommandOutcome.Accept();
    }

    private CommandOutcome FinishNow(DateTimeOffset now)
    {
        var building = Constructing;
        if (building == null)
        {
            return CommandOutcome.Reject("Nothing is being built.");
        }

        building.UpgradeEndsAtUtc = now;
        Advance(now);
        return CommandOutcome.Accept();
    }

    // ---------------- terrain ----------------

    private bool IsWildTreeStanding(GridPosition tile) =>
        _map.WildTrees.Contains(tile) && _buildings.All(b => b.Position != tile)
        && (!_treeCuts.TryGetValue(tile, out var cut) || cut.AddHours(_table.WildTreeRegrowHours) <= _now);

    private CommandOutcome Clear(GridPosition tile)
    {
        if (!_map.Contains(tile))
        {
            return CommandOutcome.Reject("Nothing to clear there.");
        }

        if (!_map.IsOpen(tile, HearthLevel))
        {
            return CommandOutcome.Reject($"That land opens at Hearth {_map.OpensAt(tile)}.");
        }

        if (_map.IsBedrock(tile))
        {
            return CommandOutcome.Reject("That's bedrock. It was here before the Romans and it will be here after the Prophet.");
        }

        if (_map.IsBoulder(tile) && !_cleared.Contains(tile))
        {
            if (!_stock.Covers(_table.BoulderCost))
            {
                return CommandOutcome.Reject($"Need {_stock.Missing(_table.BoulderCost)} more.");
            }

            _stock -= _table.BoulderCost;
            var scrap = Math.Min(_table.BoulderScrap, Math.Max(0, Caps.Scrap - _stock.Scrap));
            _stock += new ResourceBag(0, 0, scrap, 0);
            _cleared.Add(tile);
            return CommandOutcome.Accept(new ResourceBag(0, 0, scrap, 0));
        }

        if (IsWildTreeStanding(tile))
        {
            var timber = Math.Min(_table.WildTreeTimber, Math.Max(0, Caps.Timber - _stock.Timber));
            _stock += new ResourceBag(timber, 0, 0, 0);
            _treeCuts[tile] = _now;
            return CommandOutcome.Accept(new ResourceBag(timber, 0, 0, 0));
        }

        return CommandOutcome.Reject("Nothing to clear there.");
    }

    // ---------------- rules ----------------

    private string? PlacementProblem(GridPosition tile)
    {
        if (_map.Contains(tile) && !_map.IsOpen(tile, HearthLevel))
        {
            return $"That land opens at Hearth {_map.OpensAt(tile)}.";
        }

        if (!_map.Contains(tile) || tile == _map.Spawn || tile == _map.Hearth || _buildings.Any(b => b.Position == tile) || _map.IsBedrock(tile))
        {
            return "Can't build there.";
        }

        if (_map.IsInSpawnClearance(tile))
        {
            return "Too close to where the raiders come in.";
        }

        if (_map.IsBoulder(tile) && !_cleared.Contains(tile))
        {
            return "Clear the boulder first.";
        }

        if (IsWildTreeStanding(tile))
        {
            return "Cut the tree down first.";
        }

        try
        {
            var layout = ToBattleLayout();
            var buildings = _buildings.Where(b => b.Type != BuildingType.Hearth && !_table.Spec(b.Type).IsTower).Select(b => b.Position);
            _map.ToBattleLayout(layout.Towers, buildings.Append(tile), null, HearthLevel).Validate();
        }
        catch (InvalidLayoutException)
        {
            return "That would seal the road. The Bishop's lawyers would have a field day.";
        }

        return null;
    }

    /// <summary>
    /// Brings the village up to <paramref name="now"/>, handling what happened meanwhile in time order: a construction
    /// finishing (which may change the Tension rate) and Tension crossing Hostile (which schedules a raid).
    /// </summary>
    private void Advance(DateTimeOffset now)
    {
        while (true)
        {
            // The earliest due event goes first: a construction, Tension crossing Hostile, a raid's deadline, an abandoned Night.
            var growing = _buildings.Where(b => b.IsUpgrading).OrderBy(b => b.UpgradeEndsAtUtc).FirstOrDefault();
            var construction = Due(growing?.UpgradeEndsAtUtc, now);
            var crossing = Due(_raid == null && _night == null ? HostileCrossing() : null, now);
            var deadline = Due(_raid != null && _night == null ? _raid.WarningEndsAtUtc : (DateTimeOffset?)null, now);
            var abandoned = Due(_night != null ? _nightStartedAt.AddMinutes(_table.Tension.AbandonedNightMinutes) : (DateTimeOffset?)null, now);
            var next = Earliest(construction, crossing, deadline, abandoned);
            if (next == null)
            {
                return;
            }

            if (next == construction)
            {
                CompleteConstruction(growing!, construction!.Value);
            }
            else if (next == crossing)
            {
                ScheduleRaid(crossing!.Value);
            }
            else if (next == deadline)
            {
                // Nobody lit the lamps: the raid happens anyway, with only the towers fighting.
                _night = PlanNight(deadline!.Value);
                _nightStartedAt = deadline.Value;
                ResolveAt(Array.Empty<PlayerInput>(), deadline.Value, whileAway: true);

                // One raid per absence: Tension starts again from now, not from the deadline (no raid every day for a month).
                if (now > _tensionAsOf)
                {
                    _tensionAsOf = now;
                }
            }
            else
            {
                ResolveAt(Array.Empty<PlayerInput>(), abandoned!.Value, whileAway: true);
            }
        }
    }

    private static DateTimeOffset? Due(DateTimeOffset? at, DateTimeOffset now) => at <= now ? at : null;

    private static DateTimeOffset? Earliest(params DateTimeOffset?[] times)
    {
        DateTimeOffset? best = null;
        foreach (var t in times)
        {
            if (t != null && (best == null || t < best))
            {
                best = t;
            }
        }

        return best;
    }

    // ---------------- Nights ----------------

    private CommandOutcome Begin(DateTimeOffset now)
    {
        if (_night != null)
        {
            return CommandOutcome.Reject("A Night is already on.");
        }

        if (_raid == null && _shieldEndsAt > now)
        {
            return CommandOutcome.Reject("Wulfric's men are still licking their wounds. Nobody comes while the shield is up.");
        }

        _night = PlanNight(now);
        _nightStartedAt = now;
        return CommandOutcome.Accept() with { Plan = _night };
    }

    /// <summary>The coming raid, or an early Night sized by the current Tension; the layout is frozen into the plan.</summary>
    private NightPlan PlanNight(DateTimeOffset now)
    {
        MaterializeTension(now);
        var tension = TensionValue(now);
        var power = _table.Tension.BasePower(HearthLevel);
        var night = LastNightRecorded + 1;
        var layout = ToBattleLayout();
        var budget = _raid != null ? power * (100 + tension - _table.Tension.Hostile) / 100 : Math.Max(power * tension / _table.Tension.Hostile, power / 2);
        var waves = _raid != null && tension >= _table.Tension.Punitive ? 5 : 4;
        var seed = _raid?.Seed ?? (ulong)(946 + night);
        return new NightPlan(night, seed, budget, waves, SluiceBonus, _raid != null, layout.Towers, layout.Obstacles, layout.Structures);
    }

    private CommandOutcome Resolve(ResolveNight command, DateTimeOffset now)
    {
        if (_night == null || command.Night != _night.Night)
        {
            return CommandOutcome.Reject("That isn't the Night being fought.");
        }

        for (var i = 0; i < command.Inputs.Count; i++)
        {
            if (command.Inputs[i].Tick < 0 || (i > 0 && command.Inputs[i].Tick < command.Inputs[i - 1].Tick))
            {
                return CommandOutcome.Reject("That input log doesn't add up.");
            }
        }

        return CommandOutcome.Accept() with { Report = ResolveAt(command.Inputs, now, whileAway: false) };
    }

    /// <summary>Replays the Night the village planned (never trusting a reported result) and applies what it finds.</summary>
    private NightReport ResolveAt(IReadOnlyList<PlayerInput> inputs, DateTimeOffset at, bool whileAway)
    {
        var plan = _night!;
        var battle = new Battle(plan.ToBattleSetup(_map, inputs));
        var result = battle.RunToCompletion();
        var wrecked = new List<int>();
        foreach (var structure in battle.Structures)
        {
            if (structure.IsWrecked)
            {
                wrecked.Add(structure.Id);
            }
        }

        var salvage = Math.Min(_table.Salvage(result.Stars), Math.Max(0, Caps.Scrap - _stock.Scrap));
        _stock += new ResourceBag(0, 0, salvage, 0);
        foreach (var id in wrecked)
        {
            var building = Find(id);
            if (building != null && building.Type != BuildingType.Hearth)
            {
                Materialize(building, at);
                building.Wrecked = true;
            }
        }

        MaterializeTension(at);
        _tensionMilli = 0;
        _shieldEndsAt = at.AddHours(_table.Tension.ShieldHours);
        _raid = null;
        _night = null;
        LastNightRecorded = plan.Night;
        var report = new NightReport(plan.Night, plan.Scheduled, whileAway, result.Stars, result.HearthHealth, result.HearthMaxHealth, wrecked, salvage, result.StateHashHex);
        if (whileAway)
        {
            _unseenReport = report;
        }

        return report;
    }

    private CommandOutcome Acknowledge()
    {
        if (_unseenReport == null)
        {
            return CommandOutcome.Reject("Nothing to report.");
        }

        _unseenReport = null;
        return CommandOutcome.Accept();
    }

    /// <summary>Completes a construction at its own end time (so production and the Tension rate change from then).</summary>
    private void CompleteConstruction(Building building, DateTimeOffset endedAt)
    {
        MaterializeTension(endedAt);
        Materialize(building, endedAt);
        building.Level++;
        building.UpgradeEndsAtUtc = null;
        if (endedAt > building.AsOfUtc)
        {
            building.AsOfUtc = endedAt; // never backwards: time already counted is never counted again
        }
    }

    private void ScheduleRaid(DateTimeOffset crossing)
    {
        MaterializeTension(crossing);
        var id = _nextRaidId++;
        _raid = new ScheduledRaid(id, (ulong)(1_000_003L * id + 946), crossing.AddHours(_table.Tension.WarningHours));
    }

    // ---------------- Tension (lazy and linear, ADR 0004) ----------------

    private int TensionRate => _table.Tension.RatePerHour(HearthLevel);

    /// <summary>Tension in thousandths at <paramref name="at"/>: no rise during a shield, capped at the max.</summary>
    private long TensionMilliAt(DateTimeOffset at)
    {
        var start = _shieldEndsAt > _tensionAsOf ? _shieldEndsAt!.Value : _tensionAsOf;
        if (at <= start)
        {
            return _tensionMilli;
        }

        var seconds = (at.UtcTicks - start.UtcTicks) / TimeSpan.TicksPerSecond;
        return Math.Min(_table.Tension.Max * 1000L, _tensionMilli + TensionRate * 1000L * seconds / 3600);
    }

    private int TensionValue(DateTimeOffset at) => (int)(TensionMilliAt(at) / 1000);

    private void MaterializeTension(DateTimeOffset at)
    {
        if (at <= _tensionAsOf)
        {
            return;
        }

        _tensionMilli = TensionMilliAt(at);
        _tensionAsOf = at;
    }

    /// <summary>When Tension reaches Hostile at the current rate (analytically), or null if it never will.</summary>
    private DateTimeOffset? HostileCrossing()
    {
        var start = _shieldEndsAt > _tensionAsOf ? _shieldEndsAt!.Value : _tensionAsOf;
        var need = _table.Tension.Hostile * 1000L - _tensionMilli;
        if (need <= 0)
        {
            return start;
        }

        if (TensionRate <= 0)
        {
            return null;
        }

        var perSecondTimes3600 = TensionRate * 1000L;
        var seconds = (need * 3600 + perSecondTimes3600 - 1) / perSecondTimes3600; // round up: reached, not almost
        return start.AddSeconds(seconds);
    }

    private CommandOutcome RaiseTension(int amount, DateTimeOffset now)
    {
        MaterializeTension(now);
        _tensionMilli = Math.Min(_table.Tension.Max * 1000L, Math.Max(0, _tensionMilli + amount * 1000L));
        Advance(now);
        return CommandOutcome.Accept();
    }

    private CommandOutcome SkipWarning(DateTimeOffset now)
    {
        if (_raid == null)
        {
            return CommandOutcome.Reject("No raid is coming. Enjoy it.");
        }

        _raid = _raid with { WarningEndsAtUtc = now };
        Advance(now);
        return CommandOutcome.Accept();
    }

    /// <summary>What it produces now: nothing while being built or wrecked.</summary>
    private int Rate(Building b) => b.Wrecked ? 0 : LevelRate(b);

    private int LevelRate(Building b)
    {
        var spec = _table.Spec(b.Type);
        return spec.Produces == null || b.Level == 0 ? 0 : spec.Levels[b.Level - 1].PerHour;
    }

    /// <summary>How much it can hold: set by its level, so a wreck keeps what was already made.</summary>
    private long CapacityMilli(Building b) => LevelRate(b) * (long)_table.BufferHours * 1000;

    private long Buffer(Building b, DateTimeOffset at)
    {
        var seconds = Math.Max(0, (at.UtcTicks - b.AsOfUtc.UtcTicks) / TimeSpan.TicksPerSecond);
        return Math.Min(CapacityMilli(b), b.BufferMilli + Rate(b) * 1000L * seconds / 3600);
    }

    private void Materialize(Building b, DateTimeOffset at)
    {
        if (at <= b.AsOfUtc)
        {
            return; // a clock that went backwards never takes anything away
        }

        b.BufferMilli = Buffer(b, at);
        b.AsOfUtc = at;
    }

    private Building? Find(int id) => _buildings.FirstOrDefault(b => b.Id == id);

    private void Remember(Guid id)
    {
        _recent.Add(id);
        _recentOrder.Enqueue(id);
        if (_recentOrder.Count > RememberedCommands)
        {
            _recent.Remove(_recentOrder.Dequeue());
        }
    }
}
