using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The land around the village (scenery only; raiders and buildings ignore it): a river that winds past the south
    /// and east, mountains and deep forest marking the edge of the land, and a thin far forest, so zooming out shows a world instead of blackness. Pure and deterministic.
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

        /// <summary>
        /// The edge of the land, so nobody wonders how far the village goes: a belt of deep forest hugging the land, and
        /// mountains to the north and west. One gap stays open: the pass west of the spawn, where the raiders come from.
        /// </summary>
        public static List<(string Model, GridPosition Tile, Vector3 Jitter, float Yaw, float Scale)> BorderLayout(int width, int height, GridPosition spawn)
        {
            var water = Banks(width, height);
            var layout = new List<(string, GridPosition, Vector3, float, float)>();
            for (var y = -2; y < height + 2; y++)
            {
                for (var x = -2; x < width + 2; x++)
                {
                    var tile = new GridPosition(x, y);
                    var depth = Math.Max(Math.Max(-x, x - width + 1), Math.Max(-y, y - height + 1)); // tiles out from the land
                    var inPass = x < 0 && Math.Abs(y - spawn.Y) <= 1;
                    // Alternate tiles per ring: every edge tile has a clump beside it, without hundreds of models.
                    if (depth < 1 || inPass || water.Contains(tile) || (x + y + depth) % 2 != 0)
                    {
                        continue;
                    }

                    var hash = (x * 83492791) ^ (y * 19349663) ^ 0x1b873593;
                    var jitter = new Vector3((hash >> 4 & 15) / 40f - 0.19f, 0f, (hash >> 8 & 15) / 40f - 0.19f);
                    layout.Add(("deep_forest", tile, jitter, hash >> 16 & 359, 1.15f + (hash >> 20 & 3) * 0.1f));
                }
            }

            for (var y = -13; y < height + 2; y += 4)
            {
                for (var x = -13; x < width + 6; x += 4)
                {
                    var stagger = (y / 4 & 1) * 2;
                    var tile = new GridPosition(x + stagger, y);
                    var mountainous = tile.Y <= -5 || tile.X <= -5;
                    var nearPass = tile.X < 0 && Math.Abs(tile.Y - spawn.Y) <= 4;
                    if (!mountainous || nearPass || water.Contains(tile))
                    {
                        continue;
                    }

                    var hash = (tile.X * 73856093) ^ (tile.Y * 19349663) ^ 0x2545F491;
                    var jitter = new Vector3((hash >> 4 & 15) / 20f - 0.4f, 0f, (hash >> 8 & 15) / 20f - 0.4f);
                    layout.Add(("mountain", tile, jitter, hash >> 12 & 359, 1.4f + (hash >> 20 & 7) * 0.12f));
                }
            }

            return layout;
        }

        /// <summary>The raiders' track: out of the pass and away west to the horizon.</summary>
        public static IReadOnlyList<GridPosition> PassTrack(GridPosition spawn)
        {
            var track = new List<GridPosition>();
            for (var x = -1; x >= -16; x--)
            {
                track.Add(new GridPosition(x, spawn.Y));
            }

            return track;
        }

        private static HashSet<GridPosition> Banks(int width, int height)
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

            return water;
        }

        /// <summary>Far forest: a sparse band 3–16 tiles out, never in the river or on its banks.</summary>
        public static List<(string Model, GridPosition Tile, Vector3 Jitter, float Yaw, float Scale)> WorldLayout(int width, int height)
        {
            var water = Banks(width, height);
            string[] nature = { "pine", "pine", "oak", "birch", "pine", "bush", "oak", "rock_a", "pine", "dead_tree" };
            var layout = new List<(string, GridPosition, Vector3, float, float)>();
            for (var y = -16; y < height + 16; y++)
            {
                for (var x = -16; x < width + 16; x++)
                {
                    var nearVillage = x >= -3 && y >= -3 && x < width + 3 && y < height + 3;
                    var mountainCountry = x < -4 || y < -4;
                    var hash = (x * 83492791) ^ (y * 19349663) ^ 0x5bd1e995;
                    if (nearVillage || mountainCountry || water.Contains(new GridPosition(x, y)) || (hash & 15) != 3)
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
