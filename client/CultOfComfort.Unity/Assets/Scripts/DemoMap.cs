using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>The muddy valley: now defined once in the shared library (<see cref="VillageMap.Demo"/>).</summary>
    public static class DemoMap
    {
        public static readonly int Width = VillageMap.Demo.Width;
        public static readonly int Height = VillageMap.Demo.Height;
        public static readonly GridPosition Spawn = VillageMap.Demo.Spawn;
        public static readonly GridPosition Hearth = VillageMap.Demo.Hearth;

        public static IReadOnlyList<GridPosition> CreateRocks() => new List<GridPosition>(VillageMap.Demo.Rocks);
    }
}
