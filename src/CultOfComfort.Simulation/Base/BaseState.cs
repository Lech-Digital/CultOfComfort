using System;
using System.Collections.Generic;
using System.Linq;
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

    private BaseState(BuildingTable table, VillageMap map)
    {
        _table = table;
        _map = map;
    }

    public long Version { get; private set; }

    public int LastNightRecorded { get; private set; }

    public int HearthLevel => Hearth.Level;

    private Building Hearth => _buildings[0];

    private Building? Constructing => _buildings.FirstOrDefault(b => b.IsUpgrading);

    public static BaseState CreateNew(BuildingTable table, VillageMap map, DateTimeOffset nowUtc)
    {
        var state = new BaseState(table, map) { _stock = table.StartingStock };
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

        state.Validate();
        return state;
    }

    /// <summary>Rejects a save that no sequence of commands could have produced (hand-edited or corrupted).</summary>
    private void Validate()
    {
        void Fail(string reason) => throw new InvalidBaseDataException(reason);

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

        if (_buildings.Count(b => b.IsUpgrading) > 1)
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
                && (!_map.Contains(b.Position) || b.Position == _map.Spawn || b.Position == _map.Hearth || _map.Rocks.Contains(b.Position))))
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
        _recentOrder.ToList());

    public CommandOutcome Apply(BaseCommand command, DateTimeOffset nowUtc)
    {
        if (_recent.Contains(command.CommandId))
        {
            return new CommandOutcome(CommandStatus.Duplicate, Version);
        }

        Advance(nowUtc);
        var outcome = command switch
        {
            PlaceBuilding place => Place(place, nowUtc),
            UpgradeBuilding upgrade => Upgrade(Find(upgrade.BuildingId), nowUtc),
            UpgradeHearth _ => Upgrade(Hearth, nowUtc),
            DemolishBuilding demolish => Demolish(demolish.BuildingId),
            Collect collect => DoCollect(collect.BuildingId, nowUtc),
            RecordNightResult night => RecordNight(night, nowUtc),
            FinishConstructionNow _ => FinishNow(nowUtc),
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
        var views = _buildings.Select(b => new BuildingView(
            b.Id, b.Type, b.Level, b.Position, (int)(Buffer(b, nowUtc) / 1000), (int)(CapacityMilli(b) / 1000), Rate(b), b.UpgradeEndsAtUtc, b.Wrecked)).ToList();
        return new BaseSnapshot(Version, nowUtc, HearthLevel, _stock, Caps, views, SluiceBonus, LastNightRecorded);
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
    public BattleLayout ToBattleLayout() => _map.ToBattleLayout(
        _buildings.Where(b => _table.Spec(b.Type).IsTower).Select(b => new PlacedTower(_table.Spec(b.Type).Tower!.Value, b.Position)),
        _buildings.Where(b => b.Type != BuildingType.Hearth && !_table.Spec(b.Type).IsTower).Select(b => b.Position),
        _buildings.Where(b => b.Type != BuildingType.Hearth).Select(b =>
        {
            var health = _table.StructureHealth(b.Type, b.Level);
            return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health);
        }));
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
        if (!instant && Constructing != null)
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

        if (building.IsUpgrading)
        {
            return CommandOutcome.Reject("Let the builder finish first.");
        }

        var spec = _table.Spec(building.Type);
        var cost = spec.Levels[building.Level - 1].Cost;
        if (!building.Wrecked)
        {
            _stock += spec.IsTower ? cost : cost.Half(); // a wreck is worth nothing (else demolish-and-rebuild is a free repair)
        }

        _buildings.Remove(building);
        return CommandOutcome.Accept();
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

    // ---------------- rules ----------------

    private string? PlacementProblem(GridPosition tile)
    {
        if (!_map.Contains(tile) || tile == _map.Spawn || tile == _map.Hearth || _map.Rocks.Contains(tile) || _buildings.Any(b => b.Position == tile))
        {
            return "Can't build there.";
        }

        try
        {
            var layout = ToBattleLayout();
            _map.ToBattleLayout(layout.Towers, layout.Obstacles.Except(_map.Rocks).Append(tile)).Validate();
        }
        catch (InvalidLayoutException)
        {
            return "That would seal the road. The Bishop's lawyers would have a field day.";
        }

        return null;
    }

    /// <summary>Completes a construction whose timer ran out, at its own end time (so production counts from then).</summary>
    private void Advance(DateTimeOffset now)
    {
        var building = Constructing;
        if (building == null || building.UpgradeEndsAtUtc > now)
        {
            return;
        }

        var endedAt = building.UpgradeEndsAtUtc!.Value;
        Materialize(building, endedAt);
        building.Level++;
        building.UpgradeEndsAtUtc = null;
        if (endedAt > building.AsOfUtc)
        {
            building.AsOfUtc = endedAt; // never backwards: time already counted is never counted again
        }
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
