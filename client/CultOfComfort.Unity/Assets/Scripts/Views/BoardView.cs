using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Static scenery: ground, rocks, the Hearth, towers, the road preview and the tile cursor.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<int, BuildingVisual> _buildings = new Dictionary<int, BuildingVisual>();
        private readonly List<SpriteRenderer> _billboards = new List<SpriteRenderer>();
        private readonly Dictionary<MapZone, GameObject> _zones = new Dictionary<MapZone, GameObject>();
        private readonly Dictionary<GridPosition, GameObject> _rocks = new Dictionary<GridPosition, GameObject>();
        private readonly List<GameObject> _paintGhosts = new List<GameObject>();
        private GameObject _ghost;
        private string _ghostKey;
        private int? _hiddenBuilding;
        private readonly Dictionary<GridPosition, GameObject> _wildTrees = new Dictionary<GridPosition, GameObject>();
        private readonly List<Transform> _water = new List<Transform>();
        private Transform _hearth;
        private GameObject _hearthModel;
        private int _hearthLevel = 1;
        private Transform _root;
        private Transform _roadRoot;
        private GameObject _cursor;
        private GameObject _leaderMarker;
        private Light _hearthLight;
        private float _night;
        private Transform _hearthFire;
        private Transform _hearthSteam;

        /// <summary>Blender models face +Z; the board camera looks from −Z, so static buildings turn round to face it.</summary>
        private static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Color Soggy = new Color(0.44f, 0.5f, 0.53f);
        private MaterialPropertyBlock _block;

        public Vector3 HearthPosition { get; private set; }

        /// <summary>A building finished being built or upgraded (it just popped).</summary>
        public event Action LevelledUp;

        public void Build(IReadOnlyList<GridPosition> rocks)
        {
            _block = new MaterialPropertyBlock();
            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);
            _roadRoot = new GameObject("Road").transform;
            _roadRoot.SetParent(_root, false);

            BuildGround();

            foreach (var rock in rocks)
            {
                var seed = rock.X * 31 + rock.Y * 17;
                var bedrock = VillageMap.Demo.IsBedrock(rock);
                var model = Place(RockModel(VillageMap.Demo, rock), Shapes.ToWorld(rock), seed * 37 % 360, (bedrock ? 1.1f : 1.35f) + seed % 3 * 0.1f);
                if (model != null)
                {
                    _rocks[rock] = model;
                }
                if (model == null)
                {
                    var go = Shapes.Create(PrimitiveType.Sphere, Palette.Rock, Shapes.ToWorld(rock, 0.3f),
                        new Vector3(0.85f, 0.6f + seed % 3 * 0.12f, 0.8f), _root, "Rock");
                    go.transform.localRotation = Quaternion.Euler(0f, seed * 37 % 360, 8f);
                }
            }

            BuildSurroundings();
            BuildEntranceClearing();
            BuildSpawn();
            BuildHearth();

            _cursor = Shapes.Create(PrimitiveType.Cube, Palette.Highlight, Vector3.zero, new Vector3(0.95f, 0.05f, 0.95f), _root, "Cursor");
            _cursor.SetActive(false);
            _leaderMarker = Shapes.Create(PrimitiveType.Cylinder, Palette.Linen, Vector3.zero, new Vector3(0.5f, 0.01f, 0.5f), _root, "Leader Destination");
            _leaderMarker.SetActive(false);
        }

        /// <summary>
        /// Shows the village: one visual per building id, rebuilt only when its type or level changes; a construction
        /// bar over anything being built; a resource icon over producers with something to collect.
        /// </summary>
        public void SetBuildings(IReadOnlyList<BuildingView> buildings, int hearthLevel, BuildingTable table, DateTimeOffset now)
        {
            if (hearthLevel != _hearthLevel)
            {
                SwapHearthModel(hearthLevel);
            }

            UpdateRoads(buildings);
            var seen = new HashSet<int>();
            var barriers = new HashSet<GridPosition>(buildings.Where(b => BuildingTable.IsBarrier(b.Type)).SelectMany(b => b.Tiles));
            foreach (var building in buildings)
            {
                seen.Add(building.Id);
                var mask = Joins(building.Type) ? WallJoints.Mask(building.Position, barriers) : WallLink.None;
                if (!_buildings.TryGetValue(building.Id, out var visual) || visual.Level != building.Level
                    || (Joins(building.Type) && visual.Mask != mask) || visual.Anchor != building.Position || visual.Rotated != building.Rotated || visual.Flipped != building.Flipped) // moved, turned or re-joined
                {
                    var levelledUp = visual != null && building.Level > visual.Level;
                    if (visual != null)
                    {
                        visual.Destroy();
                    }

                    visual = CreateBuilding(building, mask);
                    if (levelledUp)
                    {
                        visual.Root.gameObject.AddComponent<UpgradePop>();
                        LevelledUp?.Invoke();
                    }
                    visual.Mask = mask;
                    visual.Anchor = building.Position;
                    visual.Rotated = building.Rotated;
                    visual.Flipped = building.Flipped;
                    visual.Wrecked = false;
                    _buildings[building.Id] = visual;
                }

                visual.Root.gameObject.SetActive(building.Id != _hiddenBuilding);

                var spec = table.Spec(building.Type);
                visual.Bar.SetActive(building.IsUpgrading);
                if (building.IsUpgrading)
                {
                    var total = Mathf.Max(1f, spec.Levels[building.Level].BuildSeconds);
                    var left = Mathf.Clamp((float)(building.UpgradeEndsAtUtc.Value - now).TotalSeconds, 0f, total);
                    visual.Bar.transform.localScale = new Vector3(0.7f * (1f - left / total), 0.07f, 0.07f);
                }

                // Somebody is hammering at whatever the builder works on (not saplings, not roads, not towers).
                var hammering = building.IsUpgrading && spec.UsesBuilder && !BuildingTable.IsRoad(building.Type) && !spec.IsTower;
                if (hammering && visual.Builder == null)
                {
                    var (_, size) = FootprintBox(building.Tiles);
                    var front = -size.y / 2f - 0.3f; // −z faces the camera: he stands at the front edge, the pile beside him
                    visual.Builder = BuilderAtWork.Create(visual.Root, new Vector3(size.x / 2f - 0.25f, 0f, front),
                        new Vector3(-size.x / 2f + 0.3f, 0f, front + 0.05f), Vector3.zero);
                }
                else if (!hammering && visual.Builder != null)
                {
                    Destroy(visual.Builder);
                    visual.Builder = null;
                }

                if (visual.Ready != null)
                {
                    visual.Ready.enabled = building.Ready >= 1 && !building.Wrecked;
                }

                if (visual.Wrecked != building.Wrecked)
                {
                    visual.Wrecked = building.Wrecked;
                    TintBuilding(visual, building.Wrecked);
                    visual.Health.SetActive(false);
                }
            }

            foreach (var id in _buildings.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                _buildings[id].Destroy();
                _buildings.Remove(id);
            }
        }

        /// <summary>During a Night: a health bar over a damaged building; a wreck turns it soggy at once.</summary>
        public void ShowStructureHealth(int id, float fraction, bool wrecked)
        {
            if (!_buildings.TryGetValue(id, out var visual))
            {
                return;
            }

            visual.Health.SetActive(fraction > 0f && fraction < 1f);
            visual.Health.transform.localScale = new Vector3(0.7f * Mathf.Clamp01(fraction), 0.07f, 0.07f);
            Shapes.Tint(visual.Health.GetComponent<Renderer>(), fraction > 0.4f ? Palette.HealthGood : Palette.HealthBad, _block);
            if (wrecked && !visual.Wrecked)
            {
                visual.Wrecked = true;
                TintBuilding(visual, true);
            }
        }

        /// <summary>Bedrock is a bare crag (it never moves); a loose boulder is a mossy field stone (clear it for Scrap).</summary>
        public static string RockModel(VillageMap map, GridPosition tile) =>
            map.IsBedrock(tile) ? "crag" : (tile.X * 31 + tile.Y * 17) % 2 == 0 ? "rock_a" : "rock_b";

        /// <summary>Wild trees that stand and boulders that remain; cut and cleared ones disappear, regrown ones return.</summary>
        public void SetTerrain(BaseSnapshot snapshot)
        {
            var map = VillageMap.Demo;
            var cleared = new HashSet<GridPosition>(snapshot.ClearedTerrain ?? Array.Empty<GridPosition>());
            foreach (var pair in _rocks)
            {
                var gone = map.IsBoulder(pair.Key) && (cleared.Contains(pair.Key) || snapshot.BuildingAt(pair.Key) != null);
                if (pair.Value.activeSelf == gone)
                {
                    pair.Value.SetActive(!gone);
                }
            }

            foreach (var tile in map.WildTrees)
            {
                var standing = map.IsOpen(tile, snapshot.HearthLevel) && !cleared.Contains(tile) && snapshot.BuildingAt(tile) == null;
                if (standing && !_wildTrees.ContainsKey(tile))
                {
                    var seed = tile.X * 31 + tile.Y * 17;
                    var tree = Place(seed % 3 == 0 ? "oak" : seed % 3 == 1 ? "pine" : "birch", Shapes.ToWorld(tile), seed * 37 % 360, 0.85f + seed % 3 * 0.08f);
                    if (tree != null)
                    {
                        _wildTrees[tile] = tree;
                    }
                }
                else if (!standing && _wildTrees.TryGetValue(tile, out var go))
                {
                    Destroy(go);
                    _wildTrees.Remove(tile);
                }
            }
        }

        /// <summary>Locked land is forest (one patch per zone, so the phone stays fast); opening a level clears it.</summary>
        public void SetOpenLevel(int hearthLevel)
        {
            var map = VillageMap.Demo;
            if (hearthLevel != _wantedFloorLevel)
            {
                _wantedFloorLevel = hearthLevel; // the painter lays the forest floor for it
                PaintGround();
            }

            foreach (var zone in map.Zones ?? Array.Empty<MapZone>())
            {
                var open = zone.OpensAtHearth <= hearthLevel;
                if (open && _zones.TryGetValue(zone, out var patch))
                {
                    Destroy(patch);
                    _zones.Remove(zone);
                }
                else if (!open && !_zones.ContainsKey(zone))
                {
                    _zones[zone] = CreateForestPatch(zone, map);
                }
            }
        }

        private GameObject CreateForestPatch(MapZone zone, VillageMap map)
        {
            var holder = new GameObject($"Forest (Hearth {zone.OpensAtHearth})");
            holder.transform.SetParent(_root, false);
            var w = zone.MaxX - zone.MinX + 1;
            var h = zone.MaxY - zone.MinY + 1;
            // The undergrowth is painted into the ground (a slab hid the raiders' roads through the forest).
            var roads = map.AllRoadTiles;
            bool Clear(GridPosition t) =>
                (new Vector2(t.X, t.Y) - StoneCircle).sqrMagnitude < 12f || (t.X == 9 && t.Y == 44)
                || roads.Contains(t) || map.Rocks.Contains(t) || (map.River != null && map.River(t.X, t.Y) < 0.8)
                || roads.Contains(new GridPosition(t.X + 1, t.Y)) || roads.Contains(new GridPosition(t.X - 1, t.Y))
                || roads.Contains(new GridPosition(t.X, t.Y + 1)) || roads.Contains(new GridPosition(t.X, t.Y - 1));
            for (var y = zone.MinY; y <= zone.MaxY; y++)
            {
                for (var x = zone.MinX; x <= zone.MaxX; x++)
                {
                    var tile = new GridPosition(x, y);
                    var seed = x * 31 + y * 17;
                    if ((x - zone.MinX) % 2 != 0 || (y - zone.MinY) % 2 != 0
                        || Clear(tile) || Clear(new GridPosition(x + 1, y)) || Clear(new GridPosition(x, y + 1)) || Clear(new GridPosition(x + 1, y + 1)))
                    {
                        continue; // trees stand clear of the river, its banks and the roads
                    }

                    var tree = ArtLibrary.Instantiate("deep_forest", holder.transform);
                    if (tree != null)
                    {
                        tree.transform.localPosition = Shapes.ToWorld(tile) + new Vector3(0.5f, 0f, -0.5f); // centre of the 2×2 block
                        tree.transform.localRotation = Quaternion.Euler(0f, seed * 37 % 360, 0f);
                        tree.transform.localScale = Vector3.one * (1.3f + seed % 3 * 0.08f);
                    }
                }
            }

            var sign = zone.OpensAtHearth >= VillageMap.Wild ? null : ArtLibrary.Instantiate("signpost", holder.transform); // the wild never opens: no sign
            if (sign != null)
            {
                sign.transform.localPosition = Shapes.ToWorld(new GridPosition(zone.MinX, zone.MinY));
                sign.transform.localRotation = FaceCamera;
            }

            return holder;
        }

        /// <summary>Trampled mud where the raiders come in: nobody may build there.</summary>
        private void BuildEntranceClearing()
        {
            var map = VillageMap.Demo;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var tile = new GridPosition(x, y);
                    if (map.IsInSpawnClearance(tile) && !map.Rocks.Contains(tile) && map.OpensAt(tile) < VillageMap.Wild) // in the wild nobody builds anyway
                    {
                        Shapes.Create(PrimitiveType.Cube, Palette.Mud, Shapes.ToWorld(tile, 0.004f), new Vector3(0.96f, 0.01f, 0.96f), _root, "Entrance (no building)");
                    }
                }
            }
        }

        /// <summary>
        /// The river (spec 2026-10-20 river). In the valley it is painted into the ground; past the edge of the map it
        /// runs on as water tiles to the horizon. Plank bridges carry the raiders' roads over it, reeds grow on the banks,
        /// a few rocks break the current, and a ring of standing stones waits on the far bank.
        /// </summary>
        private const int BeyondMargin = 26;
        private const int BeyondPerTile = 8;
        private Texture2D _beyondTexture;
        private System.Threading.Tasks.Task<Color32[]> _beyondPainting;

        /// <summary>
        /// The meadow beyond the map, painted (spec 2026-10-20 river): the river and its banks run on smooth past the
        /// edge instead of in tile-sized squares. One textured sheet just under the map's ground, painted off the main
        /// thread like the ground.
        /// </summary>
        private void BuildBeyond()
        {
            var map = VillageMap.Demo;
            var width = map.Width + BeyondMargin * 2;
            var height = map.Height + BeyondMargin * 2;
            _beyondTexture = new Texture2D(width * BeyondPerTile, height * BeyondPerTile, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2, name = "Beyond the map",
            };
            _beyondTexture.SetPixels32(Enumerable.Repeat((Color32)Palette.Grass, _beyondTexture.width * _beyondTexture.height).ToArray());
            _beyondTexture.Apply(true, false);
            var (x0, x1) = (-BeyondMargin - 0.5f, map.Width + BeyondMargin - 0.5f);
            var (z0, z1) = (BeyondMargin + 0.5f, -(map.Height + BeyondMargin - 0.5f));
            var mesh = new Mesh { name = "Beyond the map" };
            mesh.SetVertices(new[] { new Vector3(x0, -0.12f, z0), new Vector3(x1, -0.12f, z0), new Vector3(x0, -0.12f, z1), new Vector3(x1, -0.12f, z1) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0);
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            var sheet = new GameObject("Beyond the map");
            sheet.transform.SetParent(_root, false);
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Resources.Load<Material>("BaseLit")) { mainTexture = _beyondTexture, color = Color.white };
            material.SetFloat("_Smoothness", 0.05f);
            sheet.AddComponent<MeshRenderer>().sharedMaterial = material;
            GroundPainter.Warm();
            _beyondPainting = System.Threading.Tasks.Task.Run(() => GroundPainter.Beyond(map.Width, map.Height, BeyondMargin, BeyondPerTile, map.River));
        }

        /// <summary>Where the stone circle stands on the far bank (the forest leaves room round it).</summary>
        private static readonly Vector2 StoneCircle = new Vector2(63.5f, 28.5f);

        private void BuildRiver()
        {
            var map = VillageMap.Demo;
            if (map.River == null)
            {
                return;
            }

            // Bridges: a plank span on every road tile over the water, laid along the road.
            var spans = new HashSet<GridPosition>();
            for (var road = 0; road < map.RoadCount; road++)
            {
                var path = map.RoadAt(road);
                for (var k = 0; k < path.Count; k++)
                {
                    if (!map.Water.Contains(path[k]) || !spans.Add(path[k]))
                    {
                        continue;
                    }

                    var next = path[Math.Min(k + 1, path.Count - 1)];
                    var prev = path[Math.Max(k - 1, 0)];
                    var alongX = next.X != prev.X;
                    Place("bridge", Shapes.ToWorld(path[k], 0.02f), alongX ? 0f : 90f, 1f);
                }
            }

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var tile = new GridPosition(x, y);
                    var d = map.River(x, y);
                    var hash = (x * 73856093) ^ (y * 19349663) ^ 0x51ed270b;
                    var jitter = new Vector3((hash >> 4 & 15) / 40f - 0.19f, 0f, (hash >> 8 & 15) / 40f - 0.19f);
                    if (map.AllRoadTiles.Contains(tile))
                    {
                        continue;
                    }

                    if (d >= -0.2 && d < 0.6 && (hash & 3) == 0)
                    {
                        Place("reeds", Shapes.ToWorld(tile) + jitter, hash >> 12 & 359, 1.1f + (hash >> 20 & 3) * 0.15f);
                    }
                    else if (d < -0.9 && (hash & 31) == 7)
                    {
                        Place("rock_a", Shapes.ToWorld(tile, -0.08f) + jitter, hash >> 12 & 359, 0.55f);
                    }
                }
            }

            // The stone circle on the far bank, east of the river: seven stones, one fallen.
            var circle = new Vector3(StoneCircle.x, 0f, -StoneCircle.y);
            for (var k = 0; k < 7; k++)
            {
                var a = k * Mathf.PI * 2f / 7f;
                var stone = Place("standing_stone", circle + new Vector3(Mathf.Cos(a) * 2f, 0f, Mathf.Sin(a) * 2f), -a * Mathf.Rad2Deg + 90f, 1.3f);
                if (stone != null && k == 4)
                {
                    stone.transform.localRotation *= Quaternion.Euler(80f, 0f, 0f); // the fallen one
                }
            }

            Place("standing_stone", Shapes.ToWorld(new GridPosition(9, 44)), 20f, 1.6f); // and a lone one on the south bank
        }

        /// <summary>Hides every building's health bar (the Night is over, or a new one starts at full health).</summary>
        public void ClearStructureHealth()
        {
            foreach (var visual in _buildings.Values)
            {
                visual.Health.SetActive(false);
            }
        }

        /// <summary>Soggy grey-blue while wrecked; the palette's own colours otherwise.</summary>
        private void TintBuilding(BuildingVisual visual, bool wrecked)
        {
            foreach (var renderer in visual.Root.GetComponentsInChildren<Renderer>())
            {
                if (renderer.gameObject == visual.Bar || renderer.gameObject == visual.Health)
                {
                    continue;
                }

                if (wrecked)
                {
                    Shapes.Tint(renderer, Soggy, _block);
                }
                else
                {
                    renderer.SetPropertyBlock(null);
                }
            }
        }

        /// <summary>What the raiders' road is paved with (0: trodden mud, 1: gravel, 2: cobbles); repaints when it changes.</summary>
        public void SetRaidersRoad(int level)
        {
            if (level == _raidersRoad)
            {
                return;
            }

            _raidersRoad = level;
            PaintGround();
        }

        /// <summary>The raiders' track across the land, painted into the ground (the village's own roads paint over it).</summary>
        public void ShowRoad(IReadOnlyList<GridPosition> path)
        {
            var tiles = path?.ToList() ?? new List<GridPosition>();
            if (tiles.SequenceEqual(_raidersPath))
            {
                return; // the same roads: the ground is painted already
            }

            _raidersPath = tiles;
            PaintGround();
        }

        private GameObject _scout;
        private GameObject _reach;
        private float _reachRadius = -1f;

        /// <summary>
        /// A tower's reach (spec 2026-10-20 defence): a ring of dots round <paramref name="centre"/>, so a tower can be
        /// set back from the road and still hit it. Null takes it down.
        /// </summary>
        public void ShowReach(Vector3? centre, float radius)
        {
            if (centre == null)
            {
                if (_reach != null)
                {
                    _reach.SetActive(false);
                }

                return;
            }

            if (_reach == null || !Mathf.Approximately(radius, _reachRadius))
            {
                if (_reach != null)
                {
                    Destroy(_reach);
                }

                _reachRadius = radius;
                _reach = new GameObject("Tower reach");
                _reach.transform.SetParent(_root, false);
                var dots = Mathf.Max(16, Mathf.RoundToInt(radius * 14f));
                for (var i = 0; i < dots; i++)
                {
                    var a = i * Mathf.PI * 2f / dots;
                    Shapes.Create(PrimitiveType.Cube, Palette.Highlight, new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius),
                        new Vector3(0.09f, 0.03f, 0.09f), _reach.transform, "Dot");
                }
            }

            _reach.SetActive(true);
            _reach.transform.localPosition = centre.Value;
        }

        /// <summary>
        /// The scout's flag (spec 2026-10-20 defence): a red banner where tonight's raiders will come out of the forest;
        /// null takes it down (one road: nothing to tell).
        /// </summary>
        public void ShowScout(GridPosition? spawn)
        {
            if (spawn == null)
            {
                if (_scout != null)
                {
                    _scout.SetActive(false);
                }

                return;
            }

            if (_scout == null)
            {
                _scout = new GameObject("Scout's flag");
                _scout.transform.SetParent(_root, false);
                Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f), _scout.transform, "Pole");
                Shapes.Create(PrimitiveType.Cube, Palette.Invalid, new Vector3(0.22f, 1.05f, 0f), new Vector3(0.4f, 0.26f, 0.03f), _scout.transform, "Banner");
            }

            _scout.SetActive(true);
            _scout.transform.localPosition = Shapes.ToWorld(spawn.Value);
        }

        // ---------------- the ground (owner 2026-10-15: "make the [tiles] less visible") ----------------

        private Texture2D _groundTexture;
        private Color32[] _groundBare;
        private Color32[] _groundFloor; // the bare ground with the forest floor painted on what hasn't opened yet
        private int _floorLevel = -1;       // the level _groundFloor was painted for (painter thread only)
        private int _wantedFloorLevel = -1; // the level the board shows
        private List<GridPosition> _raidersPath = new List<GridPosition>();
        private int _raidersRoad;
        private readonly Dictionary<GridPosition, RoadSurface> _villageRoads = new Dictionary<GridPosition, RoadSurface>();
        private readonly HashSet<GridPosition> _roadJoins = new HashSet<GridPosition>();
        private string _roadSignature = string.Empty;

        /// <summary>One sheet of painted mud over the whole board: no checkerboard, no seams between tiles.</summary>
        private void BuildGround()
        {
            var width = VillageMap.Demo.Width;
            var height = VillageMap.Demo.Height;
            GroundPainter.Warm(); // its colours are parsed here, on the main thread; the painting runs on another
            _groundTexture = new Texture2D(width * GroundPainter.PixelsPerTile, height * GroundPainter.PixelsPerTile, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "Village ground",
            };
            var mesh = new Mesh { name = "Village ground" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(width - 0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, -(height - 0.5f)), new Vector3(width - 0.5f, 0f, -(height - 0.5f)),
            });
            // Tile row 0 (the board's top edge, z = 0.5) is the texture's first row.
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0);
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            var mud = Enumerable.Repeat((Color32)Palette.Ground, _groundTexture.width * _groundTexture.height).ToArray();
            _groundTexture.SetPixels32(mud); // plain mud until the painted ground is ready (a moment)
            _groundTexture.Apply(true, false);
            var ground = new GameObject("Ground");
            ground.transform.SetParent(_root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Resources.Load<Material>("BaseLit")) { mainTexture = _groundTexture, color = Color.white };
            material.SetFloat("_Smoothness", 0.05f);
            ground.AddComponent<MeshRenderer>().sharedMaterial = material;
            // A thin skirt below, so the board's edge has some depth from a low camera.
            Shapes.Create(PrimitiveType.Cube, Palette.Ground, new Vector3((width - 1) / 2f, -0.11f, -(height - 1) / 2f), new Vector3(width, 0.2f, height), _root, "Ground skirt");
            PaintGround();
        }

        private System.Threading.Tasks.Task<Color32[]> _painting;
        private bool _paintAgain;

        /// <summary>Whether the painted ground is on the board and nothing is being repainted (captures wait for it).</summary>
        public bool GroundReady => _painting == null && !_paintAgain && _groundBare != null && _beyondPainting == null;

        /// <summary>
        /// Repaints the ground: the mud and the river once, the forest floor when the Hearth opens land, the roads each
        /// time. The painting runs off the main thread (a second or more on a phone); the newest request wins, and the
        /// texture takes the result in <see cref="Update"/>.
        /// </summary>
        private void PaintGround()
        {
            if (_groundTexture == null)
            {
                return;
            }

            if (_painting != null)
            {
                _paintAgain = true;
                return;
            }

            var map = VillageMap.Demo;
            var roads = new Dictionary<GridPosition, RoadSurface>();
            foreach (var tile in _raidersPath)
            {
                roads[tile] = _raidersRoad >= 2 ? RoadSurface.RaidersCobbles : _raidersRoad == 1 ? RoadSurface.Gravel : RoadSurface.RaidersTrack;
            }

            foreach (var (tile, surface) in _villageRoads)
            {
                roads[tile] = surface;
            }

            var joins = new HashSet<GridPosition>(_roadJoins);
            var level = _wantedFloorLevel;
            _painting = System.Threading.Tasks.Task.Run(() =>
            {
                _groundBare ??= GroundPainter.Ground(map.Width, map.Height, map.River);
                if (level != _floorLevel)
                {
                    var open = map.AllRoadTiles;
                    _groundFloor = level < 0 ? null : GroundPainter.WithForestFloor(_groundBare, map.Width, map.Height,
                        t => !map.IsOpen(t, level) && !open.Contains(t) && (map.River == null || map.River(t.X, t.Y) > 0.6));
                    _floorLevel = level;
                }

                return GroundPainter.WithRoads(_groundFloor ?? _groundBare, map.Width, map.Height, roads, joins, map.River);
            });
        }

        private void Update()
        {
            if (_beyondPainting != null && _beyondPainting.IsCompleted)
            {
                if (_beyondPainting.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    _beyondTexture.SetPixels32(_beyondPainting.Result);
                    _beyondTexture.Apply(true, false);
                }

                _beyondPainting = null;
            }

            if (_painting == null || !_painting.IsCompleted)
            {
                return;
            }

            if (_painting.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
            {
                // Our rows run top to bottom; a texture's run bottom to top, and the mesh's UVs already account for it.
                _groundTexture.SetPixels32(_painting.Result);
                _groundTexture.Apply(true, false);
            }
            else
            {
                Debug.LogException(_painting.Exception);
            }

            _painting = null;
            if (_paintAgain)
            {
                _paintAgain = false;
                PaintGround();
            }
        }

        /// <summary>The village's own roads (and what they join: the Hearth, gates) from the buildings; repaints when they change.</summary>
        private void UpdateRoads(IReadOnlyList<BuildingView> buildings)
        {
            var roads = buildings.Where(b => BuildingTable.IsRoad(b.Type)).OrderBy(b => b.Position.X).ThenBy(b => b.Position.Y).ToList();
            var joins = buildings.Where(b => b.Type == BuildingType.Hearth || b.Type == BuildingType.Gate).SelectMany(b => b.Tiles).ToList();
            var signature = string.Join(";", roads.Select(r => $"{r.Position.X},{r.Position.Y},{(int)r.Type}")) + "|" + string.Join(";", joins.Select(t => $"{t.X},{t.Y}"));
            if (signature == _roadSignature)
            {
                return;
            }

            _roadSignature = signature;
            _villageRoads.Clear();
            foreach (var road in roads)
            {
                _villageRoads[road.Position] = road.Type switch
                {
                    BuildingType.PlankWalk => RoadSurface.Planks,
                    BuildingType.CobbledRoad => RoadSurface.Cobbles,
                    _ => RoadSurface.Dirt,
                };
            }

            _roadJoins.Clear();
            _roadJoins.UnionWith(joins);
            PaintGround();
        }

        public void ShowCursor(GridPosition? tile, bool valid) =>
            ShowFootprintCursor(tile.HasValue ? new[] { tile.Value } : null, valid);

        /// <summary>The placement cursor covers the whole footprint of what is being placed.</summary>
        public void ShowFootprintCursor(IReadOnlyList<GridPosition> tiles, bool valid)
        {
            _cursor.SetActive(tiles != null && tiles.Count > 0);
            if (tiles != null && tiles.Count > 0)
            {
                var (centre, size) = FootprintBox(tiles);
                _cursor.transform.localPosition = centre + new Vector3(0f, 0.03f, 0f);
                _cursor.transform.localScale = new Vector3(size.x - 0.05f, 0.05f, size.y - 0.05f);
                _cursor.GetComponent<Renderer>().sharedMaterial = Shapes.Material(valid ? Palette.Highlight : Palette.Invalid);
            }
        }

        /// <summary>Ghost tiles along a road stroke while the finger is still down.</summary>
        public void ShowPaintPreview(IReadOnlyList<GridPosition> tiles, Func<GridPosition, bool> valid)
        {
            var count = tiles?.Count ?? 0;
            while (_paintGhosts.Count < count)
            {
                _paintGhosts.Add(Shapes.Create(PrimitiveType.Cube, Palette.Highlight, Vector3.zero, new Vector3(0.9f, 0.05f, 0.9f), _root, "Paint"));
            }

            for (var i = 0; i < _paintGhosts.Count; i++)
            {
                var ghost = _paintGhosts[i];
                ghost.SetActive(i < count);
                if (i < count)
                {
                    ghost.transform.localPosition = Shapes.ToWorld(tiles[i], 0.03f);
                    ghost.GetComponent<Renderer>().sharedMaterial = Shapes.Material(valid(tiles[i]) ? Palette.Highlight : Palette.Invalid);
                }
            }
        }

        /// <summary>Centre (world, on the ground) and size (x, z in tiles) of a set of tiles.</summary>
        public static (Vector3 Centre, Vector2 Size) FootprintBox(IReadOnlyList<GridPosition> tiles)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var t in tiles)
            {
                minX = Mathf.Min(minX, t.X);
                minY = Mathf.Min(minY, t.Y);
                maxX = Mathf.Max(maxX, t.X);
                maxY = Mathf.Max(maxY, t.Y);
            }

            return (new Vector3((minX + maxX) / 2f, 0f, -(minY + maxY) / 2f), new Vector2(maxX - minX + 1, maxY - minY + 1));
        }

        public void ShowLeaderDestination(GridPosition? tile)
        {
            _leaderMarker.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                _leaderMarker.transform.localPosition = Shapes.ToWorld(tile.Value, 0.03f);
            }
        }

        private GameObject _goldenDuck;

        /// <summary>The week's prizes on show (spec 2026-10-15 daily errands): the golden duck sits on the Hearth's ruin.</summary>
        public void SetCosmetics(IReadOnlyList<string> cosmetics)
        {
            var duck = cosmetics != null && cosmetics.Contains("golden-duck");
            if (duck == (_goldenDuck != null))
            {
                return;
            }

            if (!duck)
            {
                Destroy(_goldenDuck);
                _goldenDuck = null;
                return;
            }

            var at = Shapes.ToWorld(new GridPosition(VillageMap.Demo.Hearth.X + 2, VillageMap.Demo.Hearth.Y + 2), 0.05f);
            _goldenDuck = Place("rubber_duck", at, 200f, 1.6f);
            if (_goldenDuck == null)
            {
                _goldenDuck = Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#e8c547"), at + Vector3.up * 0.2f, Vector3.one * 0.4f, _root, "Golden duck");
                return;
            }

            _goldenDuck.name = "Golden duck";
            var gold = new MaterialPropertyBlock();
            foreach (var renderer in _goldenDuck.GetComponentsInChildren<Renderer>())
            {
                Shapes.Tint(renderer, Shapes.Hex("#ffd34d"), gold);
            }
        }

        /// <summary>
        /// Gates open for friends (owner 2026-10-15: "the gate should open when prophet or other of our allies are coming
        /// through"): the leaves of <paramref name="open"/> swing back (the gatehouse lifts its portcullis); the rest close.
        /// </summary>
        public void SetGatesOpen(ICollection<int> open)
        {
            foreach (var (id, visual) in _buildings)
            {
                if (visual.Type == BuildingType.Gate)
                {
                    visual.WantOpen = open.Contains(id);
                }
            }
        }

        private void SwingGates()
        {
            foreach (var visual in _buildings.Values)
            {
                if (visual.Type != BuildingType.Gate || (visual.Open <= 0f && !visual.WantOpen))
                {
                    continue;
                }

                if (!visual.PartsFound || (visual.DoorL == null && visual.Portcullis == null))
                {
                    visual.PartsFound = true;
                    visual.DoorL = ArtLibrary.FindPart(visual.Root, "DoorL");
                    visual.DoorR = ArtLibrary.FindPart(visual.Root, "DoorR");
                    visual.Portcullis = ArtLibrary.FindPart(visual.Root, "Portcullis");
                    visual.DoorLRest = visual.DoorL != null ? visual.DoorL.localRotation : Quaternion.identity;
                    visual.DoorRRest = visual.DoorR != null ? visual.DoorR.localRotation : Quaternion.identity;
                    visual.PortcullisRest = visual.Portcullis != null ? visual.Portcullis.localPosition : Vector3.zero;
                }

                visual.Open = Mathf.MoveTowards(visual.Open, visual.WantOpen ? 1f : 0f, Time.deltaTime * 2.5f);
                var eased = Mathf.SmoothStep(0f, 1f, visual.Open);
                // Swing about the world's up, whatever axes the model's parts came in with.
                if (visual.DoorL != null)
                {
                    var up = visual.DoorL.parent.InverseTransformDirection(Vector3.up);
                    visual.DoorL.localRotation = Quaternion.AngleAxis(-100f * eased, up) * visual.DoorLRest;
                }

                if (visual.DoorR != null)
                {
                    var up = visual.DoorR.parent.InverseTransformDirection(Vector3.up);
                    visual.DoorR.localRotation = Quaternion.AngleAxis(100f * eased, up) * visual.DoorRRest;
                }

                if (visual.Portcullis != null)
                {
                    var up = visual.Portcullis.parent.InverseTransformDirection(Vector3.up).normalized;
                    visual.Portcullis.localPosition = visual.PortcullisRest + up * 0.8f * eased;
                }
            }
        }

        private void LateUpdate()
        {
            SwingGates();
            // The river ripples: a slow wave travelling downstream.
            for (var i = 0; i < _water.Count; i++)
            {
                var p = _water[i].localPosition;
                p.y = -0.11f + Mathf.Sin(Time.time * 1.6f - i * 0.45f) * 0.012f;
                _water[i].localPosition = p;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // Collect icons face the camera and bob gently.
            var bob = Mathf.Sin(Time.time * 3f) * 0.06f;
            foreach (var visual in _buildings.Values)
            {
                if (visual.Ready != null && visual.Ready.enabled)
                {
                    visual.Ready.transform.rotation = camera.transform.rotation;
                    visual.Ready.transform.localPosition = new Vector3(0f, visual.ReadyHeight + bob, 0f);
                }
            }
        }

        /// <summary>0 = day, 1 = full night: the Hearth's glow reaches further in the dark.</summary>
        public void SetNight(float darkness) => _night = Mathf.Clamp01(darkness);

        public void SetHearthWarmth(float fraction)
        {
            var flicker = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
            _hearthLight.intensity = Mathf.Lerp(0.2f, 3.5f, fraction) * flicker * Mathf.Lerp(1f, 1.5f, _night);
            _hearthLight.range = Mathf.Lerp(4.5f, 7.5f, _night); // at night the fire is what lights the village
            if (_hearthFire != null)
            {
                // The brazier shrinks to embers as the Hearth goes cold.
                _hearthFire.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, fraction) * (1f + Mathf.Sin(Time.time * 11f) * 0.08f);
            }

            if (_hearthSteam != null)
            {
                _hearthSteam.localScale = Vector3.one * (0.9f + Mathf.Sin(Time.time * 1.3f) * 0.1f);
            }
        }

        /// <summary>Instantiates a pipeline model on the board, or returns null so the caller can fall back.</summary>
        private GameObject Place(string model, Vector3 position, float yaw, float scale = 1f)
        {
            var go = ArtLibrary.Instantiate(model, _root);
            if (go != null)
            {
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * scale;
            }

            return go;
        }

        /// <summary>
        /// A grass apron and a deterministic scatter of trees and bushes around the board, outside the playable
        /// tiles: scenery only, never in the way of a road or a tap.
        /// </summary>
        private void BuildSurroundings()
        {
            if (!ArtLibrary.Exists("pine"))
            {
                return; // no nature models yet
            }

            // The ground runs to the horizon (the haze hides the end of it): no blackness when zooming out.
            Shapes.Create(PrimitiveType.Cube, Palette.Grass, new Vector3((VillageMap.Demo.Width - 1) / 2f, -0.32f, -(VillageMap.Demo.Height - 1) / 2f),
                new Vector3(VillageMap.Demo.Width + 120f, 0.2f, VillageMap.Demo.Height + 120f), _root, "Apron");
            BuildBeyond();
            BuildRiver();
            foreach (var item in WorldScenery.WorldLayout(VillageMap.Demo))
            {
                Place(item.Model, Shapes.ToWorld(item.Tile) + item.Jitter, item.Yaw, item.Scale);
            }

            foreach (var item in WorldScenery.BorderLayout(VillageMap.Demo))
            {
                Place(item.Model, Shapes.ToWorld(item.Tile) + item.Jitter, item.Yaw, item.Scale);
            }

            foreach (var tile in WorldScenery.PassTracks(VillageMap.Demo))
            {
                Shapes.Create(PrimitiveType.Cube, Palette.Mud, Shapes.ToWorld(tile, -0.115f), new Vector3(1f, 0.01f, 0.9f), _root, "Raiders' track");
            }
        }

        private void BuildSpawn()
        {
            var at = Shapes.ToWorld(VillageMap.Demo.Spawn);
            if (Place("spawn_gate", at, 90f, 1.2f) != null)
            {
                return; // the banner spans the road, which leaves the spawn heading +X
            }

            Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, at + new Vector3(0f, 0.5f, 0.35f), new Vector3(0.12f, 0.5f, 0.12f), _root, "Gate Post");
            Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, at + new Vector3(0f, 0.5f, -0.35f), new Vector3(0.12f, 0.5f, 0.12f), _root, "Gate Post");
            Shapes.Create(PrimitiveType.Cube, Palette.Wood, at + new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 0.1f, 0.9f), _root, "Gate Beam");
        }

        private void BuildHearth()
        {
            HearthPosition = FootprintBox(VillageMap.Demo.HearthTiles).Centre; // the 4×4 ruin; the fire burns in its middle
            var hearth = new GameObject("Hearth").transform;
            hearth.SetParent(_root, false);
            hearth.localPosition = HearthPosition;
            _hearth = hearth;
            if (SwapHearthModel(1))
            {
                AddHearthLight(hearth);
                return;
            }

            Shapes.Create(PrimitiveType.Cube, Palette.Hearth, new Vector3(0f, 0.35f, 0f), new Vector3(0.85f, 0.7f, 0.85f), hearth, "Bathhouse");
            var roof = Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#4a3c30"), new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.7f, 0.7f), hearth, "Roof");
            roof.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            roof.transform.localScale = new Vector3(0.95f, 0.5f, 0.5f);
            Shapes.Create(PrimitiveType.Cube, Palette.Ember, new Vector3(0f, 0.25f, -0.43f), new Vector3(0.3f, 0.35f, 0.02f), hearth, "Glowing Door");
            Shapes.Create(PrimitiveType.Cylinder, Palette.Rock, new Vector3(0.25f, 1.1f, 0.1f), new Vector3(0.14f, 0.25f, 0.14f), hearth, "Chimney");
            AddHearthLight(hearth);
        }

        /// <summary>The Hearth grows by accretion: level 1 ruin, level 2 banya, level 3 upper floor.</summary>
        private bool SwapHearthModel(int level)
        {
            var model = ArtLibrary.Instantiate($"hearth_l{Mathf.Clamp(level, 1, 4)}", _hearth);
            if (model == null)
            {
                return false;
            }

            if (_hearthModel != null)
            {
                Destroy(_hearthModel);
            }

            _hearthModel = model;
            _hearthLevel = level;
            model.transform.localRotation = FaceCamera;
            model.transform.localScale = Vector3.one; // authored 1 unit = 1 tile, to fill its 4×4 plot
            _hearthFire = ArtLibrary.FindPart(model.transform, "Fire");
            _hearthSteam = ArtLibrary.FindPart(model.transform, "Steam");
            return true;
        }

        private static string ResourceIcon(BuildingType type) => type switch
        {
            BuildingType.Woodcutter => "timber",
            BuildingType.PeatCut => "peat",
            BuildingType.CultistHut => "devotion",
            BuildingType.MiddenSorter => "scrap",
            BuildingType.Bathhouse => "devotion",
            _ => null,
        };

        /// <summary>Fences and walls draw themselves from their neighbours (gates stand alone).</summary>
        private static bool Joins(BuildingType type) => type == BuildingType.WattleFence || type == BuildingType.WoodenWall;

        private static string BarrierModel(BuildingType type, int level) =>
            type == BuildingType.WattleFence ? "fence" : level >= 2 ? "stone_wall" : "palisade";

        private static string GateModel(int level) => level >= 3 ? "gatehouse" : level == 2 ? "gate_iron" : "gate_wood";

        /// <summary>A post in the middle and a half-length segment towards every joined neighbour; a lone piece is a full segment.</summary>
        private void BuildJoined(Transform root, BuildingView building, WallLink mask)
        {
            var model = BarrierModel(building.Type, building.Level);
            if (mask == WallLink.None)
            {
                var whole = ArtLibrary.Instantiate(model, root);
                if (whole != null)
                {
                    whole.transform.localRotation = Quaternion.Euler(0f, Facings.Yaw(building.Rotated, building.Flipped), 0f) * FaceCamera;
                }

                return;
            }

            foreach (var (link, offset, yaw) in new[]
            {
                (WallLink.East, new Vector3(0.25f, 0f, 0f), 0f), (WallLink.West, new Vector3(-0.25f, 0f, 0f), 0f),
                (WallLink.North, new Vector3(0f, 0f, 0.25f), 90f), (WallLink.South, new Vector3(0f, 0f, -0.25f), 90f),
            })
            {
                if ((mask & link) == 0)
                {
                    continue;
                }

                var half = ArtLibrary.Instantiate(model, root);
                if (half != null)
                {
                    half.transform.localPosition = offset;
                    half.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * FaceCamera;
                    half.transform.localScale = new Vector3(0.52f, 1f, 1f);
                }
            }

            var stone = building.Type == BuildingType.WoodenWall && building.Level >= 2;
            var height = building.Type == BuildingType.WattleFence ? 0.5f : stone ? 0.72f : 1f;
            Shapes.Create(PrimitiveType.Cylinder, stone ? Palette.Rock : Palette.Wood, new Vector3(0f, height / 2f, 0f),
                new Vector3(stone ? 0.3f : 0.14f, height / 2f, stone ? 0.3f : 0.14f), root, "Post");
        }

        /// <summary>The see-through-in-spirit ghost of what is being placed or moved (null type: none).</summary>
        public void ShowGhost(BuildingType? type, int level, IReadOnlyList<GridPosition> tiles, bool rotated, bool valid, bool flipped = false)
        {
            if (type == null)
            {
                if (_ghost != null)
                {
                    Destroy(_ghost);
                    _ghost = null;
                }

                ShowCursor(null, false);
                return;
            }

            var key = $"{type}:{level}:{rotated}:{flipped}";
            if (_ghost == null || _ghostKey != key)
            {
                if (_ghost != null)
                {
                    Destroy(_ghost);
                }

                _ghostKey = key;
                _ghost = new GameObject("Ghost");
                _ghost.transform.SetParent(_root, false);
                var (_, size) = FootprintBox(tiles);
                var name = type == BuildingType.Gate ? GateModel(level) : BuildingArt.ModelName(type.Value, level, ArtLibrary.Exists) ?? "fence";
                var model = ArtLibrary.Instantiate(name, _ghost.transform);
                if (model != null)
                {
                    model.transform.localRotation = Quaternion.Euler(0f, Facings.Yaw(rotated, flipped), 0f) * FaceCamera;
                    BuildingArt.Fit(model, _ghost.transform, new Vector2(size.x, size.y), finished: true);
                }
            }

            _ghost.transform.localPosition = FootprintBox(tiles).Centre + new Vector3(0f, 0.05f, 0f);
            ShowFootprintCursor(tiles, valid);
        }

        /// <summary>The building being moved hides; its ghost stands in for it.</summary>
        public void HideBuilding(int? id) => _hiddenBuilding = id;

        private BuildingVisual CreateBuilding(BuildingView building, WallLink mask)
        {
            var visual = new BuildingVisual { Level = building.Level, Type = building.Type };
            if (building.Type == BuildingType.Hearth)
            {
                visual.Root = new GameObject("Hearth works").transform;
                visual.Root.SetParent(_root, false);
                visual.Root.localPosition = FootprintBox(building.Tiles).Centre;
            }
            else if (BuildingTable.Default.Spec(building.Type).Tower is { } tower)
            {
                visual.Root = CreateTower(new PlacedTower(tower, building.Position), Facings.Yaw(building.Rotated, building.Flipped)).transform;
            }
            else
            {
                visual.Root = new GameObject(building.Type.ToString()).transform;
                visual.Root.SetParent(_root, false);
                var (centre, size) = FootprintBox(building.Tiles);
                visual.Root.localPosition = centre;
                if (BuildingTable.IsRoad(building.Type))
                {
                    // A road is the ground itself: painted into it (GroundPainter), joined to its neighbours, never a "building".

                    visual.Bar = new GameObject("No bar");
                    visual.Bar.transform.SetParent(visual.Root, false);
                    visual.Health = new GameObject("No health");
                    visual.Health.transform.SetParent(visual.Root, false);
                    visual.Bar.SetActive(false);
                    visual.Health.SetActive(false);
                    return visual;
                }

                if (Joins(building.Type))
                {
                    BuildJoined(visual.Root, building, mask);
                }
                else if (size.x > 1f || size.y > 1f)
                {
                    // A trodden plot the size of the footprint, so a 2×4 trench reads as 2×4.
                    Shapes.Create(PrimitiveType.Cube, Palette.Road, new Vector3(0f, 0.012f, 0f), new Vector3(size.x - 0.12f, 0.02f, size.y - 0.12f), visual.Root, "Plot");
                }

                var modelName = building.Type == BuildingType.Tree && building.Level == 0 ? "bush"
                    : building.Type == BuildingType.Gate ? GateModel(building.Level)
                    : BuildingArt.ModelName(building.Type, building.Level, ArtLibrary.Exists);
                var model = Joins(building.Type) ? null : ArtLibrary.Instantiate(modelName, visual.Root);
                var scale = (building.Level == 0 ? BuildingArt.ShellScalePerTile : BuildingArt.ScalePerTile) * Mathf.Min(size.x, size.y);
                if (model != null)
                {
                    model.transform.localRotation = Quaternion.Euler(0f, Facings.Yaw(building.Rotated, building.Flipped), 0f) * FaceCamera;
                    if (building.Type == BuildingType.Stone || building.Type == BuildingType.Tree)
                    {
                        // Nature ignores plot lines: a crown may spread over its neighbours.
                        model.transform.localScale = Vector3.one * (building.Type == BuildingType.Stone ? 0.9f : building.Level == 0 ? 0.5f : 0.8f) * Mathf.Min(size.x, size.y);
                    }
                    else
                    {
                        BuildingArt.Fit(model, visual.Root, new Vector2(size.x, size.y), building.Level > 0);
                    }
                }
                else if (!Joins(building.Type))
                {
                    Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.3f * scale, 0f), new Vector3(0.8f, 0.6f, 0.8f) * scale, visual.Root, "Building");
                }
            }

            var barHeight = building.Type == BuildingType.Hearth ? 3f : 1.5f; // over the ruin's columns
            visual.Bar = Shapes.Create(PrimitiveType.Cube, Palette.Highlight, new Vector3(0f, barHeight, 0f), new Vector3(0.7f, 0.07f, 0.07f), visual.Root, "Construction");
            visual.Bar.SetActive(false);
            visual.Health = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, barHeight - 0.2f, 0f), new Vector3(0.7f, 0.07f, 0.07f), visual.Root, "Health");
            visual.Health.SetActive(false);
            var icon = ResourceIcon(building.Type);
            if (icon != null && building.Level > 0 && IconSprites.Get(icon) != null)
            {
                visual.Ready = new GameObject("Ready").AddComponent<SpriteRenderer>();
                visual.Ready.sprite = IconSprites.Get(icon);
                visual.Ready.sortingOrder = 10;
                visual.Ready.transform.SetParent(visual.Root, false);
                visual.ReadyHeight = 1.3f;
                visual.Ready.enabled = false;
            }

            return visual;
        }

        private sealed class BuildingVisual
        {
            public Transform Root;
            public BuildingType Type;
            public int Level;
            public GameObject Bar;
            public GameObject Builder;
            public SpriteRenderer Ready;
            public float ReadyHeight;
            public GameObject Health;
            public bool Wrecked;
            public WallLink Mask = (WallLink)(-1);
            public GridPosition Anchor;
            public bool Rotated;
            public bool Flipped;

            /// <summary>A gate's moving parts (found on first use) and how open it is, 0–1.</summary>
            public Transform DoorL, DoorR, Portcullis;
            public Quaternion DoorLRest, DoorRRest;
            public Vector3 PortcullisRest;
            public bool PartsFound;
            public float Open;
            public bool WantOpen;

            public void Destroy() => UnityEngine.Object.Destroy(Root.gameObject);
        }

        private void AddHearthLight(Transform hearth)
        {
            var lightGo = new GameObject("Hearth Glow");
            lightGo.transform.SetParent(hearth, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, -0.3f);
            _hearthLight = lightGo.AddComponent<Light>();
            _hearthLight.type = LightType.Point;
            _hearthLight.color = Palette.Ember;
            _hearthLight.range = 4.5f;
            _hearthLight.intensity = 3.5f;
        }

        private GameObject CreateTower(PlacedTower tower, float yaw)
        {
            var root = new GameObject(tower.Type.ToString()).transform;
            root.SetParent(_root, false);
            root.localPosition = Shapes.ToWorld(tower.Position);

            var model = ArtLibrary.Instantiate(tower.Type switch
            {
                TowerType.SlurryChute => "slurry_chute",
                TowerType.PressureSluice => "pressure_sluice",
                _ => "sapling_flinger",
            }, root);
            if (model != null)
            {
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * FaceCamera;
                return root.gameObject;
            }

            if (tower.Type == TowerType.SlurryChute)
            {
                for (var i = 0; i < 4; i++)
                {
                    var x = i % 2 == 0 ? -0.25f : 0.25f;
                    var z = i < 2 ? -0.25f : 0.25f;
                    Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, new Vector3(x, 0.35f, z), new Vector3(0.07f, 0.35f, 0.07f), root, "Stilt");
                }

                Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 0.35f, 0.75f), root, "Hopper");
                Shapes.Create(PrimitiveType.Cube, Palette.Mud, new Vector3(0f, 1.03f, 0f), new Vector3(0.65f, 0.02f, 0.65f), root, "Slurry");
            }
            else
            {
                Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.16f, 0.4f), root, "Base");
                var sapling = Shapes.Create(PrimitiveType.Capsule, Palette.Sapling, new Vector3(-0.1f, 0.6f, 0f), new Vector3(0.12f, 0.5f, 0.12f), root, "Sapling");
                sapling.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                Shapes.Create(PrimitiveType.Sphere, Palette.Stone, new Vector3(-0.32f, 1.05f, 0f), Vector3.one * 0.2f, root, "Rock");
            }

            return root.gameObject;
        }
    }
}
