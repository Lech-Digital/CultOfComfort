using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>What the camera frames (world x, z): a home view on the village core, and the whole valley to roam.</summary>
    public static class VillageFraming
    {
        /// <summary>Largest home view (tiles): framing a 30×20 open area makes the village too small to tap on a phone.</summary>
        public const int HomeWidth = 22;
        public const int HomeHeight = 16;

        /// <summary>The open land from the corner with the raiders' entrance, at most <see cref="HomeWidth"/> × <see cref="HomeHeight"/>.</summary>
        public static (Vector2 Min, Vector2 Max) HomeBounds(VillageMap map, int hearthLevel)
        {
            int maxX = 0, maxY = 0;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (map.IsOpen(new GridPosition(x, y), hearthLevel))
                    {
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            var hearthEnd = map.HearthTiles[map.HearthTiles.Count - 1];
            maxX = Mathf.Min(maxX, Mathf.Max(HomeWidth - 1, hearthEnd.X + 1));
            maxY = Mathf.Min(maxY, Mathf.Max(HomeHeight - 1, hearthEnd.Y + 1));
            return (new Vector2(0f, -maxY), new Vector2(maxX, 0f));
        }

        /// <summary>The whole valley: the map, its forest edge and the river past the south and east.</summary>
        public static (Vector2 Min, Vector2 Max) RoamBounds(VillageMap map) =>
            (new Vector2(-6f, -(map.Height - 1) - 7f), new Vector2(map.Width - 1 + 7f, 6f));
    }
}
