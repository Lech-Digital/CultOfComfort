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
public sealed record VillageMap(int Width, int Height, GridPosition Spawn, GridPosition Hearth, IReadOnlyList<GridPosition> Rocks, IReadOnlyList<MapZone>? Zones = null)
{
    /// <summary>
    /// The muddy valley. Building the village is the heart of the game, so there is room: 18×14 open at Hearth 1,
    /// 24×18 at Hearth 2, the whole 30×20 at Hearth 3. The road between spawn and Hearth never changes.
    /// </summary>
    public static VillageMap Demo { get; } = new(30, 20, new GridPosition(0, 1), new GridPosition(17, 9), DemoRocks(), new[]
    {
        new MapZone(0, 14, 23, 17, 2),
        new MapZone(18, 0, 23, 13, 2),
        new MapZone(0, 18, 23, 19, 3),
        new MapZone(24, 0, 29, 19, 3),
    });

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

    public BattleLayout ToBattleLayout(IEnumerable<PlacedTower> towers, IEnumerable<GridPosition> obstacles, IEnumerable<PlacedStructure>? structures = null, int hearthLevel = int.MaxValue) =>
        new(Width, Height, Spawn, Hearth, towers, Rocks.Concat(LockedTiles(hearthLevel).Except(Rocks)).Concat(obstacles), null, structures);

    /// <summary>
    /// The locked Night layout for a village snapshot: towers fight, every other building is in the way, locked land is
    /// forest. Pure, so a remote gateway derives it from the server's snapshot exactly as the device does.
    /// </summary>
    public BattleLayout ToBattleLayout(BaseSnapshot snapshot, BuildingTable table) => ToBattleLayout(
        snapshot.Buildings.Where(b => table.Spec(b.Type).IsTower).Select(b => new PlacedTower(table.Spec(b.Type).Tower!.Value, b.Position)),
        snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth && !table.Spec(b.Type).IsTower).Select(b => b.Position),
        snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth).Select(b =>
        {
            var health = table.StructureHealth(b.Type, b.Level);
            return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health);
        }),
        snapshot.HearthLevel);

    private static IReadOnlyList<GridPosition> DemoRocks()
    {
        var rocks = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            rocks.Add(new GridPosition(5, y));
        }

        // The eastern wall runs to the southern edge, so the land that opens at Hearth 2 is no shortcut for raiders.
        for (var y = 3; y <= 19; y++)
        {
            rocks.Add(new GridPosition(11, y));
        }

        rocks.AddRange(new[]
        {
            new GridPosition(1, 6), new GridPosition(2, 9), new GridPosition(8, 4),
            new GridPosition(14, 1), new GridPosition(16, 4), new GridPosition(8, 0),

            // A few boulders out in the new land, so it isn't a parade ground.
            new GridPosition(20, 5), new GridPosition(14, 16), new GridPosition(26, 12), new GridPosition(7, 18),
        });
        return rocks;
    }
}
