using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Replays the Sandbox's demo Night (same layout, seed and input log) and compares the state hash with the
    /// value produced by .NET on the desktop. A match proves the simulation is bit-identical on this
    /// platform/runtime (e.g. IL2CPP on ARM64) — the core assumption of ADR 0002.
    /// </summary>
    public static class DeterminismProbe
    {
        /// <summary>From `dotnet run --project tools/CultOfComfort.Sandbox` (seed 947, budget 54). Update when the sim changes on purpose.</summary>
        public const string ExpectedHash = "3235167ab097d532";

        public static string LastHash { get; private set; }
        public static bool? Matches { get; private set; }

        public static void Run()
        {
            PlacedTower[] towers =
            {
                new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 6)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(6, 8)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(9, 7)),
                new PlacedTower(TowerType.SlurryChute, new GridPosition(10, 2)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(12, 1)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(15, 6)),
            };
            var layout = new BattleLayout(DemoMap.Width, DemoMap.Height, DemoMap.Spawn, DemoMap.Hearth, towers, DemoMap.CreateRocks());
            var inputs = new List<PlayerInput>
            {
                new PlayerInput(2281, PlayerActionType.OpenSluiceGate),
                new PlayerInput(2481, PlayerActionType.OpenSluiceGate),
            };

            var result = Battle.Simulate(new BattleSetup(layout, 947, 54, 4, inputs));
            LastHash = result.StateHashHex;
            Matches = LastHash == ExpectedHash;
            Debug.Log($"[CoC] Determinism probe on {Application.platform} ({SystemInfo.processorType}): " +
                      $"{LastHash} expected {ExpectedHash} → {(Matches == true ? "MATCH" : "MISMATCH")}");
        }
    }
}
