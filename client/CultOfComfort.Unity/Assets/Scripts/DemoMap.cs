using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The original 18×11 valley, frozen: the determinism probe's layout (its hash is pinned). The village plays on
    /// <see cref="CultOfComfort.Simulation.Base.VillageMap.Demo"/>, which grows with the Hearth.
    /// </summary>
    public static class DemoMap
    {
        public const int Width = 18;
        public const int Height = 11;

        public static readonly GridPosition Spawn = new GridPosition(0, 1);
        public static readonly GridPosition Hearth = new GridPosition(17, 9);

        public static IReadOnlyList<GridPosition> CreateRocks()
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
}
