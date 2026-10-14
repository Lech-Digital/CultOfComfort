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
public sealed partial class BaseState
{
    /// <summary>Saves before footprints (layout version 0) are migrated on load: every building finds room for its shape.</summary>
    public const int CurrentLayoutVersion = 3;

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
    private HashSet<GridPosition> _road => _roadTiles ??= new HashSet<GridPosition>(_map.Road);
    private HashSet<GridPosition>? _roadTiles;
    private HashSet<GridPosition>? _network; // village road network, rebuilt after any building change
    private int _population;
    private DateTimeOffset _populationAsOf; // the next villager arrives an arrival interval after this
    private DateTimeOffset _upkeepAsOf;     // Peat burned up to here
    private long _upkeepMilli;              // a part-burned Peat
    private bool _cold;                     // the Peat ran out: nobody works until there is Peat again
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
        var state = new BaseState(table, map)
        {
            _stock = table.StartingStock, _tensionAsOf = nowUtc, _population = table.HearthBeds, _populationAsOf = nowUtc, _upkeepAsOf = nowUtc,
            _domainAsOf = nowUtc,
        };
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
            _population = data.Population,
            _populationAsOf = new DateTimeOffset(data.PopulationAsOfUtcTicks, TimeSpan.Zero),
            _upkeepAsOf = new DateTimeOffset(data.UpkeepAsOfUtcTicks, TimeSpan.Zero),
            _upkeepMilli = data.UpkeepMilli,
            _cold = data.Cold,
            _errandIndex = data.ErrandIndex,
            _errandEventDone = data.ErrandEventDone,
        };
        foreach (var b in data.Buildings.OrderBy(b => b.Type == BuildingType.Hearth ? -1 : b.Id))
        {
            if (!table.Specs.TryGetValue(b.Type, out var spec) || b.Level < 0 || b.Level > spec.MaxLevel)
            {
                throw new InvalidBaseDataException($"Building {b.Id} has an impossible type or level.");
            }

            var anchor = new GridPosition(b.X, b.Y);
            var tiles = data.LayoutVersion >= 1 ? Footprint.Tiles(spec, anchor, b.Rotated) : new[] { anchor }; // shapes arrived in layout version 1
            state._buildings.Add(new Building(b.Id, b.Type, anchor, b.Level, new DateTimeOffset(b.AsOfUtcTicks, TimeSpan.Zero), tiles, b.Rotated)
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

        state.RestoreDomain(data.Domain, state._tensionAsOf);
        state.RestoreResearch(data.Research);

        if (data.LayoutVersion < 1)
        {
            state.MigrateLayout();
        }

        if (data.LayoutVersion < 2)
        {
            state.LayRoadsToEverything();
        }

        if (data.LayoutVersion < 3)
        {
            // From before villagers: the beds are full and the clocks start at the save's latest moment.
            var latest = new DateTimeOffset(data.Buildings.Select(b => b.AsOfUtcTicks).Append(state._tensionAsOf.UtcTicks).Max(), TimeSpan.Zero);
            state._population = state.Beds();
            state._populationAsOf = state._upkeepAsOf = latest;
            state._upkeepMilli = 0;
            state._cold = false;
        }

        state.Validate();
        return state;
    }

    /// <summary>
    /// An old save's buildings were all 1×1 and could stand on the road. Each, in id order, keeps its anchor if its
    /// shape fits, else turns, else takes the nearest spot (up to 4 tiles away) that fits; with no room it is taken
    /// down and everything paid for it comes back.
    /// </summary>
    private void MigrateLayout()
    {
        var old = _buildings.ToList();
        _buildings.Clear();
        _buildings.Add(old.Single(b => b.Type == BuildingType.Hearth));
        foreach (var b in old.Where(b => b.Type != BuildingType.Hearth).OrderBy(b => b.Id))
        {
            var spec = _table.Spec(b.Type);
            (GridPosition Anchor, bool Rotated)? spot = null;
            foreach (var candidate in Candidates(b.Position))
            {
                if (FitProblem(spec, candidate.Anchor, candidate.Rotated, checkTrees: false) == null)
                {
                    spot = candidate;
                    break;
                }
            }

            if (spot == null)
            {
                ResourceBag paid = default;
                var levelsPaid = Math.Min(b.Level + (b.IsUpgrading ? 1 : 0), spec.MaxLevel);
                for (var level = 0; level < levelsPaid; level++)
                {
                    paid += spec.Levels[level].Cost;
                }

                _stock += paid;
                continue;
            }

            var (anchor, rotated) = spot.Value;
            _buildings.Add(new Building(b.Id, b.Type, anchor, b.Level, b.AsOfUtc, Footprint.Tiles(spec, anchor, rotated), rotated)
            {
                BufferMilli = b.BufferMilli,
                UpgradeEndsAtUtc = b.UpgradeEndsAtUtc,
                Wrecked = b.Wrecked,
            });
        }
    }

    /// <summary>
    /// A save from before village roads: every building that isn't connected gets the shortest free Dirt Track to the
    /// network over open, buildable land, so nothing that worked yesterday stops today.
    /// </summary>
    private void LayRoadsToEverything()
    {
        var track = _table.Spec(BuildingType.DirtTrack);
        foreach (var b in _buildings.Where(b => b.Type != BuildingType.Hearth && !BuildingTable.IsRoad(b.Type)).OrderBy(b => b.Id).ToList())
        {
            if (IsConnected(b))
            {
                continue;
            }

            var network = Network();
            var footprint = new HashSet<GridPosition>(b.Tiles);
            var previous = new Dictionary<GridPosition, GridPosition>();
            var frontier = new Queue<GridPosition>();
            foreach (var start in b.Tiles.SelectMany(Neighbours).Where(t => !footprint.Contains(t) && FitProblem(track, t, false, checkTrees: false) == null))
            {
                if (previous.TryAdd(start, start))
                {
                    frontier.Enqueue(start);
                }
            }

            while (frontier.Count > 0)
            {
                var tile = frontier.Dequeue();
                if (Neighbours(tile).Any(n => network.Contains(n)))
                {
                    for (var t = tile; ; t = previous[t])
                    {
                        _buildings.Add(new Building(_nextId++, BuildingType.DirtTrack, t, 1, _now, new[] { t }));
                        if (previous[t] == t)
                        {
                            break;
                        }
                    }

                    _network = null;
                    break;
                }

                foreach (var next in Neighbours(tile))
                {
                    if (!previous.ContainsKey(next) && FitProblem(track, next, false, checkTrees: false) == null)
                    {
                        previous[next] = tile;
                        frontier.Enqueue(next);
                    }
                }
            }
        }
    }

    private static IEnumerable<GridPosition> Neighbours(GridPosition t)
    {
        yield return new GridPosition(t.X + 1, t.Y);
        yield return new GridPosition(t.X - 1, t.Y);
        yield return new GridPosition(t.X, t.Y + 1);
        yield return new GridPosition(t.X, t.Y - 1);
    }

    /// <summary>
    /// The road network: from the Hearth along village roads, gates and the raiders' old track (it is a road, too).
    /// Walls and everything else cut it.
    /// </summary>
    private HashSet<GridPosition> Network()
    {
        if (_network != null)
        {
            return _network;
        }

        var carriers = new HashSet<GridPosition>(_road);
        foreach (var b in _buildings.Where(b => BuildingTable.IsRoad(b.Type) || b.Type == BuildingType.Gate))
        {
            carriers.UnionWith(b.Tiles);
        }

        var network = new HashSet<GridPosition> { _map.Hearth };
        var frontier = new Queue<GridPosition>();
        frontier.Enqueue(_map.Hearth);
        while (frontier.Count > 0)
        {
            foreach (var next in Neighbours(frontier.Dequeue()))
            {
                if (carriers.Contains(next) && network.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return _network = network;
    }

    /// <summary>A building works only when a tile beside it is on the road network (or it stands by the Hearth).</summary>
    private bool IsConnected(Building b)
    {
        if (b.Type == BuildingType.Hearth || BuildingTable.IsRoad(b.Type))
        {
            return true;
        }

        var network = Network();
        return b.Tiles.SelectMany(Neighbours).Any(n => network.Contains(n) && !b.Tiles.Contains(n));
    }

    /// <summary>Best road bonus (percent) among the road tiles beside it.</summary>
    private int RoadBonus(Building b)
    {
        var best = 0;
        foreach (var n in b.Tiles.SelectMany(Neighbours))
        {
            var road = _buildings.FirstOrDefault(o => BuildingTable.IsRoad(o.Type) && o.Position == n);
            if (road != null)
            {
                best = Math.Max(best, BuildingTable.RoadBonusPercent(road.Type));
            }
        }

        return best;
    }

    /// <summary>Settles every producer up to <paramref name="at"/>: done before anything that changes production.</summary>
    private void MaterializeAll(DateTimeOffset at)
    {
        foreach (var b in _buildings)
        {
            Materialize(b, at);
        }
    }

    private static IEnumerable<(GridPosition Anchor, bool Rotated)> Candidates(GridPosition origin)
    {
        for (var radius = 0; radius <= 4; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var anchor = new GridPosition(origin.X + dx, origin.Y + dy);
                    yield return (anchor, false);
                    yield return (anchor, true);
                }
            }
        }
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

        if (_population < 0 || _population > Beds() || _upkeepMilli < 0 || _upkeepMilli >= 1000)
        {
            Fail("impossible villagers");
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

            foreach (var tile in b.Tiles)
            {
                if (!tiles.Add(tile) || (b.Type != BuildingType.Hearth
                    && (!_map.IsOpen(tile, HearthLevel) || tile == _map.Spawn || tile == _map.Hearth || (_road.Contains(tile) && b.Type != BuildingType.Gate)
                        || _map.IsBedrock(tile) || (_map.IsBoulder(tile) && !_cleared.Contains(tile)))))
                {
                    Fail($"building {b.Id} stands somewhere it cannot");
                }
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
        _buildings.Select(b => new BuildingData(b.Id, b.Type, b.Level, b.Position.X, b.Position.Y, b.UpgradeEndsAtUtc?.UtcTicks, b.BufferMilli, b.AsOfUtc.UtcTicks, b.Wrecked, b.Rotated)).ToList(),
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
        _treeCuts.Select(c => new TreeCutData(c.Key.X, c.Key.Y, c.Value.UtcTicks)).ToList(),
        CurrentLayoutVersion,
        _population,
        _populationAsOf.UtcTicks,
        _upkeepAsOf.UtcTicks,
        _upkeepMilli,
        _cold,
        _errandIndex,
        _errandEventDone,
        DomainToData(),
        ResearchToData());

    public CommandOutcome Apply(BaseCommand command, DateTimeOffset nowUtc)
    {
        if (_recent.Contains(command.CommandId))
        {
            return new CommandOutcome(CommandStatus.Duplicate, Version);
        }

        Advance(nowUtc);
        _now = nowUtc;
        MaterializeAll(nowUtc); // whatever this command changes, production up to now was earned under the old rules
        if (_night != null && command is PlaceBuilding or PaintTiles or MoveBuilding or RepairAll or UpgradeBuilding or UpgradeHearth or DemolishBuilding or RepairBuilding or FinishConstructionNow or RecordNightResult or ClearTerrain or SendMissionary or PayTithe)
        {
            return CommandOutcome.Reject("Not now — there's a Night on.") with { Version = Version };
        }

        var outcome = command switch
        {
            BeginNight _ => Begin(nowUtc),
            ResolveNight resolve => Resolve(resolve, nowUtc),
            AcknowledgeReport _ => Acknowledge(),
            ClaimErrand _ => ClaimCurrentErrand(),
            ClearTerrain clear => Clear(clear.Tile),
            PlaceBuilding place => Place(place, nowUtc),
            PaintTiles paint => Paint(paint, nowUtc),
            MoveBuilding move => Move(move),
            RepairAll _ => RepairEverything(nowUtc),
            UpgradeBuilding upgrade => Upgrade(Find(upgrade.BuildingId), nowUtc),
            UpgradeHearth _ => Upgrade(Hearth, nowUtc),
            DemolishBuilding demolish => Demolish(demolish.BuildingId),
            Collect collect => DoCollect(collect.BuildingId, nowUtc),
            RecordNightResult night => RecordNight(night, nowUtc),
            FinishConstructionNow _ => FinishNow(nowUtc),
            DebugRaiseTension raise => RaiseTension(raise.Amount, nowUtc),
            DebugSkipWarning _ => SkipWarning(nowUtc),
            DebugFillStores _ => FillStores(nowUtc),
            DebugEndShield _ => EndShield(nowUtc),
            SendMissionary send => SendOut(send, nowUtc),
            CollectOfferings _ => CollectOfferings(nowUtc),
            PayTithe tithe => Tithe(tithe, nowUtc),
            StartResearch start => StartStudy(start, nowUtc),
            Trade trade => DoTrade(trade),
            RepairBuilding repair => Repair(repair.BuildingId, nowUtc),
            _ => CommandOutcome.Reject("Unknown order."),
        };

        _network = null;
        SettlePeople(nowUtc);
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
            b.Id, b.Type, b.Level, b.Position, (int)(Buffer(b, nowUtc) / 1000), (int)(CapacityMilli(b) / 1000), Rate(b), b.UpgradeEndsAtUtc, b.Wrecked, b.Rotated, b.Tiles, IsConnected(b),
            IsStaffed(b) ? _table.Spec(b.Type).Workers : 0, _table.Spec(b.Type).Workers)).ToList();
        var tension = TensionValue(nowUtc);
        return new BaseSnapshot(Version, nowUtc, HearthLevel, _stock, Caps, views, SluiceBonus, LastNightRecorded,
            tension, _table.Tension.StateOf(tension), _raid?.WarningEndsAtUtc, _shieldEndsAt > nowUtc ? _shieldEndsAt : null, _night, _unseenReport, _cleared.Concat(_treeCuts.Keys.Where(t => !IsWildTreeStanding(t))).ToList(),
            _population, Beds(), _cold, ErrandNow(), Comfort(), DomainViewAt(nowUtc), ResearchViews(), ComfortParts());
    }

    public ResourceBag Caps
    {
        get
        {
            var cap = _table.HearthStorageCaps[HearthLevel - 1];
            foreach (var b in _buildings)
            {
                if (b.Level > 0 && !b.Wrecked && IsConnected(b))
                {
                    cap += _table.Spec(b.Type).Levels[b.Level - 1].StorageBonus;
                }
            }

            cap = cap * (100 + Bonus(TechEffect.StoragePercent)) / 100;
            return new ResourceBag(cap, cap, cap, cap);
        }
    }

    public int SluiceBonus => _buildings.Where(b => b.Level > 0 && !b.Wrecked && IsConnected(b)).Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].SluiceCharges);

    /// <summary>The locked layout for a Night: towers fight, every other building is in the way.</summary>
    public BattleLayout ToBattleLayout()
    {
        return _map.ToBattleLayout(
            _buildings.Where(b => _table.Spec(b.Type).IsTower).Select(b => new PlacedTower(_table.Spec(b.Type).Tower!.Value, b.Position)),
            _buildings.Where(b => b.Type != BuildingType.Hearth && !_table.Spec(b.Type).IsTower && !BuildingTable.IsRoad(b.Type)).SelectMany(b => b.Tiles),
            _buildings.Where(b => b.Type != BuildingType.Hearth && _table.StructureHealth(b.Type, b.Level) > 0).Select(b =>
            {
                var health = _table.StructureHealth(b.Type, b.Level) * (100 + Bonus(TechEffect.HealthPercent)) / 100; // frozen into the plan
                return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health)
                {
                    Tiles = b.Tiles, Barrier = BuildingTable.IsBarrier(b.Type), Gate = b.Type == BuildingType.Gate,
                };
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

        var reason = FitProblem(spec, command.Position, command.Rotated, checkTrees: true);
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
        var building = new Building(_nextId++, command.Type, command.Position, instant ? 1 : 0, now,
            Footprint.Tiles(spec, command.Position, command.Rotated), command.Rotated);
        if (!instant)
        {
            building.UpgradeEndsAtUtc = now.AddSeconds(spec.Levels[0].BuildSeconds);
        }

        _buildings.Add(building);
        Advance(now); // zero-second builds complete at once
        return CommandOutcome.Accept();
    }

    private CommandOutcome Paint(PaintTiles command, DateTimeOffset now)
    {
        if (!BuildingTable.IsPaintable(command.Type))
        {
            return CommandOutcome.Reject("That isn't painted on.");
        }

        var spec = _table.Spec(command.Type);
        if (HearthLevel < spec.UnlockHearth)
        {
            return CommandOutcome.Reject($"{spec.Name} unlocks at Hearth {spec.UnlockHearth}.");
        }

        var cost = spec.Levels[0].Cost;
        string? firstProblem = null;
        var placed = 0;
        foreach (var tile in command.Tiles.Distinct())
        {
            var existing = _buildings.FirstOrDefault(b => b.Tiles.Contains(tile));
            string? problem;
            if (existing != null && BuildingTable.IsRoad(existing.Type) && BuildingTable.IsRoad(command.Type))
            {
                problem = BuildingTable.RoadRank(existing.Type) >= BuildingTable.RoadRank(command.Type) ? "There's already as good a road there." : null;
            }
            else
            {
                problem = FitProblem(spec, tile, false, checkTrees: true);
            }

            if (problem == null && !_stock.Covers(cost))
            {
                problem = $"Need {_stock.Missing(cost)} more.";
            }

            if (problem != null)
            {
                firstProblem ??= problem;
                continue;
            }

            if (existing != null)
            {
                _buildings.Remove(existing); // the better road goes down over the old one
            }

            _stock -= cost;
            _buildings.Add(new Building(_nextId++, command.Type, tile, 1, now, new[] { tile }));
            placed++;
        }

        _network = null;
        return placed > 0 ? CommandOutcome.Accept() : CommandOutcome.Reject(firstProblem ?? "Nowhere to put it.");
    }

    private CommandOutcome Move(MoveBuilding command)
    {
        var building = Find(command.BuildingId);
        if (building == null || building.Type == BuildingType.Hearth)
        {
            return CommandOutcome.Reject(building == null ? "That building is gone." : "The Hearth stays where the Hearth is.");
        }

        if (BuildingTable.IsRoad(building.Type))
        {
            return CommandOutcome.Reject("Roads are repainted, not moved.");
        }

        var spec = _table.Spec(building.Type);
        var problem = FitProblem(spec, command.Position, command.Rotated, checkTrees: true, ignore: building);
        if (problem != null)
        {
            return CommandOutcome.Reject(problem);
        }

        var index = _buildings.IndexOf(building);
        _buildings[index] = new Building(building.Id, building.Type, command.Position, building.Level, building.AsOfUtc,
            Footprint.Tiles(spec, command.Position, command.Rotated), command.Rotated)
        {
            BufferMilli = building.BufferMilli,
            UpgradeEndsAtUtc = building.UpgradeEndsAtUtc,
            Wrecked = building.Wrecked,
        };
        _network = null;
        return CommandOutcome.Accept();
    }

    private CommandOutcome RepairEverything(DateTimeOffset now)
    {
        var wrecked = _buildings.Where(b => b.Wrecked).ToList();
        if (wrecked.Count == 0)
        {
            return CommandOutcome.Reject("Nothing is broken. Yet.");
        }

        var total = wrecked.Aggregate(default(ResourceBag), (sum, b) => sum + _table.RepairCost(b.Type, b.Level));
        if (!_stock.Covers(total))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(total)} more.");
        }

        foreach (var b in wrecked)
        {
            Repair(b.Id, now);
        }

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

        var instant = spec.Levels[building.Level].BuildSeconds == 0; // a wall turned to stone needs no builder
        if (building.IsUpgrading || (!instant && Constructing != null))
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
        if (instant)
        {
            Advance(now);
        }

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
        NoteCollected(gained);
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
        NoteNight(night.Stars);
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
        if (building == null && _studying != null)
        {
            _studyEndsAt = now; // dev: the study finishes too
            Advance(now);
            return CommandOutcome.Accept();
        }

        if (building == null)
        {
            return CommandOutcome.Reject("Nothing is being built.");
        }

        building.UpgradeEndsAtUtc = now;
        Advance(now);
        return CommandOutcome.Accept();
    }

    // ---------------- the Trolley Market ----------------

    private CommandOutcome DoTrade(Trade trade)
    {
        if (trade.Amount <= 0 || trade.Give == trade.Get)
        {
            return CommandOutcome.Reject("The trolley doesn't do that.");
        }

        var price = _table.TradePrice(trade.Give, trade.Get, trade.Amount);
        if (_stock.Get(trade.Give) < price)
        {
            return CommandOutcome.Reject($"That costs {price} {trade.Give}.");
        }

        var room = Caps.Get(trade.Get) - _stock.Get(trade.Get);
        if (room < trade.Amount)
        {
            return CommandOutcome.Reject($"No room in the stores for {trade.Amount} {trade.Get}.");
        }

        _stock = _stock - ResourceBag.Of(trade.Give, price) + ResourceBag.Of(trade.Get, trade.Amount);
        return CommandOutcome.Accept(ResourceBag.Of(trade.Get, trade.Amount));
    }

    // ---------------- terrain ----------------

    private bool IsWildTreeStanding(GridPosition tile) =>
        _map.WildTrees.Contains(tile) && _buildings.All(b => !b.Tiles.Contains(tile))
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

    /// <summary>Why the building's shape can't stand there (null: it can). Every tile must be free and buildable.</summary>
    private string? FitProblem(BuildingSpec spec, GridPosition anchor, bool rotated, bool checkTrees, Building? ignore = null)
    {
        var tiles = Footprint.Tiles(spec, anchor, rotated);
        foreach (var tile in tiles)
        {
            if (_map.Contains(tile) && !_map.IsOpen(tile, HearthLevel))
            {
                return $"That land opens at Hearth {_map.OpensAt(tile)}.";
            }
        }

        foreach (var tile in tiles)
        {
            if (!_map.Contains(tile) || tile == _map.Spawn || tile == _map.Hearth)
            {
                return "Can't build there.";
            }
        }

        foreach (var tile in tiles)
        {
            if (_road.Contains(tile) && spec.Type != BuildingType.Gate) // a gate may stand across the road; nothing else may
            {
                return "That's the raiders' road. Build beside it.";
            }
        }

        if (spec.Type == BuildingType.Gate && !VillageMap.GateFits(tiles, _road,
                _buildings.Where(b => b != ignore && (BuildingTable.IsRoad(b.Type) || b.Type is BuildingType.WoodenWall or BuildingType.WattleFence)).SelectMany(b => b.Tiles)))
        {
            return VillageMap.GateMisplaced; // owner 2026-10-14: a gate alone in a field made no sense
        }

        if (spec.Type == BuildingType.Gate && tiles.Any(_road.Contains)
            && _buildings.Count(b => b != ignore && b.Type == BuildingType.Gate && b.Tiles.Any(_road.Contains)) >= _table.RoadGates(HearthLevel))
        {
            return "No room for another gate on the road until the Hearth grows.";
        }

        foreach (var tile in tiles)
        {
            if (!_map.Contains(tile) || tile == _map.Spawn || tile == _map.Hearth || _buildings.Any(b => b != ignore && b.Tiles.Contains(tile)) || _map.IsBedrock(tile))
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

            if (checkTrees && IsWildTreeStanding(tile))
            {
                return "Cut the tree down first.";
            }
        }

        if (VillageMap.NeedsRoad(spec) && !_map.RoadCouldReach(HearthLevel, Hearth.Tiles, _buildings.Where(b => BuildingTable.IsRoad(b.Type) || b.Type == BuildingType.Gate).SelectMany(b => b.Tiles),
                t => _buildings.Any(b => b != ignore && !BuildingTable.IsRoad(b.Type) && b.Tiles.Contains(t)), tiles))
        {
            return VillageMap.NoRoadReaches;
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
            var arrival = Due(_population < Beds() ? _populationAsOf.AddMinutes(_table.VillagerArrivalMinutes) : (DateTimeOffset?)null, now);
            var runsOut = Due(PeatRunsOutAt(), now);
            var domain = Due(NextDomainEvent(), now);
            var study = Due(StudyEnds(), now);
            var next = Earliest(construction, crossing, deadline, abandoned, arrival, runsOut, domain, study);
            if (next == null)
            {
                MaterializeUpkeep(now);
                SettlePeople(now);
                return;
            }

            // Everything earned or burned up to this moment counts under the rules before it.
            MaterializeAll(next.Value);
            MaterializeUpkeep(next.Value);
            if (next == arrival)
            {
                _population++;
                _populationAsOf = arrival!.Value;
                continue;
            }

            if (next == runsOut)
            {
                _stock = new ResourceBag(_stock.Timber, 0, _stock.Scrap, _stock.Devotion);
                _upkeepMilli = 0;
                _cold = true;
                continue;
            }

            SettlePeople(next.Value); // a full village's arrival clock waits at each event

            if (next == construction)
            {
                CompleteConstruction(growing!, construction!.Value);
            }
            else if (next == domain)
            {
                RunDomainEvents(domain!.Value);
            }
            else if (next == study)
            {
                CompleteStudy(study!.Value);
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

            SettlePeople(next.Value); // beds may have been built or wrecked
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
        return new NightPlan(night, seed, budget, waves, SluiceBonus, _raid != null, layout.Towers, layout.Obstacles, layout.Structures, HearthLevel);
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
        NoteNight(result.Stars);
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

        var spec = _table.Spec(building.Type);
        if (building.Level == 1 && spec.Produces != null)
        {
            // The crew finds some on day one (deadwood, a peat stack): a new producer starts with a little in store.
            building.BufferMilli += spec.Levels[0].PerHour * 1000L * _table.StarterStockMinutes / 60;
        }
    }

    private void ScheduleRaid(DateTimeOffset crossing)
    {
        MaterializeTension(crossing);
        var id = _nextRaidId++;
        _raid = new ScheduledRaid(id, (ulong)(1_000_003L * id + 946), crossing.AddHours(_table.Tension.WarningHours));
    }

    // ---------------- Tension (lazy and linear, ADR 0004) ----------------

    /// <summary>Tension per hour (thousandths): the Authority's passive mood plus the Marchlands' influence.</summary>
    private long TensionRateMilli() => _table.Tension.RatePerHour(HearthLevel) * 1000L + DomainTensionPerHourMilli();

    /// <summary>Tension in thousandths at <paramref name="at"/>: no rise during a shield, capped at the max.</summary>
    private long TensionMilliAt(DateTimeOffset at)
    {
        var start = _shieldEndsAt > _tensionAsOf ? _shieldEndsAt!.Value : _tensionAsOf;
        if (at <= start)
        {
            return _tensionMilli;
        }

        var seconds = (at.UtcTicks - start.UtcTicks) / TimeSpan.TicksPerSecond;
        return Math.Min(_table.Tension.Max * 1000L, _tensionMilli + TensionRateMilli() * seconds / 3600);
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

        var perSecondTimes3600 = TensionRateMilli();
        if (perSecondTimes3600 <= 0)
        {
            return null;
        }

        var seconds = (need * 3600 + perSecondTimes3600 - 1) / perSecondTimes3600; // round up: reached, not almost
        return start.AddSeconds(seconds);
    }

    private CommandOutcome EndShield(DateTimeOffset now)
    {
        if (!(_shieldEndsAt > now))
        {
            return CommandOutcome.Reject("There is no shield up.");
        }

        _shieldEndsAt = now;
        Advance(now);
        return CommandOutcome.Accept();
    }

    private CommandOutcome FillStores(DateTimeOffset now)
    {
        _stock = Caps;
        SettlePeople(now); // Peat back in storage warms a cold village
        Advance(now);
        return CommandOutcome.Accept();
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
    // Cold stops every trade but peat cutting: the cutters are how the village warms up again.
    private int Rate(Building b)
    {
        if (b.Wrecked || (_cold && _table.Spec(b.Type).Produces != ResourceType.Peat) || !IsConnected(b) || !IsStaffed(b))
        {
            return 0;
        }

        var produces = _table.Spec(b.Type).Produces;
        var research = produces == ResourceType.Timber ? Bonus(TechEffect.TimberPercent) : produces == ResourceType.Peat ? Bonus(TechEffect.PeatPercent) : 0;
        var rate = LevelRate(b) * (100 + RoadBonus(b) + research) / 100;
        return produces == ResourceType.Devotion ? DevotionWithComfort(rate) : rate;
    }

    // ---------------- comfort ----------------

    /// <summary>Devotion × (1 + Comfort / 200), kept between half and three times (GDD Economy).</summary>
    private int DevotionWithComfort(int rate)
    {
        var scaled = (long)rate * (_table.ComfortPerDoubleDevotion + Comfort()) / _table.ComfortPerDoubleDevotion;
        return (int)Math.Max(rate / 2, Math.Min(rate * 3L, scaled));
    }

    /// <summary>
    /// The village's Comfort: the Hearth's, plus every finished, intact building's (homes and baths add, middens
    /// subtract), minus extra Squalor for each home a midden stands beside.
    /// </summary>
    public int Comfort() => ComfortParts().Sum(p => p.Amount);

    /// <summary>Where the Comfort comes from (the Cult panel shows this): the Hearth, each kind of building, Squalor, research.</summary>
    public IReadOnlyList<(string Label, int Amount)> ComfortParts()
    {
        var parts = new List<(string, int)> { ("Hearth", _table.HearthComfort[Math.Min(Math.Max(HearthLevel, 1), _table.HearthComfort.Count) - 1]) };
        var standing = _buildings.Where(b => b.Level > 0 && !b.Wrecked && b.Type != BuildingType.Hearth).ToList();
        foreach (var group in standing.GroupBy(b => b.Type).OrderBy(g => g.Key))
        {
            var sum = group.Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].Comfort + (b.Type == BuildingType.Bathhouse ? Bonus(TechEffect.BathComfort) : 0));
            if (sum != 0)
            {
                parts.Add((sum > 0 ? _table.Spec(group.Key).Name : _table.Spec(group.Key).Name + " (Squalor)", sum));
            }
        }

        var smell = standing.Where(b => b.Type == BuildingType.MiddenSorter)
            .Sum(midden => standing.Count(h => IsHome(h.Type) && Gap(midden, h) <= _table.SqualorRange) * _table.SqualorNearHome);
        if (smell > 0)
        {
            parts.Add(("Midden smell next to homes", -smell));
        }

        if (Bonus(TechEffect.Comfort) != 0)
        {
            parts.Add(("Research", Bonus(TechEffect.Comfort)));
        }

        return parts;
    }

    private static bool IsHome(BuildingType type) => type is BuildingType.Cottage or BuildingType.CultistHut or BuildingType.Bathhouse;

    /// <summary>Tiles between two footprints (0: touching or overlapping; diagonals count as one).</summary>
    private static int Gap(Building a, Building b)
    {
        var best = int.MaxValue;
        foreach (var p in a.Tiles)
        {
            foreach (var q in b.Tiles)
            {
                best = Math.Min(best, Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y)) - 1);
            }
        }

        return Math.Max(0, best);
    }

    /// <summary>Peat burned per hour: every villager's fire, and every standing bathhouse's stove.</summary>
    private int PeatBurnPerHour() => _population * _table.PeatPerVillagerPerHour + _buildings
        .Where(b => b.Level > 0 && !b.Wrecked && _table.Spec(b.Type).Levels[b.Level - 1].PeatPerHour > 0)
        .Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].PeatPerHour);

    // ---------------- villagers ----------------

    /// <summary>The Hearth's beds plus every connected, finished, intact cottage's.</summary>
    private int Beds() => _table.HearthBeds + _buildings
        .Where(b => b.Level > 0 && !b.Wrecked && _table.Spec(b.Type).Levels[b.Level - 1].Beds > 0 && IsConnected(b))
        .Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].Beds);

    /// <summary>
    /// Villagers crew connected, finished, intact producers in building order; one that can't get its full crew gets
    /// none (the next, smaller one may still be crewed).
    /// </summary>
    private bool IsStaffed(Building building)
    {
        var need = _table.Spec(building.Type).Workers;
        if (need == 0)
        {
            return true;
        }

        var free = _population - MissionariesAway;
        foreach (var b in _buildings.OrderBy(b => b.Id))
        {
            var workers = _table.Spec(b.Type).Workers;
            if (workers == 0 || b.Level == 0 || b.Wrecked || !IsConnected(b))
            {
                continue;
            }

            var crewed = free >= workers;
            if (crewed)
            {
                free -= workers;
            }

            if (b == building)
            {
                return crewed;
            }
        }

        return false;
    }

    /// <summary>Burns Peat for every villager up to <paramref name="at"/> (nothing while cold: there's nothing to burn).</summary>
    private void MaterializeUpkeep(DateTimeOffset at)
    {
        if (at <= _upkeepAsOf)
        {
            return;
        }

        if (!_cold && PeatBurnPerHour() > 0)
        {
            var seconds = (at.UtcTicks - _upkeepAsOf.UtcTicks) / TimeSpan.TicksPerSecond;
            var burned = _upkeepMilli + PeatBurnPerHour() * 1000L * seconds / 3600;
            var whole = (int)Math.Min(burned / 1000, _stock.Peat);
            _stock -= new ResourceBag(0, whole, 0, 0);
            _upkeepMilli = whole < burned / 1000 ? 0 : burned - whole * 1000L;
        }

        _upkeepAsOf = at;
    }

    /// <summary>When the Peat in storage runs out at today's burn (null: it won't, or it already has).</summary>
    private DateTimeOffset? PeatRunsOutAt()
    {
        var perHour = PeatBurnPerHour();
        if (_cold || perHour <= 0)
        {
            return null;
        }

        var milliLeft = _stock.Peat * 1000L - _upkeepMilli;
        var seconds = (milliLeft * 3600 + perHour * 1000L - 1) / (perHour * 1000L); // round up: it's gone at the end of that second
        return _upkeepAsOf.AddSeconds(Math.Max(0, seconds));
    }

    /// <summary>After anything that changes beds or Peat: extra villagers leave; Peat back in storage warms everyone up.</summary>
    private void SettlePeople(DateTimeOffset at)
    {
        var beds = Beds();
        if (_population > beds)
        {
            _population = beds;
        }

        if (_population >= beds && at > _populationAsOf)
        {
            _populationAsOf = at; // full: the next newcomer comes an interval after a bed frees up
        }

        if (_cold && _stock.Peat > 0)
        {
            _cold = false;
            _upkeepAsOf = at > _upkeepAsOf ? at : _upkeepAsOf;
        }
    }

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
