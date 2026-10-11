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

    public BattleLayout ToBattleLayout(IEnumerable<PlacedTower> towers, IEnumerable<GridPosition> obstacles) =>
        new(Width, Height, Spawn, Hearth, towers, Rocks.Concat(obstacles));

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
