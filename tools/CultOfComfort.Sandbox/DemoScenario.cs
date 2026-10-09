using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Sandbox;

/// <summary>A muddy 18x11 valley with two rock ridges forcing a serpentine route to the Hearth.</summary>
internal static class DemoScenario
{
    public static BattleLayout CreateLayout()
    {
        var obstacles = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            obstacles.Add(new GridPosition(5, y));
        }

        for (var y = 3; y <= 10; y++)
        {
            obstacles.Add(new GridPosition(11, y));
        }

        obstacles.AddRange([new(1, 6), new(2, 9), new(8, 4), new(14, 1), new(16, 4), new(8, 0)]);

        PlacedTower[] towers =
        [
            new(TowerType.SlurryChute, new GridPosition(4, 6)),
            new(TowerType.SaplingFlinger, new GridPosition(6, 8)),
            new(TowerType.SaplingFlinger, new GridPosition(9, 7)),
            new(TowerType.SlurryChute, new GridPosition(10, 2)),
            new(TowerType.SaplingFlinger, new GridPosition(12, 1)),
            new(TowerType.SaplingFlinger, new GridPosition(15, 6)),
        ];

        return new BattleLayout(18, 11, new GridPosition(0, 1), new GridPosition(17, 9), towers, obstacles);
    }

    /// <summary>
    /// Stand-in for a human: opens the sluice when anyone gets within 6 tiles of the Hearth.
    /// Its actions are recorded in the battle's input log exactly as a real player's would be.
    /// </summary>
    public static void AutoPlay(Battle battle)
    {
        var dangerZone = battle.PathLength - Fix.FromInt(6);
        if (battle.IsSluiceReady && battle.Enemies.Any(e => e.IsActive && e.DistanceAlongPath > dangerZone))
        {
            battle.Intervene(PlayerActionType.OpenSluiceGate);
        }
    }
}
