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
    public static VillageMap Demo { get; } = new(48, 32, new GridPosition(0, 1), new GridPosition(17, 9), DemoRocks(), new[]
    {
        new MapZone(0, 20, 37, 25, 2),
        new MapZone(30, 0, 37, 19, 2),
        new MapZone(0, 26, 47, 31, 3),
        new MapZone(38, 0, 47, 25, 3),
    }, DemoBoulders, SpawnClearance: 3) { HearthSize = 4 };

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
        SpawnClearance > 0 && System.Math.Max(System.Math.Abs(tile.X - Spawn.X), System.Math.Abs(tile.Y - Spawn.Y)) <= SpawnClearance;

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
        var road = new HashSet<GridPosition>(Road);
        var trees = new List<GridPosition>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var tile = new GridPosition(x, y);
                var hash = (x * 73856093) ^ (y * 19349663) ^ 0x2545F491;
                var nearHearth = DistanceFromHearth(tile) <= 1;
                if ((hash & 0x7fffffff) % 12 == 5 && !road.Contains(tile) && !Rocks.Contains(tile) && !IsInSpawnClearance(tile) && !nearHearth && tile != Spawn)
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
        int hearthLevel = int.MaxValue, IReadOnlyCollection<GridPosition>? cleared = null)
    {
        var rocks = cleared == null ? Rocks : Rocks.Where(r => !cleared.Contains(r)).ToList();
        return new(Width, Height, Spawn, Hearth, towers, rocks.Concat(LockedTiles(hearthLevel).Except(Rocks)).Concat(obstacles), null, structures, Road);
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
        var carriers = new HashSet<GridPosition>(Road);
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
        var road = new HashSet<GridPosition>(Road);
        var locked = tiles.Where(t => Contains(t) && !IsOpen(t, snapshot.HearthLevel)).Select(t => (GridPosition?)t).FirstOrDefault();
        if (locked != null)
        {
            return $"That land opens at Hearth {OpensAt(locked.Value)}.";
        }

        if (tiles.Any(t => !Contains(t) || t == Spawn || IsHearthTile(t)))
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
    internal static bool NeedsRoad(BuildingSpec spec) =>
        spec.Produces != null || spec.Workers > 0 || spec.Levels.Any(l => l.Beds > 0 || l.StorageBonus > 0 || l.SluiceCharges > 0);

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
        var raidersRoad = new HashSet<GridPosition>(Road);
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
            && t != Spawn && !IsInSpawnClearance(t) && !occupied(t);

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
    public BattleLayout ToBattleLayout(BaseSnapshot snapshot, BuildingTable table)
    {
        var cleared = snapshot.ClearedTerrain ?? System.Array.Empty<GridPosition>();
        return ToBattleLayout(
            snapshot.Buildings.Where(b => table.Spec(b.Type).IsTower).Select(b => new PlacedTower(table.Spec(b.Type).Tower!.Value, b.Position)),
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
            cleared);
    }

    private static IReadOnlyList<GridPosition> DemoRocks()
    {
        var rocks = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            rocks.Add(new GridPosition(5, y));
        }

        // The eastern wall runs to the southern edge, so the land that opens at Hearth 2 is no shortcut for raiders.
        for (var y = 3; y <= 31; y++)
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
        });
        return rocks;
    }
}
