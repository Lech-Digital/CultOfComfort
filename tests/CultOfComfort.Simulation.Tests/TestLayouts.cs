using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests;

internal static class TestLayouts
{
    /// <summary>10x3 corridor: spawn at (0,1), Hearth at (9,1), straight path along y = 1.</summary>
    public static BattleLayout Corridor(params PlacedTower[] towers) =>
        new(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), towers, []);

    /// <summary>Corridor lined with towers on both sides of the path.</summary>
    public static BattleLayout Fortified() =>
        Corridor(
            new PlacedTower(TowerType.SlurryChute, new GridPosition(2, 0)),
            new PlacedTower(TowerType.SaplingFlinger, new GridPosition(3, 2)),
            new PlacedTower(TowerType.SaplingFlinger, new GridPosition(4, 0)),
            new PlacedTower(TowerType.SaplingFlinger, new GridPosition(5, 2)),
            new PlacedTower(TowerType.SaplingFlinger, new GridPosition(6, 0)),
            new PlacedTower(TowerType.SaplingFlinger, new GridPosition(7, 2)));
}
