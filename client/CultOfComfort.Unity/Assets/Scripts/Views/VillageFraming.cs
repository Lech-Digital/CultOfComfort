using System.Linq;
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

        /// <summary>The smallest home view (tiles): a new village isn't framed so tight that there's nowhere to build.</summary>
        public const int MinWidth = 12;
        public const int MinHeight = 10;

        /// <summary>
        /// Frame the village, not the field (spec 2026-11-17 fun first, F3): the Hearth and what's built, two tiles round
        /// them, at least <see cref="MinWidth"/> × <see cref="MinHeight"/>, never past <see cref="HomeBounds"/>. It grows
        /// with the village; beyond it, the forest and the fog.
        /// </summary>
        /// <param name="built">The tiles of the village's buildings (not roads or nature).</param>
        public static (Vector2 Min, Vector2 Max) BuiltBounds(VillageMap map, int hearthLevel, System.Collections.Generic.IEnumerable<GridPosition> built)
        {
            var (homeMin, homeMax) = HomeBounds(map, hearthLevel);
            var tiles = map.HearthTiles.Concat(built ?? Enumerable.Empty<GridPosition>()).ToList();
            float x0 = tiles.Min(t => t.X) - 2, x1 = tiles.Max(t => t.X) + 2;
            float y0 = tiles.Min(t => t.Y) - 2, y1 = tiles.Max(t => t.Y) + 2;
            Grow(ref x0, ref x1, MinWidth - 1);
            Grow(ref y0, ref y1, MinHeight - 1);

            // Grid y runs down the screen; world z = -y (Shapes.ToWorld).
            var min = new Vector2(Mathf.Max(x0, homeMin.x), Mathf.Max(-y1, homeMin.y));
            var max = new Vector2(Mathf.Min(x1, homeMax.x), Mathf.Min(-y0, homeMax.y));
            return (min, max);
        }

        private static void Grow(ref float lo, ref float hi, float size)
        {
            var missing = size - (hi - lo);
            if (missing > 0f)
            {
                lo -= missing / 2f;
                hi += missing / 2f;
            }
        }

        /// <summary>The stretch of road in front of the Hearth the Night frames: where the towers stand and the Prophet fights.</summary>
        public const int NightRoadTiles = 10;

        /// <summary>
        /// The Night's stage: the last <see cref="NightRoadTiles"/> tiles of tonight's road and the Hearth, with a tile and a
        /// half round them. A fixed stretch, not a share of the road: on a long road from across the river a share framed the
        /// whole valley and the village became a speck in a corner (playtest 2026-11-17). Raiders walk into view.
        /// </summary>
        public static (Vector2 Min, Vector2 Max) NightBounds(VillageMap map, int road = 0)
        {
            const float margin = 1.5f;
            var path = map.RoadAt(road);
            var tiles = path.Skip(Mathf.Max(0, path.Count - NightRoadTiles)).Concat(map.HearthTiles).ToList();
            return (new Vector2(tiles.Min(t => t.X) - margin, -tiles.Max(t => t.Y) - margin), new Vector2(tiles.Max(t => t.X) + margin, -tiles.Min(t => t.Y) + margin));
        }

        /// <summary>Where tonight's raiders come from (x, z): the start of the road. The Night's spare room goes that way.</summary>
        public static Vector2 NightLean(VillageMap map, int road = 0)
        {
            var path = map.RoadAt(road);
            return new Vector2(path[0].X, -path[0].Y);
        }

        /// <summary>The whole valley: the map, its forest edge and the river past the south and east.</summary>
        public static (Vector2 Min, Vector2 Max) RoamBounds(VillageMap map) =>
            (new Vector2(-6f, -(map.Height - 1) - 7f), new Vector2(map.Width - 1 + 7f, 6f));
    }
}
