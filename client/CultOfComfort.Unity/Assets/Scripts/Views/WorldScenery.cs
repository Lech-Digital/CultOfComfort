using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The land around the village (scenery only; raiders and buildings ignore it): a river that winds past the south
    /// and east, and a thin far forest, so zooming out shows a world instead of blackness. Pure and deterministic.
    /// </summary>
    public static class WorldScenery
    {
        /// <summary>River tiles in flowing order: in from the west below the village, round the south-east, off to the north.</summary>
        public static IReadOnlyList<GridPosition> RiverPath(int width, int height)
        {
            var path = new List<GridPosition>();
            void Add(GridPosition tile)
            {
                if (path.Count > 0)
                {
                    // Fill diagonal-free gaps so the stream never breaks.
                    var last = path[path.Count - 1];
                    while (Math.Max(Math.Abs(tile.X - last.X), Math.Abs(tile.Y - last.Y)) > 1)
                    {
                        last = new GridPosition(last.X + Math.Sign(tile.X - last.X), last.Y + Math.Sign(tile.Y - last.Y));
                        path.Add(last);
                    }
                }

                if (path.Count == 0 || path[path.Count - 1] != tile)
                {
                    path.Add(tile);
                }
            }

            var south = height + 4;
            var east = width + 5;
            for (var x = -20; x <= east; x++)
            {
                Add(new GridPosition(x, south + (int)Math.Round(Math.Sin(x * 0.33) * 1.4)));
            }

            for (var y = south; y >= -18; y--)
            {
                Add(new GridPosition(east + (int)Math.Round(Math.Sin(y * 0.4) * 1.4), y));
            }

            return path;
        }

        /// <summary>Far forest: a sparse band 3–16 tiles out, never in the river or on its banks.</summary>
        public static List<(string Model, GridPosition Tile, Vector3 Jitter, float Yaw, float Scale)> WorldLayout(int width, int height)
        {
            var water = new HashSet<GridPosition>();
            foreach (var tile in RiverPath(width, height))
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        water.Add(new GridPosition(tile.X + dx, tile.Y + dy));
                    }
                }
            }

            string[] nature = { "pine", "pine", "oak", "birch", "pine", "bush", "oak", "rock_a", "pine", "dead_tree" };
            var layout = new List<(string, GridPosition, Vector3, float, float)>();
            for (var y = -16; y < height + 16; y++)
            {
                for (var x = -16; x < width + 16; x++)
                {
                    var nearVillage = x >= -3 && y >= -3 && x < width + 3 && y < height + 3;
                    var hash = (x * 83492791) ^ (y * 19349663) ^ 0x5bd1e995;
                    if (nearVillage || water.Contains(new GridPosition(x, y)) || (hash & 15) != 3)
                    {
                        continue;
                    }

                    var jitter = new Vector3((hash >> 4 & 15) / 30f - 0.25f, 0f, (hash >> 8 & 15) / 30f - 0.25f);
                    layout.Add((nature[(hash >> 12 & 0x7fff) % nature.Length], new GridPosition(x, y), jitter, hash >> 16 & 359, 1.1f + (hash >> 20 & 3) * 0.15f));
                }
            }

            return layout;
        }
    }
}
