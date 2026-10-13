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
        private readonly List<GameObject> _roadTiles = new List<GameObject>();
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

        public void Build(IReadOnlyList<GridPosition> rocks)
        {
            _block = new MaterialPropertyBlock();
            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);
            _roadRoot = new GameObject("Road").transform;
            _roadRoot.SetParent(_root, false);

            for (var y = 0; y < VillageMap.Demo.Height; y++)
            {
                for (var x = 0; x < VillageMap.Demo.Width; x++)
                {
                    var color = (x + y) % 2 == 0 ? Palette.Ground : Palette.GroundAlt; // all mud: grass means "not your land"
                    Shapes.Create(PrimitiveType.Cube, color, new Vector3(x, -0.1f, -y), new Vector3(1f, 0.2f, 1f), _root, "Tile");
                }
            }

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

            var seen = new HashSet<int>();
            var barriers = new HashSet<GridPosition>(buildings.Where(b => BuildingTable.IsBarrier(b.Type)).SelectMany(b => b.Tiles));
            foreach (var building in buildings)
            {
                seen.Add(building.Id);
                var mask = Joins(building.Type) ? WallJoints.Mask(building.Position, barriers) : WallLink.None;
                if (!_buildings.TryGetValue(building.Id, out var visual) || visual.Level != building.Level
                    || (Joins(building.Type) && visual.Mask != mask) || visual.Anchor != building.Position || visual.Rotated != building.Rotated) // moved, turned or re-joined
                {
                    if (visual != null)
                    {
                        visual.Destroy();
                    }

                    visual = CreateBuilding(building, mask);
                    visual.Mask = mask;
                    visual.Anchor = building.Position;
                    visual.Rotated = building.Rotated;
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
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#2f3a22"), new Vector3(zone.MinX + (w - 1) / 2f, 0.02f, -(zone.MinY + (h - 1) / 2f)),
                new Vector3(w, 0.04f, h), holder.transform, "Undergrowth");
            for (var y = zone.MinY; y <= zone.MaxY; y++)
            {
                for (var x = zone.MinX; x <= zone.MaxX; x++)
                {
                    var tile = new GridPosition(x, y);
                    var seed = x * 31 + y * 17;
                    if ((x - zone.MinX) % 2 != 0 || (y - zone.MinY) % 2 != 0 || map.Rocks.Contains(tile))
                    {
                        continue;
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

            var sign = ArtLibrary.Instantiate("signpost", holder.transform);
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
                    if (map.IsInSpawnClearance(tile) && !map.Rocks.Contains(tile))
                    {
                        Shapes.Create(PrimitiveType.Cube, Palette.Mud, Shapes.ToWorld(tile, 0.004f), new Vector3(0.96f, 0.01f, 0.96f), _root, "Entrance (no building)");
                    }
                }
            }
        }

        /// <summary>The river past the south and east: water a hair above the grass, muddy banks, one plank bridge.</summary>
        private void BuildRiver()
        {
            var river = WorldScenery.RiverPath(VillageMap.Demo.Width, VillageMap.Demo.Height);
            var water = new HashSet<GridPosition>(river);
            var banks = new HashSet<GridPosition>();
            foreach (var tile in river)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var bank = new GridPosition(tile.X + dx, tile.Y + dy);
                        if (!water.Contains(bank))
                        {
                            banks.Add(bank);
                        }
                    }
                }
            }

            foreach (var bank in banks)
            {
                Shapes.Create(PrimitiveType.Cube, Palette.Mud, Shapes.ToWorld(bank, -0.115f), new Vector3(1f, 0.01f, 1f), _root, "Bank");
            }

            foreach (var tile in river)
            {
                _water.Add(Shapes.Create(PrimitiveType.Cube, Palette.Water, Shapes.ToWorld(tile, -0.11f), new Vector3(1.02f, 0.02f, 1.02f), _root, "River").transform);
            }

            var bridge = river[river.Count / 4];
            for (var k = -1; k <= 1; k++)
            {
                Shapes.Create(PrimitiveType.Cube, Palette.Wood, Shapes.ToWorld(bridge, 0f) + new Vector3(k * 0.32f, 0f, 0f), new Vector3(0.28f, 0.08f, 2.6f), _root, "Bridge plank");
            }
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

        public void ShowRoad(IReadOnlyList<GridPosition> path)
        {
            foreach (var go in _roadTiles)
            {
                Destroy(go);
            }

            _roadTiles.Clear();
            foreach (var tile in path)
            {
                _roadTiles.Add(Shapes.Create(PrimitiveType.Cube, Palette.Road, Shapes.ToWorld(tile, 0.005f), new Vector3(0.8f, 0.02f, 0.8f), _roadRoot, "Road"));
            }
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

        private void LateUpdate()
        {
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
            Shapes.Create(PrimitiveType.Cube, Palette.Grass, new Vector3((VillageMap.Demo.Width - 1) / 2f, -0.22f, -(VillageMap.Demo.Height - 1) / 2f),
                new Vector3(VillageMap.Demo.Width + 120f, 0.2f, VillageMap.Demo.Height + 120f), _root, "Apron");
            BuildRiver();
            foreach (var item in WorldScenery.WorldLayout(VillageMap.Demo.Width, VillageMap.Demo.Height))
            {
                Place(item.Model, Shapes.ToWorld(item.Tile) + item.Jitter, item.Yaw, item.Scale);
            }

            foreach (var item in WorldScenery.BorderLayout(VillageMap.Demo.Width, VillageMap.Demo.Height, VillageMap.Demo.Spawn))
            {
                Place(item.Model, Shapes.ToWorld(item.Tile) + item.Jitter, item.Yaw, item.Scale);
            }

            foreach (var tile in WorldScenery.PassTrack(VillageMap.Demo.Spawn))
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
            HearthPosition = Shapes.ToWorld(VillageMap.Demo.Hearth);
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
            var model = ArtLibrary.Instantiate($"hearth_l{Mathf.Clamp(level, 1, 3)}", _hearth);
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
            model.transform.localScale = Vector3.one * 0.8f; // fits its tile; the columns still tower over the cultists
            _hearthFire = ArtLibrary.FindPart(model.transform, "Fire");
            _hearthSteam = ArtLibrary.FindPart(model.transform, "Steam");
            return true;
        }

        private static string ModelName(BuildingType type) => type switch
        {
            BuildingType.Woodcutter => "woodcutter",
            BuildingType.PeatCut => "peat_cut",
            BuildingType.StorageHut => "storage_hut",
            BuildingType.CultistHut => "cultist_hut",
            BuildingType.Cistern => "cistern",
            BuildingType.SlurryChute => "slurry_chute",
            BuildingType.SaplingFlinger => "sapling_flinger",
            BuildingType.WattleFence => "fence",
            BuildingType.WoodenWall => "palisade",
            BuildingType.Gate => "gate_wood",
            BuildingType.DirtTrack => "road_dirt",
            BuildingType.PlankWalk => "road_planks",
            BuildingType.CobbledRoad => "road_cobbles",
            BuildingType.Cottage => "cottage",
            BuildingType.Stone => "rock_b",
            BuildingType.Tree => "oak",
            _ => null,
        };

        private static string ResourceIcon(BuildingType type) => type switch
        {
            BuildingType.Woodcutter => "timber",
            BuildingType.PeatCut => "peat",
            BuildingType.CultistHut => "devotion",
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
                    whole.transform.localRotation = building.Rotated ? Quaternion.Euler(0f, 90f, 0f) * FaceCamera : FaceCamera;
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
        public void ShowGhost(BuildingType? type, int level, IReadOnlyList<GridPosition> tiles, bool rotated, bool valid)
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

            var key = $"{type}:{level}:{rotated}";
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
                var name = type == BuildingType.Gate ? GateModel(level) : ModelName(type.Value) ?? "fence";
                var model = ArtLibrary.Instantiate(name, _ghost.transform);
                if (model != null)
                {
                    model.transform.localRotation = rotated ? Quaternion.Euler(0f, 90f, 0f) * FaceCamera : FaceCamera;
                    model.transform.localScale = Vector3.one * 0.8f * Mathf.Min(size.x, size.y);
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
                visual.Root.localPosition = Shapes.ToWorld(building.Position);
            }
            else if (building.Type == BuildingType.SlurryChute || building.Type == BuildingType.SaplingFlinger)
            {
                var tower = building.Type == BuildingType.SlurryChute ? TowerType.SlurryChute : TowerType.SaplingFlinger;
                visual.Root = CreateTower(new PlacedTower(tower, building.Position)).transform;
            }
            else
            {
                visual.Root = new GameObject(building.Type.ToString()).transform;
                visual.Root.SetParent(_root, false);
                var (centre, size) = FootprintBox(building.Tiles);
                visual.Root.localPosition = centre;
                if (BuildingTable.IsRoad(building.Type))
                {
                    // A road is the ground itself: flat, full tile, never a "building" with bars and badges.
                    var tile = ArtLibrary.Instantiate(ModelName(building.Type), visual.Root);
                    if (tile == null)
                    {
                        Shapes.Create(PrimitiveType.Cube, Palette.Road, new Vector3(0f, 0.01f, 0f), new Vector3(0.98f, 0.02f, 0.98f), visual.Root, "Road");
                    }

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
                    : ModelName(building.Type);
                var model = Joins(building.Type) ? null : ArtLibrary.Instantiate(modelName, visual.Root);
                var grow = Mathf.Min(size.x, size.y); // big footprints get a big model
                var scale = (building.Type == BuildingType.Stone ? 0.9f : building.Level == 0 ? 0.5f : 0.8f) * grow; // a half-built shell (or a sapling) until it is done
                if (model != null)
                {
                    model.transform.localRotation = building.Rotated ? Quaternion.Euler(0f, 90f, 0f) * FaceCamera : FaceCamera;
                    model.transform.localScale = Vector3.one * scale;
                }
                else if (!Joins(building.Type))
                {
                    Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.3f * scale, 0f), new Vector3(0.8f, 0.6f, 0.8f) * scale, visual.Root, "Building");
                }
            }

            visual.Bar = Shapes.Create(PrimitiveType.Cube, Palette.Highlight, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 0.07f, 0.07f), visual.Root, "Construction");
            visual.Bar.SetActive(false);
            visual.Health = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.07f, 0.07f), visual.Root, "Health");
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
            public SpriteRenderer Ready;
            public float ReadyHeight;
            public GameObject Health;
            public bool Wrecked;
            public WallLink Mask = (WallLink)(-1);
            public GridPosition Anchor;
            public bool Rotated;

            public void Destroy() => UnityEngine.Object.Destroy(Root.gameObject);
        }

        private void AddHearthLight(Transform hearth)
        {
            var lightGo = new GameObject("Hearth Glow");
            lightGo.transform.SetParent(hearth, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
            _hearthLight = lightGo.AddComponent<Light>();
            _hearthLight.type = LightType.Point;
            _hearthLight.color = Palette.Ember;
            _hearthLight.range = 4.5f;
            _hearthLight.intensity = 3.5f;
        }

        private GameObject CreateTower(PlacedTower tower)
        {
            var root = new GameObject(tower.Type.ToString()).transform;
            root.SetParent(_root, false);
            root.localPosition = Shapes.ToWorld(tower.Position);

            var model = ArtLibrary.Instantiate(tower.Type == TowerType.SlurryChute ? "slurry_chute" : "sapling_flinger", root);
            if (model != null)
            {
                model.transform.localRotation = FaceCamera;
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
