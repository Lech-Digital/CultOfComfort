using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>A place on the land: a world node (or your village, <see cref="Type"/> null) and where it stands.</summary>
    public readonly struct LandPlace
    {
        public LandPlace(int id, NodeType? type, int q, int r, Vector2 at)
        {
            Id = id;
            Type = type;
            Q = q;
            R = r;
            At = at;
        }

        public int Id { get; }

        public NodeType? Type { get; }

        public int Q { get; }

        public int R { get; }

        /// <summary>Land position (x, z).</summary>
        public Vector2 At { get; }
    }

    /// <summary>A tree, rock or mountain the land grows in a chunk.</summary>
    public readonly struct LandProp
    {
        public LandProp(string model, Vector3 at, float yaw, float scale)
        {
            Model = model;
            At = at;
            Yaw = yaw;
            Scale = scale;
        }

        public string Model { get; }

        public Vector3 At { get; }

        public float Yaw { get; }

        public float Scale { get; }
    }

    /// <summary>
    /// The world's land, grown from the world's seed (owner 2026-10-15: "more of a real map, like in kingshot"): rolling
    /// hills, forests, rivers and lakes, mountains round the edge, and tracks between neighbouring places. Places sit on
    /// their hex but jiggle off the grid, so nothing looks like a board game. Pure and deterministic: every phone grows
    /// the same land, and the 3D map (<see cref="WorldStage"/>) streams it in chunks.
    /// </summary>
    public sealed class WorldLand
    {
        /// <summary>Hex centre to corner, in land units (a cottage is about 0.55 across on the map).</summary>
        public const float HexSize = 2f;

        public const float WaterLevel = 0f;
        public const float ChunkSize = 16f;
        public const int ChunkPixels = 64;
        public const int ChunkCells = 32;

        private const float PlaceFlat = 1.7f;
        private const float RoadHalfWidth = 0.2f;
        private const float BucketSize = 8f;

        private static readonly Color Grass = Shapes.Hex("#6f7a49");
        private static readonly Color GrassAlt = Shapes.Hex("#5f6b3c");
        private static readonly Color Meadow = Shapes.Hex("#86894f");
        private static readonly Color ForestFloor = Shapes.Hex("#4c5a30");
        private static readonly Color Shore = Shapes.Hex("#8f7d56");
        private static readonly Color Riverbed = Shapes.Hex("#4e5f55");
        private static readonly Color RoadMud = Shapes.Hex("#8a6a47");
        private static readonly Color Planks = Shapes.Hex("#6d4c2f");
        private static readonly Color Trodden = Shapes.Hex("#7d6a45");
        private static readonly Color Rock = Shapes.Hex("#7f7b73");
        private static readonly Color Snow = Shapes.Hex("#ece6d6");

        private readonly uint _seed;
        private readonly int _radius;
        private readonly List<LandPlace> _places = new List<LandPlace>();
        private readonly List<float> _placeGround = new List<float>();
        private readonly Dictionary<(int, int), List<int>> _placeBuckets = new Dictionary<(int, int), List<int>>();
        private readonly List<Vector2[]> _roads = new List<Vector2[]>();
        private readonly List<(int A, int B)> _roadEnds = new List<(int, int)>();
        private readonly Dictionary<(int, int), List<int>> _roadBuckets = new Dictionary<(int, int), List<int>>();
        private readonly Dictionary<int, int> _placeIndex = new Dictionary<int, int>();
        private readonly List<Vector2> _seats = new List<Vector2>();
        private static readonly Color BorderInk = Shapes.Hex("#5a2a20");

        /// <param name="seed">The world's seed.</param>
        /// <param name="radius">The world's radius in hexes (mountains beyond).</param>
        /// <param name="places">Every place in the world.</param>
        /// <param name="home">Your village (null: none, e.g. a preview).</param>
        public WorldLand(ulong seed, int radius, IEnumerable<(int Id, NodeType Type, int Q, int R)> places, (int Q, int R)? home)
        {
            _seed = unchecked((uint)seed ^ (uint)(seed >> 32) ^ 0x9e3779b9u);
            _radius = radius;
            foreach (var p in places)
            {
                AddPlace(new LandPlace(p.Id, p.Type, p.Q, p.R, Spot(p.Q, p.R)));
            }

            if (home != null)
            {
                AddPlace(new LandPlace(WorldMapPresenter.HomeId, null, home.Value.Q, home.Value.R, Spot(home.Value.Q, home.Value.R)));
            }

            LayRoads();
        }

        public int Radius => _radius;

        public IReadOnlyList<LandPlace> Places => _places;

        public IReadOnlyList<Vector2[]> Roads => _roads;

        /// <summary>The unjiggled centre of hex (q, r): pointy-top, r grows towards the viewer (−z).</summary>
        public static Vector2 HexCentre(int q, int r) => new Vector2(HexSize * Mathf.Sqrt(3f) * (q + r / 2f), -HexSize * 1.5f * r);

        /// <summary>Fractional hex distance of a land point from the world's centre.</summary>
        public static float HexDistanceFromCentre(Vector2 at)
        {
            var r = -at.y / (HexSize * 1.5f);
            var q = at.x / (HexSize * Mathf.Sqrt(3f)) - r / 2f;
            return (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2f;
        }

        /// <summary>Where something on hex (q, r) stands: off the centre by up to half a unit, the same every time.</summary>
        public Vector2 Spot(int q, int r)
        {
            var a = Hash01(q, r, 101) * Mathf.PI * 2f;
            var d = 0.15f + Hash01(q, r, 102) * 0.35f;
            return HexCentre(q, r) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
        }

        public bool TryPlace(int id, out LandPlace place)
        {
            var found = _placeIndex.TryGetValue(id, out var index);
            place = found ? _places[index] : default;
            return found;
        }

        /// <summary>The ground height at a land point (water level 0).</summary>
        public float Height(Vector2 at)
        {
            var h = Natural(at, out _);
            return Shape(at, h, out _, out _);
        }

        /// <summary>The ground's colour at a land point (the chunk textures).</summary>
        public Color Colour(Vector2 at)
        {
            var natural = Natural(at, out var river);
            var h = Shape(at, natural, out var road, out var near);
            var edge = HexDistanceFromCentre(at) - (_radius - 0.5f);
            var detail = Fbm(at.x / 3.1f, at.y / 3.1f, 11, 2);
            var colour = Color.Lerp(Grass, GrassAlt, 0.5f + detail * 0.9f);
            colour = Color.Lerp(colour, Meadow, Mathf.Clamp01((Fbm(at.x / 13f, at.y / 13f, 12, 2) - 0.15f) * 2.2f));
            colour = Color.Lerp(colour, ForestFloor, Mathf.Clamp01((Forest(at) - 0.05f) * 4f));
            colour = Color.Lerp(colour, Meadow, Mathf.Clamp01((h - 1.0f) * 1.2f));
            if (edge > 0f)
            {
                colour = Color.Lerp(colour, Rock, Mathf.Clamp01(edge * 0.9f + detail * 0.3f));
                colour = Color.Lerp(colour, Snow, Mathf.Clamp01((h - 3.6f) * 0.8f));
            }

            colour = Color.Lerp(colour, Shore, Mathf.Clamp01(1f - (h - WaterLevel) / 0.16f));
            if (h < WaterLevel)
            {
                colour = Color.Lerp(Shore, Riverbed, Mathf.Clamp01(-h * 4f));
            }

            colour = Color.Lerp(colour, Trodden, Mathf.Clamp01((PlaceFlat - 0.6f - near) * 1.4f) * 0.55f);
            var onRoad = Mathf.Clamp01((RoadHalfWidth + 0.08f - road) / 0.12f);
            colour = Color.Lerp(colour, river > 0.5f ? Planks : RoadMud, onRoad);
            if (h >= WaterLevel && edge < 0f)
            {
                colour = Color.Lerp(colour, BorderInk, Border(at) * 0.75f);
            }

            return colour;
        }

        /// <summary>
        /// 0–1: how much of a domain border is drawn here. Each lord's lands are the ground nearest his seat (as the
        /// world's generator hands out places); the border is a dashed line of ink where two seats are about as near.
        /// </summary>
        public float Border(Vector2 at)
        {
            if (_seats.Count < 2)
            {
                return 0f;
            }

            var first = float.MaxValue;
            var second = float.MaxValue;
            foreach (var seat in _seats)
            {
                var d = (seat - at).sqrMagnitude;
                if (d < first)
                {
                    second = first;
                    first = d;
                }
                else if (d < second)
                {
                    second = d;
                }
            }

            var gap = Mathf.Sqrt(second) - Mathf.Sqrt(first);
            var line = Mathf.Clamp01((0.32f - gap) / 0.12f);
            var dash = (Mathf.Repeat(at.x * 0.8f, 1f) < 0.55f) == (Mathf.Repeat(at.y * 0.8f, 1f) < 0.55f) ? 1f : 0.15f; // dashed whichever way it runs
            return line * dash;
        }

        /// <summary>Trees, rocks and the mountains round the edge, in the chunk at (cx, cz).</summary>
        public List<LandProp> Props(int cx, int cz)
        {
            var props = new List<LandProp>();
            var origin = new Vector2(cx * ChunkSize, cz * ChunkSize);
            const float step = 0.62f;
            var cells = Mathf.RoundToInt(ChunkSize / step);
            for (var i = 0; i < cells; i++)
            {
                for (var j = 0; j < cells; j++)
                {
                    var gx = cx * cells + i;
                    var gz = cz * cells + j;
                    var at = origin + new Vector2((i + 0.15f + Hash01(gx, gz, 21) * 0.7f) * step, (j + 0.15f + Hash01(gx, gz, 22) * 0.7f) * step);
                    var edge = HexDistanceFromCentre(at) - (_radius - 0.5f);
                    if (edge > -0.3f)
                    {
                        continue;
                    }

                    var forest = Forest(at);
                    var chance = forest > 0.05f ? 0.8f : forest > -0.08f ? 0.12f : 0.025f;
                    if (Hash01(gx, gz, 23) > chance)
                    {
                        continue;
                    }

                    var h = Natural(at, out _);
                    h = Shape(at, h, out var road, out var near);
                    if (h < WaterLevel + 0.12f || road < RoadHalfWidth + 0.35f || near < 1.35f)
                    {
                        continue;
                    }

                    var pick = Hash01(gx, gz, 24);
                    var model = h > 0.9f || forest > 0.3f ? (pick < 0.85f ? "pine" : "birch") : pick < 0.45f ? "oak" : pick < 0.8f ? "pine" : "birch";
                    props.Add(new LandProp(model, new Vector3(at.x, h - 0.02f, at.y), Hash01(gx, gz, 25) * 360f, 0.3f + Hash01(gx, gz, 26) * 0.2f));
                }
            }

            // Mountains past the world's edge; a few crags on the high hills.
            const float big = 3.2f;
            var bigCells = Mathf.RoundToInt(ChunkSize / big);
            for (var i = 0; i < bigCells; i++)
            {
                for (var j = 0; j < bigCells; j++)
                {
                    var gx = cx * bigCells + i;
                    var gz = cz * bigCells + j;
                    var at = origin + new Vector2((i + 0.1f + Hash01(gx, gz, 31) * 0.8f) * big, (j + 0.1f + Hash01(gx, gz, 32) * 0.8f) * big);
                    var edge = HexDistanceFromCentre(at) - (_radius - 0.5f);
                    if (edge > 0.6f)
                    {
                        props.Add(new LandProp("mountain", new Vector3(at.x, Height(at) - 0.3f, at.y), Hash01(gx, gz, 33) * 360f,
                            1.1f + Mathf.Min(edge, 4f) * 0.35f + Hash01(gx, gz, 34) * 0.6f));
                        continue;
                    }

                    var h = Natural(at, out _);
                    h = Shape(at, h, out var road, out var near);
                    if (edge < 0f && h > 1.25f && road > 0.8f && near > 1.6f && Hash01(gx, gz, 35) < 0.55f)
                    {
                        props.Add(new LandProp("crag", new Vector3(at.x, h - 0.05f, at.y), Hash01(gx, gz, 36) * 360f, 0.5f + Hash01(gx, gz, 37) * 0.4f));
                    }
                }
            }

            return props;
        }

        /// <summary>The chunk's ground heights, (cells+1)² row-major from its south-west corner.</summary>
        public float[] ChunkHeights(int cx, int cz)
        {
            var n = ChunkCells + 1;
            var heights = new float[n * n];
            for (var j = 0; j < n; j++)
            {
                for (var i = 0; i < n; i++)
                {
                    heights[j * n + i] = Height(new Vector2((cx + i / (float)ChunkCells) * ChunkSize, (cz + j / (float)ChunkCells) * ChunkSize));
                }
            }

            return heights;
        }

        /// <summary>The chunk's ground colours, <see cref="ChunkPixels"/>² row-major from its south-west corner.</summary>
        public Color32[] ChunkColours(int cx, int cz)
        {
            var pixels = new Color32[ChunkPixels * ChunkPixels];
            for (var j = 0; j < ChunkPixels; j++)
            {
                for (var i = 0; i < ChunkPixels; i++)
                {
                    // Pixel centres on the chunk's edge line up with the neighbour's, so seams don't show.
                    var at = new Vector2((cx + i / (float)(ChunkPixels - 1)) * ChunkSize, (cz + j / (float)(ChunkPixels - 1)) * ChunkSize);
                    pixels[j * ChunkPixels + i] = Colour(at);
                }
            }

            return pixels;
        }

        // ---------------- the land's shape ----------------

        /// <summary>Hills, lakes, rivers and the mountains at the edge, before places and roads level it.</summary>
        private float Natural(Vector2 at, out float river)
        {
            var h = 0.55f + 1.15f * Fbm(at.x / 24f, at.y / 24f, 1, 4);
            var meander = Mathf.Abs(Fbm(at.x / 36f, at.y / 36f, 7, 3));
            river = Mathf.Clamp01(Mathf.InverseLerp(0.05f, 0.022f, meander));
            h = Mathf.Lerp(h, Mathf.Min(h, -0.32f), river);
            var edge = HexDistanceFromCentre(at) - (_radius - 0.5f);
            if (edge > 0f)
            {
                h = Mathf.Max(h, 0.3f) + edge * 1.5f;
            }

            return h;
        }

        /// <summary>Places stand on level ground; roads keep their feet dry (a causeway over a river).</summary>
        private float Shape(Vector2 at, float h, out float road, out float near)
        {
            road = RoadDistance(at);
            near = float.MaxValue;
            var target = h;
            foreach (var index in Nearby(_placeBuckets, at))
            {
                var d = Vector2.Distance(at, _places[index].At);
                if (d < near)
                {
                    near = d;
                    target = _placeGround[index];
                }
            }

            if (near < PlaceFlat)
            {
                h = Mathf.Lerp(h, target, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlaceFlat, 0.9f, near)));
            }

            if (road < RoadHalfWidth + 0.5f)
            {
                h = Mathf.Lerp(h, Mathf.Max(h, WaterLevel + 0.1f), Mathf.InverseLerp(RoadHalfWidth + 0.5f, RoadHalfWidth, road));
            }

            return h;
        }

        private float Forest(Vector2 at) => Fbm(at.x / 10f, at.y / 10f, 3, 3);

        private void AddPlace(LandPlace place)
        {
            _placeIndex[place.Id] = _places.Count;
            if (place.Type == NodeType.Keep || place.Type == NodeType.Cathedral)
            {
                _seats.Add(place.At);
            }
            _places.Add(place);
            _placeGround.Add(Mathf.Clamp(Natural(place.At, out _) * 0.6f + 0.15f, 0.2f, 1.4f));
            Bucket(_placeBuckets, place.At, place.At, _places.Count - 1, PlaceFlat);
        }

        /// <summary>
        /// Tracks between neighbouring places (one hex apart); a place with no neighbour joins the nearest within two.
        /// Each bends a little, the same way every time.
        /// </summary>
        private void LayRoads()
        {
            var byHex = new Dictionary<(int, int), int>();
            for (var i = 0; i < _places.Count; i++)
            {
                byHex[(_places[i].Q, _places[i].R)] = i;
            }

            var linked = new HashSet<(int, int)>();
            for (var i = 0; i < _places.Count; i++)
            {
                var a = _places[i];
                var joined = false;
                foreach (var (dq, dr) in new[] { (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1) })
                {
                    if (byHex.TryGetValue((a.Q + dq, a.R + dr), out var j))
                    {
                        joined = true;
                        Link(i, j);
                    }
                }

                if (!joined)
                {
                    var best = -1;
                    for (var dq = -2; dq <= 2; dq++)
                    {
                        for (var dr = -2; dr <= 2; dr++)
                        {
                            var d = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
                            if (d == 2 && byHex.TryGetValue((a.Q + dq, a.R + dr), out var j) && (best < 0 || j < best))
                            {
                                best = j; // all two hexes off: the first one found in the world's order
                            }
                        }
                    }

                    if (best >= 0)
                    {
                        Link(i, best);
                    }
                }
            }

            void Link(int i, int j)
            {
                var key = i < j ? (i, j) : (j, i);
                if (!linked.Add(key))
                {
                    return;
                }

                var from = _places[key.Item1].At;
                var to = _places[key.Item2].At;
                var side = new Vector2(-(to - from).y, (to - from).x).normalized;
                var bend = (Hash01(key.Item1, key.Item2, 41) - 0.5f) * 0.9f;
                var wiggle = (Hash01(key.Item1, key.Item2, 42) - 0.5f) * 0.35f;
                var points = new Vector2[9];
                for (var k = 0; k < points.Length; k++)
                {
                    var t = k / (float)(points.Length - 1);
                    points[k] = Vector2.Lerp(from, to, t) + side * (Mathf.Sin(t * Mathf.PI) * bend + Mathf.Sin(t * Mathf.PI * 2f) * wiggle);
                }

                _roads.Add(points);
                _roadEnds.Add(key);
                var min = Vector2.Min(from, to) - Vector2.one;
                var max = Vector2.Max(from, to) + Vector2.one;
                Bucket(_roadBuckets, min, max, _roads.Count - 1, 0f);
            }
        }

        /// <summary>
        /// The way a walker takes between two places (ids): along the tracks when they join up, the fewest steps first;
        /// straight across the fields when they don't. Empty when either place is unknown.
        /// </summary>
        public List<Vector2> Path(int fromId, int toId)
        {
            var path = new List<Vector2>();
            if (!_placeIndex.TryGetValue(fromId, out var from) || !_placeIndex.TryGetValue(toId, out var to))
            {
                return path;
            }

            var links = new Dictionary<int, List<(int Next, int Road)>>();
            for (var k = 0; k < _roadEnds.Count; k++)
            {
                var (a, b) = _roadEnds[k];
                Add(a, b, k);
                Add(b, a, k);
            }

            void Add(int a, int b, int road)
            {
                if (!links.TryGetValue(a, out var list))
                {
                    links[a] = list = new List<(int, int)>();
                }

                list.Add((b, road));
            }

            // Breadth first, but only a little way: missionaries never walk further than the Hearth reaches.
            var came = new Dictionary<int, (int From, int Road)> { [from] = (-1, -1) };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0 && !came.ContainsKey(to) && came.Count < 4000)
            {
                var at = queue.Dequeue();
                if (!links.TryGetValue(at, out var next))
                {
                    continue;
                }

                foreach (var (n, road) in next)
                {
                    if (!came.ContainsKey(n))
                    {
                        came[n] = (at, road);
                        queue.Enqueue(n);
                    }
                }
            }

            if (!came.ContainsKey(to))
            {
                path.Add(_places[from].At);
                path.Add(_places[to].At);
                return path;
            }

            var steps = new List<(int Place, int Road)>();
            for (var at = to; at != from; at = came[at].From)
            {
                steps.Add((at, came[at].Road));
            }

            steps.Reverse();
            path.Add(_places[from].At);
            var current = from;
            foreach (var (place, road) in steps)
            {
                var points = _roads[road];
                var forward = _roadEnds[road].A == current;
                for (var k = 1; k < points.Length; k++)
                {
                    path.Add(points[forward ? k : points.Length - 1 - k]);
                }

                current = place;
            }

            return path;
        }

        /// <summary>The point <paramref name="fraction"/> (0–1) of the way along a path.</summary>
        public static Vector2 Along(IReadOnlyList<Vector2> path, float fraction)
        {
            if (path.Count == 0)
            {
                return Vector2.zero;
            }

            var total = 0f;
            for (var k = 1; k < path.Count; k++)
            {
                total += Vector2.Distance(path[k - 1], path[k]);
            }

            var left = Mathf.Clamp01(fraction) * total;
            for (var k = 1; k < path.Count; k++)
            {
                var length = Vector2.Distance(path[k - 1], path[k]);
                if (left <= length && length > 0f)
                {
                    return Vector2.Lerp(path[k - 1], path[k], left / length);
                }

                left -= length;
            }

            return path[path.Count - 1];
        }

        private float RoadDistance(Vector2 at)
        {
            var best = float.MaxValue;
            foreach (var index in Nearby(_roadBuckets, at))
            {
                var points = _roads[index];
                for (var k = 1; k < points.Length; k++)
                {
                    best = Mathf.Min(best, SegmentDistance(at, points[k - 1], points[k]));
                }
            }

            return best;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static void Bucket(Dictionary<(int, int), List<int>> buckets, Vector2 min, Vector2 max, int index, float margin)
        {
            for (var bx = Mathf.FloorToInt((min.x - margin) / BucketSize); bx <= Mathf.FloorToInt((max.x + margin) / BucketSize); bx++)
            {
                for (var bz = Mathf.FloorToInt((min.y - margin) / BucketSize); bz <= Mathf.FloorToInt((max.y + margin) / BucketSize); bz++)
                {
                    if (!buckets.TryGetValue((bx, bz), out var list))
                    {
                        buckets[(bx, bz)] = list = new List<int>();
                    }

                    list.Add(index);
                }
            }
        }

        private static readonly List<int> Empty = new List<int>();

        private static List<int> Nearby(Dictionary<(int, int), List<int>> buckets, Vector2 at) =>
            buckets.TryGetValue((Mathf.FloorToInt(at.x / BucketSize), Mathf.FloorToInt(at.y / BucketSize)), out var list) ? list : Empty;

        // ---------------- noise ----------------

        /// <summary>0–1, from a lattice point and a salt (the world's seed mixed in).</summary>
        private float Hash01(int x, int y, uint salt)
        {
            unchecked
            {
                var h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ salt * 0xcb1ab31fu ^ _seed;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                h *= 0x27d4eb2du;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        private float Noise(float x, float y, uint salt)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            var u = fx * fx * (3f - 2f * fx);
            var v = fy * fy * (3f - 2f * fy);
            var a = Hash01(ix, iy, salt);
            var b = Hash01(ix + 1, iy, salt);
            var c = Hash01(ix, iy + 1, salt);
            var d = Hash01(ix + 1, iy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v) * 2f - 1f;
        }

        /// <summary>Fractal noise, roughly −1–1.</summary>
        private float Fbm(float x, float y, uint salt, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            var frequency = 1f;
            for (var o = 0; o < octaves; o++)
            {
                sum += Noise(x * frequency + o * 17.3f, y * frequency - o * 9.1f, salt + (uint)o * 131u) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }

            return sum / total * 1.6f;
        }
    }
}
