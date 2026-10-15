using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>What a tile of road is made of, for painting.</summary>
    public enum RoadSurface
    {
        None,
        RaidersTrack,
        Dirt,
        Planks,
        Cobbles,
    }

    /// <summary>
    /// Paints the village's ground as one picture (owner 2026-10-15: "can we make the [tiles] less visible… roads should
    /// be connected… so they look like a road, not like tiles"): mud in soft patches of damp and dry with tufts of moss,
    /// and every road a continuous band joining its neighbours, worn into the mud (ruts for dirt, boards for planks,
    /// stones for cobbles). Pure: colours in, colours out.
    /// </summary>
    public static class GroundPainter
    {
        public const int PixelsPerTile = 16;

        private static readonly Color Mud = Shapes.Hex("#3d3225");
        private static readonly Color DryMud = Shapes.Hex("#4a3d2b");
        private static readonly Color DampMud = Shapes.Hex("#2f271d");
        private static readonly Color Moss = Shapes.Hex("#3f4a2a");
        private static readonly Color Track = Shapes.Hex("#5a4632");
        private static readonly Color Dirt = Shapes.Hex("#6b5238");
        private static readonly Color Rut = Shapes.Hex("#57422d");
        private static readonly Color Plank = Shapes.Hex("#8a6a43");
        private static readonly Color PlankGap = Shapes.Hex("#4a321e");
        private static readonly Color Stone = Shapes.Hex("#8c877d");
        private static readonly Color StoneGap = Shapes.Hex("#4f4a42");

        /// <summary>The bare ground, <paramref name="width"/> × <paramref name="height"/> tiles, row 0 at tile y 0 (the board's top).</summary>
        public static Color32[] Ground(int width, int height)
        {
            var w = width * PixelsPerTile;
            var h = height * PixelsPerTile;
            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            {
                for (var px = 0; px < w; px++)
                {
                    var x = (px + 0.5f) / PixelsPerTile;
                    var y = (py + 0.5f) / PixelsPerTile;
                    var patch = Fbm(x / 4.5f, y / 4.5f, 1);
                    var fine = Fbm(x * 1.7f, y * 1.7f, 2);
                    var colour = Color.Lerp(Mud, patch > 0 ? DryMud : DampMud, Mathf.Abs(patch) * 1.4f);
                    colour = Color.Lerp(colour, Moss, Mathf.Clamp01((Fbm(x / 3f, y / 3f, 3) - 0.12f) * 2.5f) * 0.85f);
                    colour *= 0.94f + fine * 0.08f;
                    colour.a = 1f;
                    pixels[py * w + px] = colour;
                }
            }

            return pixels;
        }

        /// <summary>
        /// The ground with its roads painted on: a band from each road tile's middle to each neighbouring road (or the
        /// Hearth, or a gate), with frayed edges. Returns a new array; <paramref name="ground"/> is untouched.
        /// </summary>
        public static Color32[] WithRoads(Color32[] ground, int width, int height, IReadOnlyDictionary<GridPosition, RoadSurface> roads,
            ICollection<GridPosition> joins)
        {
            var w = width * PixelsPerTile;
            var pixels = (Color32[])ground.Clone();
            var dirs = new[] { new GridPosition(1, 0), new GridPosition(-1, 0), new GridPosition(0, 1), new GridPosition(0, -1) };
            foreach (var (tile, surface) in roads)
            {
                if (surface == RoadSurface.None || tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
                {
                    continue;
                }

                var links = new List<GridPosition>();
                foreach (var d in dirs)
                {
                    var n = new GridPosition(tile.X + d.X, tile.Y + d.Y);
                    if ((roads.TryGetValue(n, out var other) && other != RoadSurface.None) || joins.Contains(n))
                    {
                        links.Add(d);
                    }
                }

                var halfWidth = surface == RoadSurface.RaidersTrack ? 0.3f : surface == RoadSurface.Dirt ? 0.34f : 0.4f;
                for (var py = tile.Y * PixelsPerTile; py < (tile.Y + 1) * PixelsPerTile; py++)
                {
                    for (var px = tile.X * PixelsPerTile; px < (tile.X + 1) * PixelsPerTile; px++)
                    {
                        // Position inside the tile, -0.5..0.5 from its middle.
                        var lx = (px + 0.5f) / PixelsPerTile - tile.X - 0.5f;
                        var ly = (py + 0.5f) / PixelsPerTile - tile.Y - 0.5f;
                        var fray = Fbm((px + 0.5f) / PixelsPerTile * 3f, (py + 0.5f) / PixelsPerTile * 3f, 7) * 0.06f;
                        var inside = new Vector2(lx, ly).magnitude - (halfWidth + 0.04f);
                        var along = 0; // 1: runs along x, 2: along y (for boards and ruts)
                        foreach (var d in links)
                        {
                            // A band from the middle to the edge the neighbour is on.
                            var t = d.X != 0 ? lx * d.X : ly * d.Y;
                            var side = d.X != 0 ? Mathf.Abs(ly) : Mathf.Abs(lx);
                            var band = t >= 0f ? side - halfWidth : Mathf.Max(side - halfWidth, -t - halfWidth);
                            if (band < inside)
                            {
                                inside = band;
                                along = d.X != 0 ? 1 : 2;
                            }
                        }

                        var edge = inside + fray;
                        if (edge > 0.03f)
                        {
                            continue;
                        }

                        var blend = Mathf.Clamp01((0.03f - edge) / 0.06f);
                        var wx = (px + 0.5f) / PixelsPerTile;
                        var wy = (py + 0.5f) / PixelsPerTile;
                        var colour = Surface(surface, wx, wy, along == 0 ? (links.Count > 0 && links[0].X != 0 ? 1 : 2) : along, lx, ly);
                        var i = py * w + px;
                        pixels[i] = Color.Lerp(pixels[i], colour, blend);
                    }
                }
            }

            return pixels;
        }

        private static Color Surface(RoadSurface surface, float x, float y, int along, float lx, float ly)
        {
            var grain = Fbm(x * 2.3f, y * 2.3f, 9) * 0.06f;
            switch (surface)
            {
                case RoadSurface.Planks:
                {
                    // Boards across the way, with dark gaps between them.
                    var across = along == 1 ? x : y;
                    var gap = Mathf.Repeat(across * 4f, 1f) < 0.12f;
                    return (gap ? PlankGap : Plank) * (0.94f + grain);
                }

                case RoadSurface.Cobbles:
                {
                    var row = Mathf.FloorToInt(y * 4f);
                    var cx = Mathf.Repeat(x * 4f + (row % 2) * 0.5f, 1f);
                    var cy = Mathf.Repeat(y * 4f, 1f);
                    var gap = cx < 0.12f || cy < 0.12f;
                    return (gap ? StoneGap : Stone) * (0.92f + grain + Hash(Mathf.FloorToInt(x * 4f + (row % 2) * 0.5f), row) * 0.1f);
                }

                case RoadSurface.Dirt:
                {
                    // Two cart ruts running along the way.
                    var side = along == 1 ? ly : lx;
                    var rut = Mathf.Abs(Mathf.Abs(side) - 0.15f) < 0.05f;
                    return (rut ? Rut : Dirt) * (0.95f + grain);
                }

                default:
                    return Track * (0.95f + grain);
            }
        }

        // ---------------- noise ----------------

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xffff) / 65535f;
            }
        }

        private static float Noise(float x, float y, int salt)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            var u = fx * fx * (3f - 2f * fx);
            var v = fy * fy * (3f - 2f * fy);
            var s = salt * 7919;
            return Mathf.Lerp(Mathf.Lerp(Hash(ix + s, iy), Hash(ix + 1 + s, iy), u), Mathf.Lerp(Hash(ix + s, iy + 1), Hash(ix + 1 + s, iy + 1), u), v) * 2f - 1f;
        }

        private static float Fbm(float x, float y, int salt) =>
            (Noise(x, y, salt) * 0.6f + Noise(x * 2.1f + 5.3f, y * 2.1f - 1.7f, salt + 1) * 0.3f + Noise(x * 4.3f - 2.9f, y * 4.3f + 3.1f, salt + 2) * 0.1f) * 1.3f;
    }
}
