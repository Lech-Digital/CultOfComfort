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
    public const int CurrentLayoutVersion = 5; // 4: production flows straight into the stores; 5: the Hearth's ground is 4×4

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
    private int _raidersRoad; // 0: trodden mud; else how many of the table's pavings are laid
    private readonly List<StowedTower> _stowed = new(); // towers packed up, waiting to be set down again
    private DateTimeOffset _nightStartedAt;
    private NightReport? _unseenReport;
    private readonly List<GridPosition> _cleared = new(); // boulders, gone for good
    private readonly Dictionary<GridPosition, DateTimeOffset> _treeCuts = new();
    private HashSet<GridPosition> _road => _map.RoadTiles(HearthLevel); // every raiders' road open at this Hearth
    private HashSet<GridPosition>? _network; // village road network, rebuilt after any building change
    private int _population;
    private DateTimeOffset _populationAsOf; // the next villager arrives an arrival interval after this
    private DateTimeOffset _upkeepAsOf;     // the stores are settled up to here: production in, Peat burned
    private readonly long[] _fractions = new long[4]; // thousandths of a resource beyond the whole stock (Timber, Peat, Scrap, Devotion)
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
        state._buildings.Add(new Building(HearthId, BuildingType.Hearth, map.Hearth, 1, nowUtc, map.HearthTiles));
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
            _cold = data.Cold,
            _errandIndex = table.Errands.IndexOf(data.ErrandId, data.ErrandIndex), // by id; old saves through the old order
            _errandEventDone = data.ErrandEventDone,
            _raidersRoad = Math.Clamp(data.RaidersRoad, 0, table.RaidersRoadPavings.Count),
        };
        foreach (var t in data.Stowed ?? Array.Empty<StowedTower>())
        {
            if (!table.Specs.TryGetValue(t.Type, out var stowedSpec) || !stowedSpec.IsTower || t.Level < 1 || t.Level > stowedSpec.MaxLevel)
            {
                throw new InvalidBaseDataException($"A packed-up {t.Type} at level {t.Level} isn't a tower.");
            }

            state._stowed.Add(t);
        }
        foreach (var b in data.Buildings.OrderBy(b => b.Type == BuildingType.Hearth ? -1 : b.Id))
        {
            if (!table.Specs.TryGetValue(b.Type, out var spec) || b.Level < 0 || b.Level > spec.MaxLevel)
            {
                throw new InvalidBaseDataException($"Building {b.Id} has an impossible type or level.");
            }

            var anchor = new GridPosition(b.X, b.Y);
            var tiles = b.Type == BuildingType.Hearth ? map.HearthTiles
                : data.LayoutVersion >= 1 ? Footprint.Tiles(spec, anchor, b.Rotated) : new[] { anchor }; // shapes arrived in layout version 1
            state._buildings.Add(new Building(b.Id, b.Type, anchor, b.Level, new DateTimeOffset(b.AsOfUtcTicks, TimeSpan.Zero), tiles, b.Rotated, b.Flipped)
            {
                Patrol = b.Patrol,
                BufferMilli = b.BufferMilli,
                UpgradeEndsAtUtc = b.UpgradeEndsAtUtcTicks == null ? null : new DateTimeOffset(b.UpgradeEndsAtUtcTicks.Value, TimeSpan.Zero),
                Wrecked = b.Wrecked,
                CrewWanted = b.Crew is int crew && crew >= 0 && crew <= spec.SlotsAt(spec.MaxLevel) ? crew : null,
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
        state.RestoreDaily(data.Daily);

        if (data.LayoutVersion < 1)
        {
            state.MigrateLayout();
        }
        else if (data.LayoutVersion < 5)
        {
            state.ClearHearthGround();
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
            state._cold = false;
        }

        state.RestoreFractions(data);
        if (data.LayoutVersion < 4)
        {
            state.SweepOldBuffers();
        }

        state.ClearRoads(); // a save from before more roads opened at its Hearth level
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
            Resettle(b);
        }
    }

    /// <summary>
    /// A save from before the Hearth's 4×4 ground: whatever stood on the ruin moves off it, the same way (nearest spot
    /// that fits, else refunded). A road under the ruin just goes; the ruin's own floor carries the network.
    /// </summary>
    private void ClearHearthGround()
    {
        var hearth = new HashSet<GridPosition>(_map.HearthTiles);
        var displaced = _buildings.Where(b => b.Type != BuildingType.Hearth && b.Tiles.Any(hearth.Contains)).OrderBy(b => b.Id).ToList();
        foreach (var b in displaced)
        {
            _buildings.Remove(b);
        }

        _network = null;
        foreach (var b in displaced.Where(b => !BuildingTable.IsRoad(b.Type)))
        {
            Resettle(b);
        }
    }

    /// <summary>
    /// A raiders' road that has opened (spec 2026-10-20 defence) takes its ground: the village's own tracks on it just go
    /// (the raiders' road carries the network), a gate may stay across it, anything else moves to the nearest spot that
    /// fits, else comes down with everything paid for it back. Nothing to clear: nothing happens.
    /// </summary>
    private void ClearRoads()
    {
        var road = new HashSet<GridPosition>(_map.OpenRoads(HearthLevel).Where(i => i > 0).SelectMany(_map.RoadAt)); // the first road was never built on
        var displaced = _buildings.Where(b => b.Type is not (BuildingType.Hearth or BuildingType.Gate) && b.Tiles.Any(road.Contains)).OrderBy(b => b.Id).ToList();
        if (displaced.Count == 0)
        {
            return;
        }

        foreach (var b in displaced)
        {
            _buildings.Remove(b);
        }

        _network = null;
        foreach (var b in displaced.Where(b => !BuildingTable.IsRoad(b.Type)))
        {
            Resettle(b);
        }
    }

    /// <summary>
    /// Puts an uprooted building back: its anchor if its shape fits, else turned, else the nearest spot (up to 4 tiles
    /// away) that fits; with no room it is taken down and everything paid for it comes back.
    /// </summary>
    private void Resettle(Building b)
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
            return;
        }

        var (anchor, rotated) = spot.Value;
        _buildings.Add(new Building(b.Id, b.Type, anchor, b.Level, b.AsOfUtc, Footprint.Tiles(spec, anchor, rotated), rotated, b.Flipped)
        {
            BufferMilli = b.BufferMilli,
            UpgradeEndsAtUtc = b.UpgradeEndsAtUtc,
            Wrecked = b.Wrecked,
            CrewWanted = b.CrewWanted,
            Patrol = b.Patrol,
        });
        _network = null;
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

        var network = new HashSet<GridPosition>(_map.HearthTiles);
        var frontier = new Queue<GridPosition>(_map.HearthTiles);
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
        if (b.Type == BuildingType.Hearth || BuildingTable.IsRoad(b.Type) || !BuildingTable.NeedsRoad(b.Type))
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
    private void MaterializeAll(DateTimeOffset at) => MaterializeEconomy(at);

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

        if (_population < 0 || _population > Beds() || _fractions.Any(f => f < 0 || f >= 1000))
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
                    && (!_map.CanStand(tile, b.Type, HearthLevel) || _map.IsSpawn(tile) || _map.IsHearthTile(tile) || (_road.Contains(tile) && b.Type != BuildingType.Gate)
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
        _buildings.Select(b => new BuildingData(b.Id, b.Type, b.Level, b.Position.X, b.Position.Y, b.UpgradeEndsAtUtc?.UtcTicks, b.BufferMilli, b.AsOfUtc.UtcTicks, b.Wrecked, b.Rotated, b.CrewWanted, b.Flipped, b.Patrol)).ToList(),
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
        0,
        _cold,
        _errandIndex,
        _errandEventDone,
        DomainToData(),
        ResearchToData(),
        _fractions.ToList(),
        DailyToData(),
        CurrentErrand?.Id,
        _raidersRoad,
        _stowed.ToList());

    public CommandOutcome Apply(BaseCommand command, DateTimeOffset nowUtc)
    {
        if (_recent.Contains(command.CommandId))
        {
            return new CommandOutcome(CommandStatus.Duplicate, Version);
        }

        Advance(nowUtc);
        EnsureDay(nowUtc);
        _now = nowUtc;
        MaterializeAll(nowUtc); // whatever this command changes, production up to now was earned under the old rules
        var roadsBefore = _buildings.Count(b => BuildingTable.IsRoad(b.Type));
        if (_night != null && command is PlaceBuilding or PaintTiles or MoveBuilding or RepairAll or UpgradeBuilding or UpgradeHearth or DemolishBuilding or RepairBuilding or FinishConstructionNow or RecordNightResult or ClearTerrain or SendMissionary or SendWorldMissionary or PayTithe or MeetDemand or PleadWithLord or SetCrew or CancelConstruction or PaveRaidersRoad or SetDuty or PackUpTower or PackUpTowers)
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
            PaveRaidersRoad _ => Pave(),
            PlaceBuilding place => Place(place, nowUtc),
            PaintTiles paint => Paint(paint, nowUtc),
            MoveBuilding move => Move(move),
            RepairAll _ => RepairEverything(nowUtc),
            UpgradeBuilding upgrade => Upgrade(Find(upgrade.BuildingId), nowUtc),
            UpgradeHearth _ => Upgrade(Hearth, nowUtc),
            DemolishBuilding demolish => Demolish(demolish.BuildingId),
            Collect _ => DoCollect(),
            RecordNightResult night => RecordNight(night, nowUtc),
            FinishConstructionNow _ => FinishNow(nowUtc),
            DebugRaiseTension raise => RaiseTension(raise.Amount, nowUtc),
            DebugSkipWarning _ => SkipWarning(nowUtc),
            DebugFillStores _ => FillStores(nowUtc),
            DebugEndShield _ => EndShield(nowUtc),
            SetCrew crew => ChooseCrew(crew),
            SetDuty duty => ChooseDuty(duty),
            PackUpTower pack => PackUp(Find(pack.BuildingId)),
            PackUpTowers _ => PackUpAll(),
            SendMissionary send => SendOut(send, nowUtc),
            SendWorldMissionary send => SendOutInWorld(send, nowUtc),
            JoinWorld join => Join(join, nowUtc),
            WorldUpdate update => ApplyWorldUpdate(update, nowUtc),
            ReceiveOfferings receive => Receive(receive.Offerings, nowUtc),
            CollectOfferings _ => CollectOfferings(nowUtc),
            PayTithe tithe => Tithe(tithe, nowUtc),
            MeetDemand meet => Meet(meet, nowUtc),
            PleadWithLord plea => Plead(plea, nowUtc),
            ChooseTale choose => Choose(choose, nowUtc),
            ClaimDaily claim => ClaimDailyErrand(claim, nowUtc),
            ClaimThermos _ => OpenThermos(nowUtc),
            SpendTowels spend => Spend(spend, nowUtc),
            SetDayClock clock => SetClock(clock),
            StartResearch start => StartStudy(start, nowUtc),
            CancelResearch _ => StopStudy(),
            CancelConstruction cancel => CancelBuild(cancel.BuildingId, nowUtc),
            Trade trade => DoTrade(trade),
            RepairBuilding repair => Repair(repair.BuildingId, nowUtc),
            _ => CommandOutcome.Reject("Unknown order."),
        };

        _network = null;
        SettlePeople(nowUtc);
        if (outcome.Status == CommandStatus.Accepted)
        {
            NoteDailyCommand(command, outcome, nowUtc, roadsBefore);
            EnsureDay(nowUtc); // the last starter errand claimed: the first daily list is today's
            Version++;
            Remember(command.CommandId);
        }

        return outcome with { Version = Version };
    }

    public BaseSnapshot Snapshot(DateTimeOffset nowUtc)
    {
        Advance(nowUtc);
        EnsureDay(nowUtc);
        _now = nowUtc;
        var views = _buildings.Select(b => new BuildingView(
            b.Id, b.Type, b.Level, b.Position, 0, 0, Rate(b), b.UpgradeEndsAtUtc, b.Wrecked, b.Rotated, b.Tiles, IsConnected(b),
            CrewOf(b), Slots(b), b.CrewWanted ?? Slots(b), b.Flipped, b.Patrol)).ToList();
        var tension = TensionValue(nowUtc);
        return new BaseSnapshot(Version, nowUtc, HearthLevel, _stock, Caps, views, SluiceBonus, LastNightRecorded,
            tension, _table.Tension.StateOf(tension), _raid?.WarningEndsAtUtc, _shieldEndsAt > nowUtc ? _shieldEndsAt : null, _night, _unseenReport, _cleared.Concat(_treeCuts.Keys.Where(t => !IsWildTreeStanding(t))).ToList(),
            _population, Beds(), _cold, ErrandNow(), Comfort(), DomainViewAt(nowUtc), ResearchViews(), ComfortParts(), IdleVillagers(), DailyViewAt(nowUtc), _raidersRoad, NextRoad, _stowed.ToList());
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

    public int SluiceBonus => _buildings.Where(b => b.Level > 0 && !b.Wrecked && IsConnected(b)).Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].SluiceCharges)
        + Bonus(TechEffect.SluiceCharges);

    /// <summary>
    /// Which road the raiders take on Night <paramref name="night"/> (spec 2026-10-20 defence): one of the roads open at
    /// this Hearth, fixed by the Night's number and the raid's seed, so the scout can tell at dusk.
    /// </summary>
    public int TonightsRoad(int night, ulong seed)
    {
        var open = _map.OpenRoads(HearthLevel);
        if (open.Count == 1)
        {
            return open[0];
        }

        unchecked
        {
            var h = (seed * 0x9E3779B97F4A7C15UL) ^ ((ulong)night * 0xBF58476D1CE4E5B9UL);
            h ^= h >> 31;
            return open[(int)(h % (ulong)open.Count)];
        }
    }

    /// <summary>The road the next Night will come by, as the scout tells it.</summary>
    public int NextRoad
    {
        get
        {
            var night = LastNightRecorded + 1;
            return _night?.Road ?? TonightsRoad(night, _raid?.Seed ?? (ulong)(946 + night));
        }
    }

    /// <summary>The locked layout for a Night: towers fight, every other building is in the way.</summary>
    public BattleLayout ToBattleLayout() => ToBattleLayout(0);

    private BattleLayout ToBattleLayout(int road)
    {
        return _map.ToBattleLayout(
            _buildings.Where(b => _table.Spec(b.Type).IsTower).Select(b => new PlacedTower(_table.Spec(b.Type).Tower!.Value, b.Position, b.Level)),
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
            _cleared,
            road);
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

        var unpacked = spec.IsTower ? Unpack(command.Type) : null;
        if (spec.IsTower && unpacked == null && TowerCount >= _table.TowerCap(HearthLevel))
        {
            return CommandOutcome.Reject($"{_table.TowerCap(HearthLevel)} towers is all this Hearth can keep. Upgrade one, or grow the Hearth.");
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

        var cost = unpacked != null ? default : spec.Levels[0].Cost; // a packed-up tower is set down for free
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        var building = new Building(_nextId++, command.Type, command.Position, unpacked?.Level ?? (instant ? 1 : 0), now,
            Footprint.Tiles(spec, command.Position, command.Rotated), command.Rotated, command.Flipped)
        {
            Wrecked = unpacked?.Wrecked ?? false,
        };
        if (unpacked != null)
        {
            _stowed.Remove(unpacked);
        }
        if (!instant)
        {
            building.UpgradeEndsAtUtc = now.AddSeconds(BuildSeconds(spec.Levels[0].BuildSeconds));
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
            Footprint.Tiles(spec, command.Position, command.Rotated), command.Rotated, command.Flipped)
        {
            BufferMilli = building.BufferMilli,
            UpgradeEndsAtUtc = building.UpgradeEndsAtUtc,
            Wrecked = building.Wrecked,
            CrewWanted = building.CrewWanted,
            Patrol = building.Patrol,
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
        if (building.Level >= spec.MaxLevel)
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
        MaterializeEconomy(now);
        building.UpgradeEndsAtUtc = now.AddSeconds(BuildSeconds(spec.Levels[building.Level].BuildSeconds));
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

    /// <summary>Old clients still send Collect: there's nothing to tap any more, production reaches the stores by itself.</summary>
    private static CommandOutcome DoCollect() => CommandOutcome.Reject("No need: the stores fill up by themselves now.");

    private CommandOutcome RecordNight(RecordNightResult night, DateTimeOffset now)
    {
        if (night.Night <= LastNightRecorded)
        {
            return CommandOutcome.Reject("That Night is already in the books.");
        }

        var salvage = Math.Min(Salvage(night.Stars), Math.Max(0, Caps.Scrap - _stock.Scrap));
        _stock += new ResourceBag(0, 0, salvage, 0);
        LastNightRecorded = night.Night;
        NoteNight(night.Stars, now);
        foreach (var id in night.WreckedBuildingIds ?? Array.Empty<int>())
        {
            var building = Find(id);
            if (building != null && building.Type != BuildingType.Hearth)
            {
                MaterializeEconomy(now);
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
        MaterializeEconomy(now); // nothing accrued while wrecked; production restarts from now
        building.Wrecked = false;
        return CommandOutcome.Accept();
    }

    private CommandOutcome CancelBuild(int id, DateTimeOffset now)
    {
        var building = Find(id);
        if (building == null || !building.IsUpgrading)
        {
            return CommandOutcome.Reject(building == null ? "That building is gone." : "Nothing is being built there.");
        }

        MaterializeTension(now);
        MaterializeEconomy(now);
        var spec = _table.Spec(building.Type);
        var refund = Refund(spec.Levels[building.Level].Cost); // a new one paid for level 1; an upgrade for the next level
        building.UpgradeEndsAtUtc = null;
        if (building.Level == 0)
        {
            _buildings.Remove(building);
        }
        else if (now > building.AsOfUtc)
        {
            building.AsOfUtc = now;
        }

        return CommandOutcome.Accept(refund);
    }

    /// <summary>Gives <paramref name="cost"/> back, as far as the stores have room (like every gain: never over the cap).</summary>
    private ResourceBag Refund(ResourceBag cost)
    {
        var caps = Caps;
        var back = new ResourceBag(
            Math.Min(cost.Timber, Math.Max(0, caps.Timber - _stock.Timber)),
            Math.Min(cost.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(cost.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)),
            Math.Min(cost.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        _stock += back;
        return back;
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
            if (_map.Contains(tile) && !_map.CanStand(tile, spec.Type, HearthLevel))
            {
                return _map.LockedReason(tile, spec.Type);
            }
        }

        if (_map.AltarProblem(spec.Type, tiles) is { } altar)
        {
            return altar;
        }

        foreach (var tile in tiles)
        {
            if (!_map.Contains(tile) || _map.IsSpawn(tile) || _map.IsHearthTile(tile))
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
            if (!_map.Contains(tile) || _map.IsSpawn(tile) || _map.IsHearthTile(tile) || _buildings.Any(b => b != ignore && b.Tiles.Contains(tile)) || _map.IsBedrock(tile))
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
            var warms = Due(WarmsUpAt(), now);
            var domain = Due(NextDomainEvent(), now);
            var study = Due(StudyEnds(), now);
            var next = Earliest(construction, crossing, deadline, abandoned, arrival, runsOut, warms, domain, study);
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
                _fractions[(int)ResourceType.Peat] = 0;
                _cold = true;
                continue;
            }

            if (next == warms)
            {
                _cold = false; // an hour's Peat is stacked: the fires are lit again
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
    public const int FirstNightBudget = 12;
    public const int FirstNightWaves = 3;

    private NightPlan PlanNight(DateTimeOffset now)
    {
        MaterializeTension(now);
        var tension = TensionValue(now);
        var power = _table.Tension.BasePower(HearthLevel);
        var night = LastNightRecorded + 1;
        var layout = ToBattleLayout();
        var budget = _raid != null ? power * (100 + tension - _table.Tension.Hostile) / 100 : Math.Max(power * tension / _table.Tension.Hostile, power / 2);
        var waves = _raid != null && tension >= _table.Tension.Punitive ? 5 : 4;
        if (night == 1 && _raid == null)
        {
            // The first Night teaches (spec 2026-10-16 first day): won by a Chute alone, three stars for a Prophet who
            // fights. At the old size (20, four waves) nothing a new village has could win it, skills included.
            budget = Math.Min(budget, FirstNightBudget);
            waves = FirstNightWaves;
        }

        var seed = _raid?.Seed ?? (ulong)(946 + night);
        var road = TonightsRoad(night, seed);
        layout = road == 0 ? layout : ToBattleLayout(road);
        var (towers, wardens) = Garrison(layout);
        return new NightPlan(night, seed, budget, waves, SluiceBonus, _raid != null, towers, layout.Obstacles, layout.Structures, HearthLevel,
            AutoRelics: true, Defenders: DefenderPosts(layout).Concat(wardens).ToList(),
            TowerDamagePercent: Bonus(TechEffect.TowerDamagePercent), ProphetHealthPercent: Bonus(TechEffect.ProphetHealthPercent), // research, frozen into the plan
            Road: road);
    }

    /// <summary>
    /// The Guardhouses' wardens (spec 2026-10-20 defence): each Guardhouse's crew mans the nearest unmanned towers within
    /// its reach (they fire faster), oldest Guardhouse first; on patrol, or with no tower left to man, they come out and
    /// fight from beside it, like a hut's cultists.
    /// </summary>
    private (IReadOnlyList<PlacedTower> Towers, List<GridPosition> Wardens) Garrison(BattleLayout layout)
    {
        var towers = layout.Towers.ToList();
        var wardens = new List<GridPosition>();
        foreach (var house in _buildings.Where(b => b.Type == BuildingType.Guardhouse && b.Level >= 1 && !b.Wrecked).OrderBy(b => b.Id))
        {
            var crew = CrewOf(house);
            if (!house.Patrol)
            {
                var near = Enumerable.Range(0, towers.Count)
                    .Where(i => !towers[i].Manned && Reach(house, towers[i].Position) <= _table.GuardhouseReach)
                    .OrderBy(i => Reach(house, towers[i].Position)).ThenBy(i => i)
                    .Take(crew).ToList();
                foreach (var i in near)
                {
                    towers[i] = towers[i] with { Manned = true };
                }

                crew -= near.Count;
            }

            var post = PostBeside(house, layout);
            if (post != null)
            {
                wardens.AddRange(Enumerable.Repeat(post.Value, crew));
            }
        }

        return (towers, wardens);
    }

    /// <summary>Tiles from a building's walls to a tile (Chebyshev).</summary>
    private static int Reach(Building b, GridPosition tile) =>
        b.Tiles.Min(t => Math.Max(Math.Abs(t.X - tile.X), Math.Abs(t.Y - tile.Y)));

    /// <summary>The open tile beside a building nearest the Hearth, where its people stand guard (null: walled in).</summary>
    private GridPosition? PostBeside(Building b, BattleLayout layout) =>
        b.Tiles
            .SelectMany(t => new[] { new GridPosition(t.X + 1, t.Y), new GridPosition(t.X - 1, t.Y), new GridPosition(t.X, t.Y + 1), new GridPosition(t.X, t.Y - 1) })
            .Where(t => !b.Tiles.Contains(t) && layout.IsWalkableForLeader(t))
            .OrderBy(t => Math.Abs(t.X - _map.Hearth.X) + Math.Abs(t.Y - _map.Hearth.Y)).ThenBy(t => t.X).ThenBy(t => t.Y)
            .Cast<GridPosition?>().FirstOrDefault();

    /// <summary>
    /// Who comes out to defend (spec 2026-10-15): each standing Cultist Hut sends out as many cultists as its level, all
    /// posted on the open tile beside it nearest the Hearth.
    /// </summary>
    private List<GridPosition> DefenderPosts(BattleLayout layout)
    {
        var posts = new List<GridPosition>();
        foreach (var hut in _buildings.Where(b => b.Type == BuildingType.CultistHut && b.Level >= 1 && !b.Wrecked).OrderBy(b => b.Id))
        {
            var post = hut.Tiles
                .SelectMany(t => new[] { new GridPosition(t.X + 1, t.Y), new GridPosition(t.X - 1, t.Y), new GridPosition(t.X, t.Y + 1), new GridPosition(t.X, t.Y - 1) })
                .Where(t => !hut.Tiles.Contains(t) && layout.IsWalkableForLeader(t))
                .OrderBy(t => Math.Abs(t.X - _map.Hearth.X) + Math.Abs(t.Y - _map.Hearth.Y)).ThenBy(t => t.X).ThenBy(t => t.Y)
                .Cast<GridPosition?>().FirstOrDefault();
            if (post != null)
            {
                posts.AddRange(Enumerable.Repeat(post.Value, hut.Level));
            }
        }

        return posts;
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

        var salvage = Math.Min(Salvage(result.Stars), Math.Max(0, Caps.Scrap - _stock.Scrap));
        _stock += new ResourceBag(0, 0, salvage, 0);
        foreach (var id in wrecked)
        {
            var building = Find(id);
            if (building != null && building.Type != BuildingType.Hearth)
            {
                MaterializeEconomy(at);
                building.Wrecked = true;
            }
        }

        MaterializeTension(at);
        _tensionMilli = 0;
        _shieldEndsAt = at.AddHours(_table.Tension.ShieldHours);
        _raid = null;
        _night = null;
        LastNightRecorded = plan.Night;
        NoteNight(result.Stars, at);
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
        MaterializeEconomy(endedAt);
        building.Level++;
        building.UpgradeEndsAtUtc = null;
        if (building.Type == BuildingType.Hearth)
        {
            ClearRoads(); // a new road opens: whatever stood on it moves aside
        }
        if (endedAt > building.AsOfUtc)
        {
            building.AsOfUtc = endedAt; // never backwards: time already counted is never counted again
        }

        if (building.Type == BuildingType.Hearth && building.Level == _table.Domain.UnlockHearth)
        {
            StartDemands(endedAt);
        }

        var spec = _table.Spec(building.Type);
        if (building.Level == 1 && spec.Produces != null)
        {
            // The crew finds some on day one (deadwood, a peat stack): a new producer starts with a little in store.
            var pile = spec.Levels[0].PerHour * _table.StarterStockMinutes / 60;
            var room = Math.Max(0, Caps.Get(spec.Produces.Value) - _stock.Get(spec.Produces.Value));
            _stock += ResourceBag.Of(spec.Produces.Value, Math.Min(pile, room));
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
    private long TensionRateMilli() =>
        (_table.Tension.RatePerHour(HearthLevel) * 1000L + DomainTensionPerHourMilli()) * (100 - Math.Min(90, Bonus(TechEffect.TemperPercent))) / 100;

    /// <summary>A construction's time after research (Measure Twice): never under a second, if it took any.</summary>
    private int BuildSeconds(int seconds) => seconds <= 0 ? seconds : Math.Max(1, seconds * (100 - Math.Min(90, Bonus(TechEffect.BuildTimePercent))) / 100);

    /// <summary>The Scrap a Night leaves behind, with research (Finders Keepers).</summary>
    private int Salvage(int stars) => _table.Salvage(stars) * (100 + Bonus(TechEffect.SalvagePercent)) / 100;

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
        if (b.Wrecked || (_cold && _table.Spec(b.Type).Produces != ResourceType.Peat) || !IsConnected(b) || (Slots(b) > 0 && CrewOf(b) == 0))
        {
            return 0;
        }

        var produces = _table.Spec(b.Type).Produces;
        var research = Bonus(produces switch
        {
            ResourceType.Timber => TechEffect.TimberPercent,
            ResourceType.Peat => TechEffect.PeatPercent,
            ResourceType.Scrap => TechEffect.ScrapPercent,
            _ => TechEffect.DevotionPercent,
        });
        var rate = (int)((long)LevelRate(b) * BuildingSpec.CrewPerMille(CrewOf(b), Slots(b)) / 1000) * (100 + RoadBonus(b) + research) / 100;
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

        if (_raidersRoad > 0)
        {
            parts.Add(($"The raiders' road ({_table.RaidersRoadPavings[_raidersRoad - 1].Name.ToLowerInvariant()})", _table.RaidersRoadPavings[_raidersRoad - 1].Comfort));
        }

        return parts;
    }

    private CommandOutcome Pave()
    {
        if (_raidersRoad >= _table.RaidersRoadPavings.Count)
        {
            return CommandOutcome.Reject("The raiders' road is as fine as it gets. They still won't wipe their feet.");
        }

        var next = _table.RaidersRoadPavings[_raidersRoad];
        if (HearthLevel < next.Hearth)
        {
            return CommandOutcome.Reject($"{next.Name} needs Hearth level {next.Hearth}.");
        }

        if (!_stock.Covers(next.Cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(next.Cost)} more.");
        }

        _stock -= next.Cost;
        _raidersRoad++;
        return CommandOutcome.Accept();
    }

    private static bool IsHome(BuildingType type) => type is BuildingType.Cottage or BuildingType.CultistHut or BuildingType.Bathhouse or BuildingType.Sauna;

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

    /// <summary>Worker slots at the building's current level.</summary>
    private int Slots(Building b) => _table.Spec(b.Type).SlotsAt(b.Level);

    /// <summary>
    /// Who works where (spec 2026-10-14 cultist work): the villagers at home fill each connected, finished, intact
    /// producer's chosen crew in building order, oldest first; when hands run short, the newest go without.
    /// </summary>
    private int CrewOf(Building building)
    {
        if (Slots(building) == 0)
        {
            return 0;
        }

        var free = _population - MissionariesAway;
        foreach (var b in _buildings.OrderBy(b => b.Id))
        {
            var slots = Slots(b);
            if (slots == 0 || b.Wrecked || !IsConnected(b))
            {
                continue;
            }

            var crew = Math.Max(0, Math.Min(Math.Min(b.CrewWanted ?? slots, slots), free));
            free -= crew;
            if (b == building)
            {
                return crew;
            }
        }

        return 0;
    }

    /// <summary>Villagers at home with no work: what's left after every crew is filled.</summary>
    private int IdleVillagers() => Math.Max(0, _population - MissionariesAway - _buildings.Sum(CrewOf));

    /// <summary>Towers standing or packed up, every kind together (the Hearth's cap counts these).</summary>
    public int TowerCount => _buildings.Count(b => _table.Spec(b.Type).IsTower) + _stowed.Count;

    private CommandOutcome PackUp(Building? tower)
    {
        if (tower == null || !_table.Spec(tower.Type).IsTower)
        {
            return CommandOutcome.Reject("Only towers fold up.");
        }

        _stowed.Add(new StowedTower(tower.Type, tower.Level, tower.Wrecked));
        _buildings.Remove(tower);
        return CommandOutcome.Accept();
    }

    private CommandOutcome PackUpAll()
    {
        var towers = _buildings.Where(b => _table.Spec(b.Type).IsTower).OrderBy(b => b.Id).ToList();
        if (towers.Count == 0)
        {
            return CommandOutcome.Reject("No towers standing to pack up.");
        }

        foreach (var tower in towers)
        {
            PackUp(tower);
        }

        return CommandOutcome.Accept();
    }

    /// <summary>The packed-up tower of this kind to set down: the best level, a sound one before a wreck.</summary>
    private StowedTower? Unpack(BuildingType type) =>
        _stowed.Where(t => t.Type == type).OrderBy(t => t.Wrecked).ThenByDescending(t => t.Level).FirstOrDefault();

    private CommandOutcome ChooseDuty(SetDuty command)
    {
        var building = Find(command.BuildingId);
        if (building == null || building.Type != BuildingType.Guardhouse)
        {
            return CommandOutcome.Reject("Only a Guardhouse has wardens to send.");
        }

        building.Patrol = command.Patrol;
        return CommandOutcome.Accept();
    }

    private CommandOutcome ChooseCrew(SetCrew command)
    {
        var building = _buildings.FirstOrDefault(b => b.Id == command.BuildingId);
        if (building == null)
        {
            return CommandOutcome.Reject("There's nothing there.");
        }

        var slots = Slots(building);
        if (slots == 0)
        {
            return CommandOutcome.Reject("Nobody works there.");
        }

        if (command.Crew < 0 || command.Crew > slots)
        {
            return CommandOutcome.Reject($"It has room for {slots}.");
        }

        building.CrewWanted = command.Crew == slots ? null : command.Crew; // full stays full when it's upgraded
        return CommandOutcome.Accept();
    }

    private void MaterializeUpkeep(DateTimeOffset at) => MaterializeEconomy(at);

    /// <summary>What the village makes of a resource per hour now (every producer of it, at its crew and bonuses).</summary>
    private int ProductionPerHour(ResourceType resource) =>
        _buildings.Where(b => _table.Spec(b.Type).Produces == resource).Sum(Rate);

    /// <summary>The stores' change per hour: production in, and for Peat the fires out (nothing burns while cold).</summary>
    private int NetPerHour(ResourceType resource) =>
        ProductionPerHour(resource) - (resource == ResourceType.Peat && !_cold ? PeatBurnPerHour() : 0);

    /// <summary>
    /// Settles the stores up to <paramref name="at"/> (spec 2026-10-14 auto-collect): every producer's output goes
    /// straight in, capped by storage; the fires burn Peat. Rates are constant between events, so this is exact.
    /// </summary>
    private void MaterializeEconomy(DateTimeOffset at)
    {
        if (at <= _upkeepAsOf)
        {
            return; // a clock that went backwards never takes anything away
        }

        var seconds = (at.UtcTicks - _upkeepAsOf.UtcTicks) / TimeSpan.TicksPerSecond;
        var caps = Caps;
        var whole = new int[4];
        for (var i = 0; i < 4; i++)
        {
            var resource = (ResourceType)i;
            var perHour = NetPerHour(resource);
            var current = _stock.Get(resource) * 1000L + _fractions[i];
            var cap = caps.Get(resource) * 1000L;
            var next = perHour >= 0
                ? (current >= cap ? current : Math.Min(cap, current + perHour * 1000L * seconds / 3600)) // full stores take no more, and lose nothing
                : Math.Max(0, current + perHour * 1000L * seconds / 3600);
            whole[i] = (int)(next / 1000);
            _fractions[i] = next % 1000;
        }

        _stock = new ResourceBag(whole[0], whole[1], whole[2], whole[3]);
        _upkeepAsOf = at;
    }

    /// <summary>When the Peat runs out at today's net burn (null: production keeps up, or it's already cold).</summary>
    private DateTimeOffset? PeatRunsOutAt()
    {
        var net = NetPerHour(ResourceType.Peat);
        if (_cold || net >= 0)
        {
            return null;
        }

        var milliLeft = _stock.Peat * 1000L + _fractions[(int)ResourceType.Peat];
        var seconds = (milliLeft * 3600 + -net * 1000L - 1) / (-net * 1000L); // round up: it's gone at the end of that second
        return _upkeepAsOf.AddSeconds(Math.Max(0, seconds));
    }

    /// <summary>A cold village relights its fires once an hour's burn is stacked (so they don't flicker on and off at zero).</summary>
    private long WarmUpMilli() => Math.Max(1, PeatBurnPerHour()) * 1000L;

    /// <summary>When the peat cutters, working through the cold, have stacked enough to relight (null: warm, or nobody's cutting).</summary>
    private DateTimeOffset? WarmsUpAt()
    {
        if (!_cold)
        {
            return null;
        }

        var have = _stock.Peat * 1000L + _fractions[(int)ResourceType.Peat];
        var need = WarmUpMilli() - have;
        if (need <= 0)
        {
            return _upkeepAsOf;
        }

        var perHour = ProductionPerHour(ResourceType.Peat);
        if (perHour <= 0 || _stock.Peat >= Caps.Peat)
        {
            return null;
        }

        return _upkeepAsOf.AddSeconds((need * 3600 + perHour * 1000L - 1) / (perHour * 1000L));
    }

    /// <summary>Old saves kept output in each producer: it goes into the stores (what doesn't fit is lost), once.</summary>
    private void SweepOldBuffers()
    {
        var caps = Caps;
        foreach (var b in _buildings)
        {
            var resource = _table.Spec(b.Type).Produces;
            if (resource == null)
            {
                continue;
            }

            var made = (int)(Buffer(b, _upkeepAsOf) / 1000);
            var room = Math.Max(0, caps.Get(resource.Value) - _stock.Get(resource.Value));
            _stock += ResourceBag.Of(resource.Value, Math.Min(made, room));
            b.BufferMilli = 0;
            b.AsOfUtc = _upkeepAsOf;
        }
    }

    private void RestoreFractions(BaseData data)
    {
        if (data.StockFractions == null || data.StockFractions.Count != 4)
        {
            return;
        }

        for (var i = 0; i < 4; i++)
        {
            _fractions[i] = data.StockFractions[i];
        }
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

        if (_cold && _stock.Peat * 1000L + _fractions[(int)ResourceType.Peat] >= WarmUpMilli())
        {
            _cold = false; // Peat bought, filled or made: enough for an hour, so the fires are lit
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
