using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>The ground the village and the Night share: grid, spawn, Hearth tile and rocks.</summary>
public sealed record VillageMap(int Width, int Height, GridPosition Spawn, GridPosition Hearth, IReadOnlyList<GridPosition> Rocks)
{
    /// <summary>The muddy valley the prototype has always used.</summary>
    public static VillageMap Demo { get; } = new(18, 11, new GridPosition(0, 1), new GridPosition(17, 9), DemoRocks());

    public bool Contains(GridPosition tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

    public BattleLayout ToBattleLayout(IEnumerable<PlacedTower> towers, IEnumerable<GridPosition> obstacles, IEnumerable<PlacedStructure>? structures = null) =>
        new(Width, Height, Spawn, Hearth, towers, Rocks.Concat(obstacles), null, structures);

    /// <summary>
    /// The locked Night layout for a village snapshot: towers fight, every other building is in the way. Pure, so a
    /// remote gateway derives it from the server's snapshot exactly as the device does.
    /// </summary>
    public BattleLayout ToBattleLayout(BaseSnapshot snapshot, BuildingTable table) => ToBattleLayout(
        snapshot.Buildings.Where(b => table.Spec(b.Type).IsTower).Select(b => new PlacedTower(table.Spec(b.Type).Tower!.Value, b.Position)),
        snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth && !table.Spec(b.Type).IsTower).Select(b => b.Position),
        snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth).Select(b =>
        {
            var health = table.StructureHealth(b.Type, b.Level);
            return new PlacedStructure(b.Id, b.Position, health, b.Wrecked ? 0 : health);
        }));

    private static IReadOnlyList<GridPosition> DemoRocks()
    {
        var rocks = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            rocks.Add(new GridPosition(5, y));
        }

        for (var y = 3; y <= 10; y++)
        {
            rocks.Add(new GridPosition(11, y));
        }

        rocks.AddRange(new[]
        {
            new GridPosition(1, 6), new GridPosition(2, 9), new GridPosition(8, 4),
            new GridPosition(14, 1), new GridPosition(16, 4), new GridPosition(8, 0),
        });
        return rocks;
    }
}
