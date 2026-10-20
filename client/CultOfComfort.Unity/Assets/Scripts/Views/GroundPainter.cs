using System;
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

        /// <summary>Touches the painter's colours on the main thread (they're parsed by Unity), before any painting runs elsewhere.</summary>
        public static void Warm() => _ = Mud;

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

        private static readonly Color Shallow = Shapes.Hex("#557f80");
        private static readonly Color Deep = Shapes.Hex("#2b4f60");
        private static readonly Color Foam = Shapes.Hex("#a9bdb5");
        private static readonly Color WetMud = Shapes.Hex("#2e251b");
        private static readonly Color Sand = Shapes.Hex("#6e6047");
        private static readonly Color ForestFloor = Shapes.Hex("#26301c");

        /// <summary>
        /// The bare ground, <paramref name="width"/> × <paramref name="height"/> tiles, row 0 at tile y 0 (the board's
        /// top), with the river painted in (spec 2026-10-20 river): deep in the middle, shallow and foaming at the edge,
        /// a dark wet bank and then sand and pebbles. <paramref name="river"/> gives the distance from the water's edge
        /// (tile units, tile centres on whole numbers; negative in the water).
        /// </summary>
        public static Color32[] Ground(int width, int height, Func<double, double, double> river = null)
        {
            // The noise and the river are smooth: worked out every few pixels and blended between, with per-pixel grit
            // on top. Every pixel at 32 a tile took seconds on the phone.
            const int step = 4;
            var w = width * PixelsPerTile;
            var h = height * PixelsPerTile;
            var cw = w / step + 2;
            var ch = h / step + 2;
            var coarse = new Color[cw * ch];
            var edge = new float[cw * ch];
            for (var cy = 0; cy < ch; cy++)
            {
                for (var cx = 0; cx < cw; cx++)
                {
                    var x = (cx * step + 0.5f) / PixelsPerTile;
                    var y = (cy * step + 0.5f) / PixelsPerTile;
                    var patch = Fbm(x / 4.5f, y / 4.5f, 1);
                    var fine = Fbm(x * 1.7f, y * 1.7f, 2);
                    var colour = Color.Lerp(Mud, patch > 0 ? DryMud : DampMud, Mathf.Abs(patch) * 1.4f);
                    colour = Color.Lerp(colour, Moss, Mathf.Clamp01((Fbm(x / 3f, y / 3f, 3) - 0.12f) * 2.5f) * 0.85f);
                    colour *= 0.94f + fine * 0.08f;
                    var d = river == null ? 9f : (float)river(x - 0.5, y - 0.5);
                    coarse[cy * cw + cx] = d < 0.9f ? RiverColour(colour, d, x, y) : colour;
                    edge[cy * cw + cx] = d;
                }
            }

            var pixels = new Color32[w * h];
            for (var py = 0; py < h; py++)
            {
                var gy = (float)py / step;
                var y0 = (int)gy;
                var v = gy - y0;
                for (var px = 0; px < w; px++)
                {
                    var gx = (float)px / step;
                    var x0 = (int)gx;
                    var u = gx - x0;
                    var a = y0 * cw + x0;
                    var colour = Color.Lerp(Color.Lerp(coarse[a], coarse[a + 1], u), Color.Lerp(coarse[a + cw], coarse[a + cw + 1], u), v);
                    var grit = Hash(px, py);
                    colour *= 0.975f + grit * 0.05f;
                    var d = Mathf.Lerp(Mathf.Lerp(edge[a], edge[a + 1], u), Mathf.Lerp(edge[a + cw], edge[a + cw + 1], u), v);
                    if (d > 0.05f && d < 0.4f && Hash(px * 7, py * 3) > 0.985f)
                    {
                        colour = Color.Lerp(colour, Pebble, 0.6f); // pebbles on the bank
                    }

                    colour.a = 1f;
                    pixels[py * w + px] = colour;
                }
            }

            return pixels;
        }

        private static Color RiverColour(Color ground, float d, float x, float y)
        {
            if (d >= 0.9f)
            {
                return ground;
            }

            if (d >= 0f)
            {
                // The bank: dark wet mud at the water, then sand and pebbles, then the ordinary mud.
                var bank = Color.Lerp(WetMud, Sand, Mathf.Clamp01(d / 0.35f));
                return Color.Lerp(bank, ground, Mathf.Clamp01((d - 0.45f) / 0.45f + Fbm(x * 2f, y * 2f, 23) * 0.3f));
            }

            // The water: deeper towards the middle, streaks of current, foam along the edge.
            var depth = Mathf.Clamp01(-d / 1.3f);
            var water = Color.Lerp(Shallow, Deep, depth);
            var streak = Fbm(x * 0.9f + y * 0.35f, y * 0.9f - x * 0.35f, 21);
            water = Color.Lerp(water, Shallow * 1.25f, Mathf.Clamp01((streak - 0.25f) * 1.6f) * 0.35f);
            var foam = Mathf.Clamp01(1f + d / 0.14f) * (0.5f + Fbm(x * 4f, y * 4f, 25) * 0.4f);
            return Color.Lerp(water, Foam, foam);
        }

        /// <summary>
        /// The forest floor on land that hasn't opened (spec 2026-10-20 river: painted, so a road through the forest
        /// stays visible): dark and mossy, fading over a tile into the open mud. Returns a new array.
        /// </summary>
        public static Color32[] WithForestFloor(Color32[] ground, int width, int height, Func<GridPosition, bool> forested)
        {
            var w = width * PixelsPerTile;
            var pixels = (Color32[])ground.Clone();
            var fields = FieldsFor(width, height);
            var mask = new float[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    mask[y * width + x] = forested(new GridPosition(x, y)) ? 1f : 0f;
                }
            }

            float At(int x, int y) => mask[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];
            for (var ty = 0; ty < height; ty++)
            {
                for (var tx = 0; tx < width; tx++)
                {
                    var near = At(tx, ty) + At(tx - 1, ty) + At(tx + 1, ty) + At(tx, ty - 1) + At(tx, ty + 1);
                    if (near == 0f)
                    {
                        continue;
                    }

                    for (var py = ty * PixelsPerTile; py < (ty + 1) * PixelsPerTile; py++)
                    {
                        for (var px = tx * PixelsPerTile; px < (tx + 1) * PixelsPerTile; px++)
                        {
                            // Bilinear over the tile centres: a soft edge a tile wide, roughened with noise.
                            var fx = (px + 0.5f) / PixelsPerTile - 0.5f;
                            var fy = (py + 0.5f) / PixelsPerTile - 0.5f;
                            var x0 = Mathf.FloorToInt(fx);
                            var y0 = Mathf.FloorToInt(fy);
                            var u = fx - x0;
                            var v = fy - y0;
                            var m = Mathf.Lerp(Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), u), Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), u), v);
                            m = Mathf.Clamp01((m - 0.5f) * 2.2f + 0.5f + fields.FloorEdge.At(fx, fy) * 0.35f);
                            if (m <= 0f)
                            {
                                continue;
                            }

                            var i = py * w + px;
                            var c = Color.Lerp(pixels[i], ForestFloor * (0.9f + fields.FloorTone.At(fx, fy) * 0.15f), m * 0.85f);
                            c.a = 1f;
                            pixels[i] = c;
                        }
                    }
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
            ICollection<GridPosition> joins, Func<double, double, double> river = null)
        {
            var w = width * PixelsPerTile;
            var pixels = (Color32[])ground.Clone();
            var fields = FieldsFor(width, height);
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
                            if (river != null && river(wx - 0.5, wy - 0.5) < 0.15)
                            {
                                continue; // over the water a road is a bridge (a model), not paint
                            }

                            // Position from the tile's middle (-0.5..0.5 inside it).
                            var lx = wx - tile.X - 0.5f;
                            var ly = wy - tile.Y - 0.5f;
                            var inTile = Mathf.Abs(lx) <= 0.5f && Mathf.Abs(ly) <= 0.5f;
                            if (pass == 1 && !inTile)
                            {
                                continue;
                            }

                            // The road wanders: wider and narrower along its length, never ruler-straight.
                            var halfWidth = baseHalf * (1f + fields.Width.At(wx, wy) * 0.16f);
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

                            var edge = inside + fields.Fray.At(wx, wy) * 0.04f;
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
                            var colour = Surface(fields, surface, wx, wy, along, lx, ly, -edge, rutStrength);
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
        private static Color Surface(Fields fields, RoadSurface surface, float x, float y, int along, float lx, float ly, float depth, float ruts)
        {
            var grain = fields.Grain.At(x, y) * 0.06f;
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

        /// <summary>
        /// A smooth noise worked out once over the map, eight samples a tile, and read back blended: what the road and
        /// forest painters used to compute for every pixel (seconds on a phone at 32 pixels a tile).
        /// </summary>
        private sealed class Field
        {
            private const int PerTile = 8;
            private readonly float[] _values;
            private readonly int _columns;
            private readonly int _rows;

            public Field(int width, int height, Func<float, float, float> noise)
            {
                _columns = (width + 2) * PerTile + 2;
                _rows = (height + 2) * PerTile + 2;
                _values = new float[_columns * _rows];
                for (var r = 0; r < _rows; r++)
                {
                    for (var c = 0; c < _columns; c++)
                    {
                        _values[r * _columns + c] = noise((float)c / PerTile - 1f, (float)r / PerTile - 1f);
                    }
                }
            }

            public float At(float x, float y)
            {
                var gx = Mathf.Clamp((x + 1f) * PerTile, 0f, _columns - 1.001f);
                var gy = Mathf.Clamp((y + 1f) * PerTile, 0f, _rows - 1.001f);
                var x0 = (int)gx;
                var y0 = (int)gy;
                var u = gx - x0;
                var v = gy - y0;
                var a = y0 * _columns + x0;
                return Mathf.Lerp(Mathf.Lerp(_values[a], _values[a + 1], u), Mathf.Lerp(_values[a + _columns], _values[a + _columns + 1], u), v);
            }
        }

        /// <summary>The fields the painters read, for one map size.</summary>
        private sealed class Fields
        {
            public Fields(int width, int height)
            {
                Width = new Field(width, height, (x, y) => Fbm(x * 0.5f, y * 0.5f, 11));
                Fray = new Field(width, height, (x, y) => Fbm(x * 1.8f, y * 1.8f, 7));
                Grain = new Field(width, height, (x, y) => Fbm(x * 2.3f, y * 2.3f, 9));
                FloorEdge = new Field(width, height, (x, y) => Fbm(x * 1.5f, y * 1.5f, 27));
                FloorTone = new Field(width, height, (x, y) => Fbm(x * 3f, y * 3f, 29));
            }

            public Field Width { get; }
            public Field Fray { get; }
            public Field Grain { get; }
            public Field FloorEdge { get; }
            public Field FloorTone { get; }
        }

        private static readonly Dictionary<(int, int), Fields> FieldsBySize = new Dictionary<(int, int), Fields>();

        private static Fields FieldsFor(int width, int height)
        {
            lock (FieldsBySize)
            {
                if (!FieldsBySize.TryGetValue((width, height), out var fields))
                {
                    fields = new Fields(width, height);
                    FieldsBySize[(width, height)] = fields;
                }

                return fields;
            }
        }

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
