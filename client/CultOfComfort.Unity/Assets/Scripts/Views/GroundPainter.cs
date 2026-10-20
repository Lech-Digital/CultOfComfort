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
        /// <summary>The raiders' road, gravelled (its first paving).</summary>
        Gravel,
        /// <summary>The raiders' road, cobbled: wider and darker than the village's own cobbles.</summary>
        RaidersCobbles,
    }

    /// <summary>
    /// Paints the village's ground as one picture (owner 2026-10-15: "can we make the [tiles] less visible… roads should
    /// be connected… so they look like a road, not like tiles"): mud in soft patches of damp and dry with tufts of moss,
    /// and every road a continuous band joining its neighbours, worn into the mud (ruts for dirt, boards for planks,
    /// stones for cobbles). Pure: colours in, colours out.
    /// </summary>
    public static class GroundPainter
    {
        public const int PixelsPerTile = 32;

        private static readonly Color Mud = Shapes.Hex("#3d3225");
        private static readonly Color DryMud = Shapes.Hex("#4a3d2b");
        private static readonly Color DampMud = Shapes.Hex("#2f271d");
        private static readonly Color Moss = Shapes.Hex("#3f4a2a");
        // The raiders' road: trampled darker than any track the cult lays, with hoof and boot prints along it, so
        // "That's the raiders' road" points at something you can see (2026-10-16: it looked like the village's tracks).
        private static readonly Color Track = Shapes.Hex("#2c2117");
        private static readonly Color Print = Shapes.Hex("#1a130d");
        private static readonly Color Dirt = Shapes.Hex("#6b5238");
        private static readonly Color DirtLight = Shapes.Hex("#7d6244");
        private static readonly Color Rut = Shapes.Hex("#4e3b28");
        private static readonly Color Pebble = Shapes.Hex("#857d70");
        private static readonly Color Kerb = Shapes.Hex("#77726a");
        private static readonly Color Gravel = Shapes.Hex("#57503f");
        private static readonly Color GravelLoose = Shapes.Hex("#6e6553");
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
        /// Hearth, or a gate), with wandering, frayed edges and a worn verge. Returns a new array; <paramref name="ground"/>
        /// is untouched.
        /// </summary>
        public static Color32[] WithRoads(Color32[] ground, int width, int height, IReadOnlyDictionary<GridPosition, RoadSurface> roads,
            ICollection<GridPosition> joins)
        {
            var w = width * PixelsPerTile;
            var pixels = (Color32[])ground.Clone();
            var dirs = new[] { new GridPosition(1, 0), new GridPosition(-1, 0), new GridPosition(0, 1), new GridPosition(0, -1) };
            List<GridPosition> Links(GridPosition tile)
            {
                var links = new List<GridPosition>();
                foreach (var d in dirs)
                {
                    var n = new GridPosition(tile.X + d.X, tile.Y + d.Y);
                    if ((roads.TryGetValue(n, out var other) && other != RoadSurface.None) || joins.Contains(n))
                    {
                        links.Add(d);
                    }
                }

                return links;
            }

            // Straight stretches carry ruts; bends and crossings don't (crossed ruts drew a grid of lines: owner 2026-10-20 "the roads are not looking great").
            var straight = new HashSet<GridPosition>();
            foreach (var (tile, surface) in roads)
            {
                var l = Links(tile);
                if (surface != RoadSurface.None && (l.Count == 1 || (l.Count == 2 && l[0].X == -l[1].X && l[0].Y == -l[1].Y)))
                {
                    straight.Add(tile);
                }
            }

            // Two passes: the verge (darkened mud beside every road, the strongest wear wins) first, then the roads over it.
            var worn = new float[pixels.Length];
            var pebbles = new bool[pixels.Length];
            for (var pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        if (worn[i] > 0f)
                        {
                            var c = (Color)pixels[i] * (1f - worn[i]);
                            c = pebbles[i] ? Color.Lerp(c, Pebble, 0.5f) : c;
                            c.a = 1f;
                            pixels[i] = c;
                        }
                    }
                }

                foreach (var (tile, surface) in roads)
                {
                    if (surface == RoadSurface.None || tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
                    {
                        continue;
                    }

                    var links = Links(tile);
                    var isStraight = straight.Contains(tile);
                    var baseHalf = HalfWidth(surface);
                    var reach = pass == 0 ? PixelsPerTile / 6 : 0; // the verge spills a little past the tile
                    for (var py = tile.Y * PixelsPerTile - reach; py < (tile.Y + 1) * PixelsPerTile + reach; py++)
                    {
                        for (var px = tile.X * PixelsPerTile - reach; px < (tile.X + 1) * PixelsPerTile + reach; px++)
                        {
                            if (px < 0 || py < 0 || px >= w || py >= height * PixelsPerTile)
                            {
                                continue;
                            }

                            var wx = (px + 0.5f) / PixelsPerTile;
                            var wy = (py + 0.5f) / PixelsPerTile;
                            // Position from the tile's middle (-0.5..0.5 inside it).
                            var lx = wx - tile.X - 0.5f;
                            var ly = wy - tile.Y - 0.5f;
                            var inTile = Mathf.Abs(lx) <= 0.5f && Mathf.Abs(ly) <= 0.5f;
                            if (pass == 1 && !inTile)
                            {
                                continue;
                            }

                            // The road wanders: wider and narrower along its length, never ruler-straight.
                            var halfWidth = baseHalf * (1f + Fbm(wx * 0.5f, wy * 0.5f, 11) * 0.16f);
                            var inside = new Vector2(lx, ly).magnitude - (halfWidth + 0.04f);
                            var along = 0; // 1: runs along x, 2: along y
                            foreach (var d in links)
                            {
                                var t = d.X != 0 ? lx * d.X : ly * d.Y;
                                var side = d.X != 0 ? Mathf.Abs(ly) : Mathf.Abs(lx);
                                // From the middle to the neighbour's edge, with a round end behind the middle: bends come out rounded.
                                var band = t >= 0f ? side - halfWidth : new Vector2(t, side).magnitude - halfWidth;
                                if (band < inside)
                                {
                                    inside = band;
                                    along = d.X != 0 ? 1 : 2;
                                }
                            }

                            var edge = inside + Fbm(wx * 1.8f, wy * 1.8f, 7) * 0.04f;
                            var i = py * w + px;
                            if (pass == 0)
                            {
                                // The verge: trodden mud just outside the edge, with a few pebbles kicked aside.
                                if (edge > 0f && edge < 0.12f && surface != RoadSurface.RaidersTrack)
                                {
                                    worn[i] = Mathf.Max(worn[i], (1f - edge / 0.12f) * 0.22f);
                                    pebbles[i] = Hash(px * 3, py * 5) > 0.996f;
                                }

                                continue;
                            }

                            if (edge > 0.02f)
                            {
                                continue;
                            }

                            var blend = Mathf.Clamp01((0.02f - edge) / 0.05f);
                            if (along == 0)
                            {
                                along = links.Count > 0 && links[0].X != 0 ? 1 : 2;
                            }

                            // Ruts fade out towards a neighbour that bends or crosses, so they never stop dead at a tile edge.
                            var rutStrength = isStraight ? RutFade(tile, along, lx, ly, straight) : 0f;
                            var colour = Surface(surface, wx, wy, along, lx, ly, -edge, rutStrength);
                            colour.a = 1f;
                            pixels[i] = Color.Lerp(pixels[i], colour, blend);
                        }
                    }
                }
            }

            return pixels;
        }

        private static float HalfWidth(RoadSurface surface) => surface switch
        {
            RoadSurface.RaidersTrack => 0.34f,
            RoadSurface.Dirt => 0.36f,
            RoadSurface.Planks => 0.34f,
            RoadSurface.Gravel => 0.38f,
            RoadSurface.RaidersCobbles => 0.42f,
            _ => 0.38f,
        };

        /// <summary>1 in a straight stretch; fading to 0 across the half tile next to a neighbour that isn't straight.</summary>
        private static float RutFade(GridPosition tile, int along, float lx, float ly, HashSet<GridPosition> straight)
        {
            var t = along == 1 ? lx : ly;
            var next = along == 1 ? new GridPosition(tile.X + (t >= 0 ? 1 : -1), tile.Y) : new GridPosition(tile.X, tile.Y + (t >= 0 ? 1 : -1));
            return straight.Contains(next) ? 1f : Mathf.Clamp01((0.5f - Mathf.Abs(t)) / 0.4f);
        }

        /// <param name="depth">How far inside the edge (tiles): kerbs and worn edges use it.</param>
        /// <param name="ruts">0..1: how strongly the cart ruts show here.</param>
        private static Color Surface(RoadSurface surface, float x, float y, int along, float lx, float ly, float depth, float ruts)
        {
            var grain = Fbm(x * 2.3f, y * 2.3f, 9) * 0.06f;
            var speck = Hash(Mathf.FloorToInt(x * PixelsPerTile), Mathf.FloorToInt(y * PixelsPerTile));
            var side = along == 1 ? ly : lx;
            var step = along == 1 ? x : y;
            switch (surface)
            {
                case RoadSurface.Planks:
                {
                    // Boards across the way, each its own shade, with dark gaps and a nail at each end.
                    var board = Mathf.FloorToInt(step * 5f);
                    var inBoard = Mathf.Repeat(step * 5f, 1f);
                    var gap = inBoard < 0.1f;
                    var tone = 0.86f + Hash(board, along) * 0.2f;
                    var nail = Mathf.Abs(inBoard - 0.55f) < 0.12f && Mathf.Abs(Mathf.Abs(side) - 0.26f) < 0.025f;
                    var c = gap ? PlankGap : nail ? PlankGap * 1.2f : Plank * tone;
                    return c * (0.94f + grain + Fbm(step * 9f, side * 1.5f, 13) * 0.05f) * Edge(depth);
                }

                case RoadSurface.Gravel:
                {
                    // Grey-brown grit, packed down the middle, loose and lighter at the sides; a scatter of bigger stones.
                    var c = Color.Lerp(Gravel, GravelLoose, Mathf.Clamp01(1f - depth * 4f));
                    var stone = Hash(Mathf.FloorToInt(x * 12f), Mathf.FloorToInt(y * 12f)) > 0.82f
                        && new Vector2(Mathf.Repeat(x * 12f, 1f) - 0.5f, Mathf.Repeat(y * 12f, 1f) - 0.5f).magnitude < 0.3f;
                    c = stone ? Color.Lerp(c, Pebble, 0.7f) : c;
                    return c * (0.78f + speck * 0.34f + grain * 2f) * Edge(depth);
                }

                case RoadSurface.RaidersCobbles:
                case RoadSurface.Cobbles:
                {
                    // Round stones on staggered rows, each its own shade, lit from the top left; a row of kerb stones on each side.
                    const float rows = 5f;
                    var row = Mathf.FloorToInt(y * rows);
                    var shift = (row & 1) * 0.5f;
                    var col = Mathf.FloorToInt(x * rows + shift);
                    var cx = Mathf.Repeat(x * rows + shift, 1f) - 0.5f + (Hash(col, row * 7) - 0.5f) * 0.12f;
                    var cy = Mathf.Repeat(y * rows, 1f) - 0.5f + (Hash(col * 5, row) - 0.5f) * 0.12f;
                    var r = Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cy)) * 0.55f + new Vector2(cx, cy).magnitude * 0.45f;
                    var kerb = depth < 0.07f;
                    if (r > 0.4f && !kerb)
                    {
                        return StoneGap * (0.9f + grain);
                    }

                    var light = 1f + (-cx - cy) * 0.25f;
                    var tone = 0.88f + Hash(col, row) * 0.18f;
                    var dark = surface == RoadSurface.RaidersCobbles ? 0.72f : 1f; // the raiders' cobbles: older, darker stone
                    return (kerb ? Kerb : Stone) * tone * light * dark * (0.96f + grain);
                }

                case RoadSurface.Dirt:
                {
                    // Packed earth, lighter where feet go, with two wobbling cart ruts on the straight and a little grit.
                    var wobble = Fbm(step * 1.3f, 0.5f, 17) * 0.04f;
                    var rut = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Abs(side + wobble) - 0.15f) / 0.045f) * ruts;
                    var c = Color.Lerp(Dirt, DirtLight, Mathf.Clamp01(depth * 3f) * 0.5f);
                    c = Color.Lerp(c, Rut, rut * 0.75f);
                    if (speck > 0.99f)
                    {
                        c = Color.Lerp(c, Pebble, 0.3f);
                    }

                    return c * (0.95f + grain) * Edge(depth);
                }

                default:
                {
                    // The raiders' road: trampled dark, with prints in a staggered line along it.
                    var k = Mathf.FloorToInt(step * 3f);
                    var jitter = (Hash(k, 3) - 0.5f) * 0.08f;
                    var print = Mathf.Abs(Mathf.Repeat(step * 3f, 1f) - 0.5f) < 0.13f && Mathf.Abs(side - (k % 2 == 0 ? 0.1f : -0.1f) - jitter) < 0.05f;
                    var c = Color.Lerp(Track, print ? Print : Track, 0.8f);
                    if (speck > 0.975f)
                    {
                        c = Color.Lerp(c, Pebble, 0.35f);
                    }

                    return c * (0.95f + grain);
                }
            }
        }

        /// <summary>A soft darkening at the road's edge, so it sits in the ground rather than on it.</summary>
        private static float Edge(float depth) => 0.9f + Mathf.Clamp01(depth / 0.08f) * 0.1f;

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
