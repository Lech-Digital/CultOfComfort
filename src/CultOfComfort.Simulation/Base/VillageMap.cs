using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>A rectangle of forest that opens for building (and for raiders) at a Hearth level.</summary>
public sealed record MapZone(int MinX, int MinY, int MaxX, int MaxY, int OpensAtHearth)
{
    public bool Contains(GridPosition tile) => tile.X >= MinX && tile.X <= MaxX && tile.Y >= MinY && tile.Y <= MaxY;
}

/// <summary>
/// A raiders' road beyond the first (spec 2026-10-20 defence): it opens at a Hearth level and runs from the forest's
/// edge to the Hearth's fire in straight runs between <paramref name="Corners"/>.
/// </summary>
public sealed record RaiderRoute(string Name, int OpensAtHearth, IReadOnlyList<GridPosition> Corners)
{
    public IReadOnlyList<GridPosition> Tiles()
    {
        var tiles = new List<GridPosition> { Corners[0] };
        for (var i = 1; i < Corners.Count; i++)
        {
            var (from, to) = (Corners[i - 1], Corners[i]);
            var (dx, dy) = (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
            if (dx != 0 && dy != 0)
            {
                throw new ArgumentException($"{Name}: corners {from} and {to} aren't in a straight line.");
            }

            for (var at = from; at != to;)
            {
                at = new GridPosition(at.X + dx, at.Y + dy);
                tiles.Add(at);
            }
        }

        return tiles;
    }
}

/// <summary>
/// The ground the village and the Night share: the full grid, spawn, Hearth tile, rocks, and forest zones that open as
/// the Hearth grows (spec: Map expansion). Locked land is unbuildable and blocks raiders, so the road never changes.
/// </summary>
public sealed record VillageMap(
    int Width,
    int Height,
    GridPosition Spawn,
    GridPosition Hearth,
    IReadOnlyList<GridPosition> Rocks,
    IReadOnlyList<MapZone>? Zones = null,
    IReadOnlyList<GridPosition>? Boulders = null,
    int SpawnClearance = 0)
{
    private IReadOnlyList<GridPosition>? _wildTrees;
    private IReadOnlyList<GridPosition>? _road;

    // Declared before Demo: static initialisers run in order, and Demo needs these.
    private static readonly GridPosition[] DemoBoulders =
    {
        new(1, 6), new(2, 9), new(8, 4), new(14, 1), new(16, 4), new(8, 0),
        new(20, 5), new(14, 16), new(26, 12), new(7, 18),
        new(34, 8), new(20, 23), new(42, 18), new(31, 28), new(8, 27), new(45, 4),
    };

    /// <summary>
    /// The muddy valley. Building the village is the heart of the game, so there is room: 30×20 open at Hearth 1,
    /// 38×26 at Hearth 2, the whole 48×32 at Hearth 3. The road between spawn and Hearth never changes.
    /// </summary>
    public static VillageMap Demo { get; } = new(68, 48, new GridPosition(0, 1), new GridPosition(17, 9), DemoRocks(), new[]
    {
        new MapZone(0, 20, 37, 25, 2),
        new MapZone(30, 0, 37, 19, 2),
        new MapZone(0, 26, 47, 31, 3),
        new MapZone(38, 0, 47, 25, 3),
        // Spec 2026-10-20 river: the near bank opens at Hearth 4, down to the water; the river and the far bank are
        // the raiders' country and never open.
        new MapZone(0, 32, 52, 35, 4),
        new MapZone(48, 0, 52, 31, 4),
        new MapZone(0, 36, 67, 47, Wild),
        new MapZone(53, 0, 67, 35, Wild),
    }, DemoBoulders, SpawnClearance: 3)
    {
        HearthSize = 4,
        River = DemoRiver,
        // Spec 2026-10-20 defence: more roads as the Hearth grows, so no one layout covers every Night. Laid over open
        // ground and the village's own tracks, round the rock and the boulders.
        Routes = new[]
        {
            // From the far bank, over a bridge, through the forest to the village (owner 2026-10-20: "the south and east
            // roads should actually start on the other sides of our river").
            new RaiderRoute("south road", 2, new GridPosition[] { new(28, 47), new(28, 13), new(19, 13), new(19, 9), new(17, 9) }),
            new RaiderRoute("east road", 3, new GridPosition[] { new(67, 17), new(28, 17), new(28, 13), new(19, 13), new(19, 9), new(17, 9) }),
        },
    };

    /// <summary>A zone's Hearth level for land that never opens: the river and the far bank.</summary>
    public const int Wild = 99;

    /// <summary>
    /// The river (spec 2026-10-20 river): how far a point (tile units, tile centres on whole numbers) is from the water's
    /// edge; negative in the water. Null: no river on this map.
    /// </summary>
    public Func<double, double, double>? River { get; init; }

    private HashSet<GridPosition>? _water;

    /// <summary>Tiles whose middle is in the river. Bridges are the road tiles among them.</summary>
    public HashSet<GridPosition> Water => _water ??= River == null ? new HashSet<GridPosition>()
        : new HashSet<GridPosition>(Enumerable.Range(0, Width * Height).Select(i => new GridPosition(i % Width, i / Width)).Where(t => River(t.X, t.Y) < 0));

    public bool IsBridge(GridPosition tile) => Water.Contains(tile) && AllRoadTiles.Contains(tile);

    /// <summary>
    /// The demo valley's river: in from the west below the village, a band three to four tiles wide winding east along
    /// rows 36–42, round a bend and north up columns 53–59 to the top. Pure maths, so the ground painter draws its banks
    /// smooth and the simulation knows its tiles.
    /// </summary>
    private static double DemoRiver(double x, double y)
    {
        double South(double px) => 39.0 + Math.Sin(px * 0.21) * 0.9 + Math.Sin(px * 0.07 + 1.0) * 0.4;
        double East(double py) => 56.0 + Math.Sin(py * 0.24 + 0.5) * 0.9 + Math.Sin(py * 0.09 + 2.0) * 0.4;
        double HalfWidth(double t) => 1.6 + Math.Sin(t * 0.15 + 2.0) * 0.35;
        // The bend: where the east reach's line meets the south reach's; each reach runs to it, and a disc rounds it off.
        var (cornerX, cornerY) = (East(39.0), South(56.0));
        var south = x <= cornerX ? Math.Abs(y - South(x)) - HalfWidth(x) : double.MaxValue;
        var east = y <= cornerY ? Math.Abs(x - East(y)) - HalfWidth(y + 40.0) : double.MaxValue;
        var bend = Math.Sqrt((x - cornerX) * (x - cornerX) + (y - cornerY) * (y - cornerY)) - HalfWidth(cornerX);
        return Math.Min(south, Math.Min(east, bend));
    }

    /// <summary>Roads beyond the first (<see cref="Road"/>), each opening at its Hearth level.</summary>
    public IReadOnlyList<RaiderRoute> Routes { get; init; } = Array.Empty<RaiderRoute>();

    /// <summary>What the first road is called when there are others.</summary>
    public string FirstRoadName { get; init; } = "north road";

    private IReadOnlyList<GridPosition>[]? _routeTiles;
    private readonly Dictionary<int, HashSet<GridPosition>> _roadTilesAt = new();
    private HashSet<GridPosition>? _allRoadTiles;

    /// <summary>Every road, opened or not (0 is <see cref="Road"/>).</summary>
    public int RoadCount => 1 + Routes.Count;

    public IReadOnlyList<GridPosition> RoadAt(int index)
    {
        if (index <= 0 || index > Routes.Count)
        {
            return Road;
        }

        _routeTiles ??= Routes.Select(r => r.Tiles()).ToArray();
        return _routeTiles[index - 1];
    }

    public string RoadName(int index) => index <= 0 || index > Routes.Count ? FirstRoadName : Routes[index - 1].Name;

    public int RoadOpensAt(int index) => index <= 0 || index > Routes.Count ? 1 : Routes[index - 1].OpensAtHearth;

    /// <summary>The roads raiders may come by at this Hearth level, by index.</summary>
    public IReadOnlyList<int> OpenRoads(int hearthLevel) => Enumerable.Range(0, RoadCount).Where(i => RoadOpensAt(i) <= hearthLevel).ToList();

    /// <summary>Every tile of every road open at this Hearth level: nobody builds there.</summary>
    public HashSet<GridPosition> RoadTiles(int hearthLevel)
    {
        lock (_roadTilesAt)
        {
            if (!_roadTilesAt.TryGetValue(hearthLevel, out var tiles))
            {
                tiles = new HashSet<GridPosition>(OpenRoads(hearthLevel).SelectMany(RoadAt));
                _roadTilesAt[hearthLevel] = tiles;
            }

            return tiles;
        }
    }

    /// <summary>Every road's tiles, opened or not (no wild tree grows on them).</summary>
    public HashSet<GridPosition> AllRoadTiles => _allRoadTiles ??= new HashSet<GridPosition>(Enumerable.Range(0, RoadCount).SelectMany(RoadAt));

    /// <summary>Where a road comes out of the forest (raiders appear there).</summary>
    public GridPosition SpawnOf(int index) => index <= 0 || index > Routes.Count ? Spawn : RoadAt(index)[0];

    public bool IsSpawn(GridPosition tile) => tile == Spawn || Routes.Any(r => r.Corners[0] == tile);

    /// <summary>
    /// The Hearth's ground is <c>HearthSize</c> × <c>HearthSize</c> tiles: the Roman ruin and the Prophet's car (owner
    /// 2026-10-14: "from level one, way bigger"). <see cref="Hearth"/> is the fire, one tile in from the north-west corner.
    /// Raiders still march to the fire, across the ruin's floor.
    /// </summary>
    public int HearthSize { get; init; } = 1;

    private IReadOnlyList<GridPosition>? _hearthTiles;

    public IReadOnlyList<GridPosition> HearthTiles => _hearthTiles ??=
        Footprint.Tiles(new GridPosition(Hearth.X - (HearthSize - 1) / 2, Hearth.Y - (HearthSize - 1) / 2), HearthSize, HearthSize, false);

    public bool IsHearthTile(GridPosition tile) => HearthTiles.Contains(tile);

    /// <summary>Chebyshev distance from the Hearth's ground (0 on it).</summary>
    public int DistanceFromHearth(GridPosition tile)
    {
        var first = HearthTiles[0];
        var dx = tile.X < first.X ? first.X - tile.X : System.Math.Max(0, tile.X - (first.X + HearthSize - 1));
        var dy = tile.Y < first.Y ? first.Y - tile.Y : System.Math.Max(0, tile.Y - (first.Y + HearthSize - 1));
        return System.Math.Max(dx, dy);
    }

    /// <summary>Rock that can never be cleared: the walls that shape the raiders' road.</summary>
    public bool IsBedrock(GridPosition tile) => Rocks.Contains(tile) && (Boulders == null || !Boulders.Contains(tile));

    public bool IsBoulder(GridPosition tile) => Boulders != null && Boulders.Contains(tile);

    /// <summary>Nobody builds near where the raiders come in, so the entrance can't be walled shut.</summary>
    public bool IsInSpawnClearance(GridPosition tile) =>
        SpawnClearance > 0 && Enumerable.Range(0, RoadCount).Select(SpawnOf)
            .Any(s => System.Math.Max(System.Math.Abs(tile.X - s.X), System.Math.Abs(tile.Y - s.Y)) <= SpawnClearance);

    /// <summary>
    /// Wild trees on the land (about one tile in twelve), deterministic, never on the road, the entrance, a rock or
    /// beside the Hearth. Players cut them for Timber.
    /// </summary>
    public IReadOnlyList<GridPosition> WildTrees => _wildTrees ??= ComputeWildTrees();

    /// <summary>
    /// The raiders' road: the old track on the bare valley (every rock standing, all land open). It never moves:
    /// nobody may build on it, and clearing a boulder doesn't open a shortcut.
    /// </summary>
    public IReadOnlyList<GridPosition> Road => _road ??= new BattleLayout(Width, Height, Spawn, Hearth, System.Array.Empty<PlacedTower>(), Rocks).ResolveEnemyPath();

    private IReadOnlyList<GridPosition> ComputeWildTrees()
    {
        var road = AllRoadTiles;
        var water = Water;
        var trees = new List<GridPosition>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var tile = new GridPosition(x, y);
                var hash = (x * 73856093) ^ (y * 19349663) ^ 0x2545F491;
                var nearHearth = DistanceFromHearth(tile) <= 1;
                if ((hash & 0x7fffffff) % 12 == 5 && !road.Contains(tile) && !water.Contains(tile) && OpensAt(tile) < Wild && !Rocks.Contains(tile) && !IsInSpawnClearance(tile) && !nearHearth && !IsSpawn(tile))
                {
                    trees.Add(tile);
                }
            }
        }

        return trees;
    }

    public bool Contains(GridPosition tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

    public bool IsOpen(GridPosition tile, int hearthLevel) => Contains(tile) && OpensAt(tile) <= hearthLevel;

    /// <summary>The Hearth level at which a tile opens (1 for land open from the start).</summary>
    public int OpensAt(GridPosition tile) => Zones?.Where(z => z.Contains(tile)).Select(z => z.OpensAtHearth).DefaultIfEmpty(1).Max() ?? 1;

    public IEnumerable<GridPosition> LockedTiles(int hearthLevel)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var tile = new GridPosition(x, y);
                if (OpensAt(tile) > hearthLevel)
                {
                    yield return tile;
                }
            }
        }
    }

    public BattleLayout ToBattleLayout(IEnumerable<PlacedTower> towers, IEnumerable<GridPosition> obstacles, IEnumerable<PlacedStructure>? structures = null,
        int hearthLevel = int.MaxValue, IReadOnlyCollection<GridPosition>? cleared = null, int road = 0)
    {
        var rocks = cleared == null ? Rocks : Rocks.Where(r => !cleared.Contains(r)).ToList();
        var path = RoadAt(road);
        // Raiders walk their road even through land the village hasn't opened: the forest is theirs.
        var locked = LockedTiles(hearthLevel).Except(Rocks).Where(t => !path.Contains(t));
        return new(Width, Height, SpawnOf(road), Hearth, towers, rocks.Concat(locked).Concat(obstacles), null, structures, path);
    }

    /// <summary>
    /// Why a building's shape can't stand at <paramref name="anchor"/> in this snapshot (null: it can) — the same rule
    /// the village applies, for a cursor hint before the command is sent. Costs, limits and the builder aren't checked.
    /// </summary>
    /// <param name="moving">The building being moved, which doesn't stand in its own way.</param>
    public const string GateMisplaced = "A gate goes across the raiders' road, or into a wall, fence or road. Not in a field.";

    /// <summary>
    /// A gate stands on the raiders' road, or next to (side by side) a wall, a fence or a village road tile: where
    /// something passes through. Alone in a field it would be a door with no house (owner 2026-10-14).
    /// </summary>
    public static bool GateFits(IReadOnlyList<GridPosition> tiles, ICollection<GridPosition> raidersRoad, IEnumerable<GridPosition> wallsFencesAndRoads)
    {
        if (tiles.Any(raidersRoad.Contains))
        {
            return true;
        }

        var neighbours = new HashSet<GridPosition>(wallsFencesAndRoads);
        return tiles.Any(t => neighbours.Contains(new GridPosition(t.X + 1, t.Y)) || neighbours.Contains(new GridPosition(t.X - 1, t.Y))
                              || neighbours.Contains(new GridPosition(t.X, t.Y + 1)) || neighbours.Contains(new GridPosition(t.X, t.Y - 1)));
    }

    /// <summary>
    /// Whether a building on <paramref name="tiles"/> would be on the road network (the village's rule, from a
    /// snapshot): a tile beside it carries a road from the Hearth, or it stands by the Hearth. The ghost warns before ✓.
    /// </summary>
    public bool WouldConnect(BaseSnapshot snapshot, IReadOnlyList<GridPosition> tiles)
    {
        var carriers = new HashSet<GridPosition>(RoadTiles(snapshot.HearthLevel));
        foreach (var b in snapshot.Buildings.Where(b => BuildingTable.IsRoad(b.Type) || b.Type == BuildingType.Gate))
        {
            carriers.UnionWith(b.Tiles);
        }

        var network = new HashSet<GridPosition>(HearthTiles);
        var frontier = new Queue<GridPosition>(HearthTiles);
        while (frontier.Count > 0)
        {
            var t = frontier.Dequeue();
            foreach (var next in new[] { new GridPosition(t.X + 1, t.Y), new GridPosition(t.X - 1, t.Y), new GridPosition(t.X, t.Y + 1), new GridPosition(t.X, t.Y - 1) })
            {
                if (carriers.Contains(next) && network.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        var own = new HashSet<GridPosition>(tiles);
        return tiles.Any(t => new[] { new GridPosition(t.X + 1, t.Y), new GridPosition(t.X - 1, t.Y), new GridPosition(t.X, t.Y + 1), new GridPosition(t.X, t.Y - 1) }
            .Any(n => network.Contains(n) && !own.Contains(n)));
    }

    public string? PlacementProblem(BaseSnapshot snapshot, BuildingSpec spec, GridPosition anchor, bool rotated, int? moving = null, BuildingTable? table = null)
    {
        var tiles = Footprint.Tiles(spec, anchor, rotated);
        var cleared = snapshot.ClearedTerrain ?? System.Array.Empty<GridPosition>();
        var road = RoadTiles(snapshot.HearthLevel);
        var locked = tiles.Where(t => Contains(t) && !IsOpen(t, snapshot.HearthLevel)).Select(t => (GridPosition?)t).FirstOrDefault();
        if (locked != null)
        {
            return $"That land opens at Hearth {OpensAt(locked.Value)}.";
        }

        if (tiles.Any(t => !Contains(t) || IsSpawn(t) || IsHearthTile(t)))
        {
            return "Can't build there.";
        }

        if (tiles.Any(road.Contains) && spec.Type != BuildingType.Gate)
        {
            return "That's the raiders' road. Build beside it.";
        }

        if (spec.Type == BuildingType.Gate && !GateFits(tiles, road,
                snapshot.Buildings.Where(b => b.Id != moving && (BuildingTable.IsRoad(b.Type) || b.Type is BuildingType.WoodenWall or BuildingType.WattleFence)).SelectMany(b => b.Tiles)))
        {
            return GateMisplaced;
        }

        var roadGates = (table ?? BuildingTable.Default).RoadGates(snapshot.HearthLevel);
        if (spec.Type == BuildingType.Gate && tiles.Any(road.Contains)
            && snapshot.Buildings.Count(b => b.Id != moving && b.Type == BuildingType.Gate && b.Tiles.Any(road.Contains)) >= roadGates)
        {
            return "No room for another gate on the road until the Hearth grows.";
        }

        foreach (var tile in tiles)
        {
            var standing = snapshot.BuildingAt(tile);
            if ((standing != null && standing.Id != moving) || IsBedrock(tile))
            {
                return "Can't build there.";
            }

            if (IsInSpawnClearance(tile))
            {
                return "Too close to where the raiders come in.";
            }

            if (IsBoulder(tile) && !cleared.Contains(tile))
            {
                return "Clear the boulder first.";
            }

            if (WildTrees.Contains(tile) && !cleared.Contains(tile))
            {
                return "Cut the tree down first.";
            }
        }

        if (NeedsRoad(spec) && !RoadCouldReach(snapshot.HearthLevel, snapshot.Buildings.First(b => b.Type == BuildingType.Hearth).Tiles,
                snapshot.Buildings.Where(b => BuildingTable.IsRoad(b.Type) || b.Type == BuildingType.Gate).SelectMany(b => b.Tiles),
                t => snapshot.BuildingAt(t) is { } b && b.Id != moving && !BuildingTable.IsRoad(b.Type), tiles))
        {
            return NoRoadReaches;
        }

        return null;
    }

    internal const string NoRoadReaches = "No road can reach this spot, so nobody could work here.";

    /// <summary>Buildings that only work when connected: producers, homes, storage, the Cistern.</summary>
    internal static bool NeedsRoad(BuildingSpec spec) => BuildingTable.NeedsRoad(spec.Type) &&
        (spec.Produces != null || spec.Workers > 0 || spec.Levels.Any(l => l.Beds > 0 || l.StorageBonus > 0 || l.SluiceCharges > 0));

    /// <summary>
    /// Could a road ever join these tiles to the Hearth's network? Searches the tiles a track could be painted on now or
    /// after clearing a tree or boulder: open land, not the raiders' road, not under another building.
    /// </summary>
    /// <param name="carrierTiles">Painted roads and gates; with the raiders' road they carry the network (as the village's own rule).</param>
    /// <param name="occupied">A building that isn't a road stands on the tile (the one being moved excluded).</param>
    internal bool RoadCouldReach(int hearthLevel, IEnumerable<GridPosition> hearthTiles, IEnumerable<GridPosition> carrierTiles, Func<GridPosition, bool> occupied,
        IReadOnlyList<GridPosition> tiles)
    {
        var footprint = new HashSet<GridPosition>(tiles);
        var raidersRoad = RoadTiles(hearthLevel);
        var roads = new HashSet<GridPosition>(carrierTiles);
        roads.UnionWith(raidersRoad);
        var network = new HashSet<GridPosition>(hearthTiles);
        var frontier = new Queue<GridPosition>(network);
        while (frontier.Count > 0)
        {
            foreach (var n in Neighbours(frontier.Dequeue()).Where(n => roads.Contains(n) && network.Add(n)))
            {
                frontier.Enqueue(n);
            }
        }

        bool Paintable(GridPosition t) =>
            Contains(t) && IsOpen(t, hearthLevel) && !footprint.Contains(t) && !raidersRoad.Contains(t) && !IsBedrock(t)
            && !IsSpawn(t) && !IsInSpawnClearance(t) && !occupied(t);

        var seen = new HashSet<GridPosition>();
        var queue = new Queue<GridPosition>();
        foreach (var start in tiles.SelectMany(Neighbours).Where(n => !footprint.Contains(n)))
        {
            if (network.Contains(start))
            {
                return true;
            }

            if (Paintable(start) && seen.Add(start))
            {
                queue.Enqueue(start);
            }
        }

        while (queue.Count > 0)
        {
            foreach (var n in Neighbours(queue.Dequeue()))
            {
                if (network.Contains(n))
                {
                    return true;
                }

                if (Paintable(n) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return false;
    }

    private static IEnumerable<GridPosition> Neighbours(GridPosition p)
    {
        yield return new GridPosition(p.X + 1, p.Y);
        yield return new GridPosition(p.X - 1, p.Y);
        yield return new GridPosition(p.X, p.Y + 1);
        yield return new GridPosition(p.X, p.Y - 1);
    }

    /// <summary>Wild trees still standing: not cut, not on locked land (already forest), not under a building.</summary>
    public IEnumerable<GridPosition> StandingWildTrees(IReadOnlyCollection<GridPosition> cleared, ISet<GridPosition> buildingTiles, int hearthLevel) =>
        WildTrees.Where(t => !cleared.Contains(t) && !buildingTiles.Contains(t) && IsOpen(t, hearthLevel));

    /// <summary>
    /// The locked Night layout for a village snapshot: towers fight, every other building is in the way, locked land is
    /// forest. Pure, so a remote gateway derives it from the server's snapshot exactly as the device does.
    /// </summary>
    public BattleLayout ToBattleLayout(BaseSnapshot snapshot, BuildingTable table, int road = 0)
    {
        var cleared = snapshot.ClearedTerrain ?? System.Array.Empty<GridPosition>();
        return ToBattleLayout(
            snapshot.Buildings.Where(b => table.Spec(b.Type).IsTower).Select(b => new PlacedTower(table.Spec(b.Type).Tower!.Value, b.Position, b.Level)),
            snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth && !table.Spec(b.Type).IsTower && !BuildingTable.IsRoad(b.Type)).SelectMany(b => b.Tiles),
            snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth && table.StructureHealth(b.Type, b.Level) > 0).Select(b =>
            {
                var health = table.StructureHealth(b.Type, b.Level);
                return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health)
                {
                    Tiles = b.Tiles, Barrier = BuildingTable.IsBarrier(b.Type), Gate = b.Type == BuildingType.Gate,
                };
            }),
            snapshot.HearthLevel,
            cleared,
            road);
    }

    private static IReadOnlyList<GridPosition> DemoRocks()
    {
        var rocks = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            rocks.Add(new GridPosition(5, y));
        }

        // The eastern wall runs to the southern edge, so the land that opens at Hearth 2 is no shortcut for raiders.
        for (var y = 3; y <= 35; y++) // down to the river (spec 2026-10-20 river)
        {
            rocks.Add(new GridPosition(11, y));
        }

        rocks.AddRange(new[]
        {
            new GridPosition(1, 6), new GridPosition(2, 9), new GridPosition(8, 4),
            new GridPosition(14, 1), new GridPosition(16, 4), new GridPosition(8, 0),

            // A few boulders out in the new land, so it isn't a parade ground.
            new GridPosition(20, 5), new GridPosition(14, 16), new GridPosition(26, 12), new GridPosition(7, 18),
            new GridPosition(34, 8), new GridPosition(20, 23), new GridPosition(42, 18), new GridPosition(31, 28),
            new GridPosition(8, 27), new GridPosition(45, 4),

            // The river country (Hearth 4): crags on the near bank.
            new GridPosition(40, 33), new GridPosition(41, 33), new GridPosition(41, 34), new GridPosition(17, 34), new GridPosition(50, 8), new GridPosition(50, 9),
        });
        return rocks;
    }
}
