using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The land around the village (scenery only; raiders and buildings ignore it): the river running on past the
    /// map, a belt of deep forest hugging the land with a gap where each road comes in, ranges of mountains to the north
    /// and west (and a few peaks beyond the river), and a thin far forest, so zooming out shows a world instead of
    /// blackness. Pure and deterministic.
    /// </summary>
    public static class WorldScenery
    {
        /// <summary>Water and banks outside the map, from the map's river: nothing else grows there.</summary>
        private static Func<GridPosition, bool> Wet(VillageMap map) =>
            t => map.River != null && map.River(t.X, t.Y) < 1.0;

        /// <summary>Whether a tile outside the map is on the way in for a road (nothing grows on it).</summary>
        private static bool OnTheWayIn(VillageMap map, GridPosition tile, int spread)
        {
            for (var road = 0; road < map.RoadCount; road++)
            {
                var spawn = map.SpawnOf(road);
                var westward = spawn.X == 0 && tile.X < 0 && Math.Abs(tile.Y - spawn.Y) <= spread;
                var southward = spawn.Y == map.Height - 1 && tile.Y >= map.Height && Math.Abs(tile.X - spawn.X) <= spread;
                var eastward = spawn.X == map.Width - 1 && tile.X >= map.Width && Math.Abs(tile.Y - spawn.Y) <= spread;
                if (westward || southward || eastward)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The edge of the land, so nobody wonders how far the village goes: a belt of deep forest hugging the land, and
        /// mountain ranges to the north and west. A gap stays open wherever a road comes in.
        /// </summary>
        public static List<(string Model, GridPosition Tile, Vector3 Jitter, float Yaw, float Scale)> BorderLayout(VillageMap map)
        {
            var (width, height) = (map.Width, map.Height);
            var wet = Wet(map);
            var layout = new List<(string, GridPosition, Vector3, float, float)>();
            for (var y = -2; y < height + 2; y++)
            {
                for (var x = -2; x < width + 2; x++)
                {
                    var tile = new GridPosition(x, y);
                    var depth = Math.Max(Math.Max(-x, x - width + 1), Math.Max(-y, y - height + 1)); // tiles out from the land
                    // Alternate tiles per ring: every edge tile has a clump beside it, without hundreds of models.
                    if (depth < 1 || OnTheWayIn(map, tile, 1) || wet(tile) || (x + y + depth) % 2 != 0)
                    {
                        continue;
                    }

                    var hash = (x * 83492791) ^ (y * 19349663) ^ 0x1b873593;
                    var jitter = new Vector3((hash >> 4 & 15) / 40f - 0.19f, 0f, (hash >> 8 & 15) / 40f - 0.19f);
                    layout.Add(("deep_forest", tile, jitter, hash >> 16 & 359, 1.15f + (hash >> 20 & 3) * 0.1f));
                }
            }

            // Mountains (owner 2026-10-20: "more cool mountains"): ridges and tall peaks in two staggered rows to the
            // north and west, the far row bigger; a few lone peaks beyond the river.
            string[] kinds = { "peak_ridge", "peak_tall", "mountain", "peak_ridge", "peak_tall" };
            for (var y = -26; y < height + 2; y += 6)
            {
                for (var x = -26; x < width + 10; x += 6)
                {
                    var stagger = (y / 6 & 1) * 3;
                    var tile = new GridPosition(x + stagger, y);
                    var far = tile.Y <= -14 || tile.X <= -14;
                    var mountainous = tile.Y <= -8 || tile.X <= -8; // well back from the land, so no peak hides the village's edge
                    var hash = (tile.X * 73856093) ^ (tile.Y * 19349663) ^ 0x2545F491;
                    var nearWater = map.River != null && map.River(tile.X, tile.Y) < 7.0; // a mountain is wide: keep it well off the river
                    if (!mountainous || OnTheWayIn(map, tile, 4) || nearWater)
                    {
                        continue;
                    }

                    var jitter = new Vector3((hash >> 4 & 15) / 12f - 0.6f, 0f, (hash >> 8 & 15) / 12f - 0.6f);
                    var kind = kinds[(hash >> 14 & 0xffff) % kinds.Length];
                    var scale = (far ? 2.0f : 1.2f) + (hash >> 20 & 7) * 0.12f;
                    layout.Add((kind, tile, jitter, hash >> 12 & 359, scale));
                }
            }

            foreach (var (x, y, scale) in new[] { (width + 9, 4, 2.4f), (width + 13, 16, 2.0f), (width + 8, height + 9, 2.2f), (22, height + 12, 2.6f), (44, height + 10, 1.9f) })
            {
                var tile = new GridPosition(x, y);
                if (map.River == null || map.River(tile.X, tile.Y) > 7.0)
                {
                    layout.Add(((x + y) % 2 == 0 ? "peak_tall" : "peak_ridge", tile, Vector3.zero, (x * 37 + y * 11) % 360, scale));
                }
            }

            return layout;
        }

        /// <summary>The raiders' tracks: out of each road's entrance and away to the horizon.</summary>
        public static IReadOnlyList<GridPosition> PassTracks(VillageMap map)
        {
            var track = new List<GridPosition>();
            for (var road = 0; road < map.RoadCount; road++)
            {
                var spawn = map.SpawnOf(road);
                var (dx, dy) = spawn.X == 0 ? (-1, 0) : spawn.X == map.Width - 1 ? (1, 0) : spawn.Y == map.Height - 1 ? (0, 1) : (0, -1);
                for (var k = 1; k <= 16; k++)
                {
                    track.Add(new GridPosition(spawn.X + dx * k, spawn.Y + dy * k));
                }
            }

            return track;
        }

        /// <summary>Far forest: a sparse band 3–16 tiles out, never in the river or on its banks, or in the mountains.</summary>
        public static List<(string Model, GridPosition Tile, Vector3 Jitter, float Yaw, float Scale)> WorldLayout(VillageMap map)
        {
            var (width, height) = (map.Width, map.Height);
            var wet = Wet(map);
            var tracks = new HashSet<GridPosition>(PassTracks(map));
            string[] nature = { "pine", "pine", "oak", "birch", "pine", "bush", "oak", "rock_a", "pine", "dead_tree" };
            var layout = new List<(string, GridPosition, Vector3, float, float)>();
            for (var y = -16; y < height + 16; y++)
            {
                for (var x = -16; x < width + 16; x++)
                {
                    var tile = new GridPosition(x, y);
                    var nearVillage = x >= -3 && y >= -3 && x < width + 3 && y < height + 3;
                    var mountainCountry = x < -4 || y < -4;
                    var hash = (x * 83492791) ^ (y * 19349663) ^ 0x5bd1e995;
                    if (nearVillage || mountainCountry || wet(tile) || tracks.Contains(tile) || (hash & 15) != 3)
                    {
                        continue;
                    }

                    var jitter = new Vector3((hash >> 4 & 15) / 30f - 0.25f, 0f, (hash >> 8 & 15) / 30f - 0.25f);
                    layout.Add((nature[(hash >> 12 & 0x7fff) % nature.Length], tile, jitter, hash >> 16 & 359, 1.1f + (hash >> 20 & 3) * 0.15f));
                }
            }

            return layout;
        }
    }
}
