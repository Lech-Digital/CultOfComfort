using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using UnityEngine;
using UnityEngine.Rendering;

namespace CultOfComfort.Client
{
    /// <summary>A rival cult's camp on the 3D map.</summary>
    public sealed class StageCamp
    {
        public int Id;
        public string Key;
        public int Q;
        public int R;
        public Color Colour;
    }

    /// <summary>A missionary on the 3D map: where they're going and how far along they are.</summary>
    public sealed class StageWalker
    {
        public int NodeId;
        public MissionaryPhase Phase;
        public float Fraction;
    }

    /// <summary>Who holds a place's faith, for the rings on the ground when the map is zoomed far out.</summary>
    public sealed class StageFaith
    {
        public int Id;
        public float Mine;
        public float Rival;
        public Color RivalColour;
    }

    /// <summary>
    /// The world map in 3D (owner 2026-10-15: "more of a real map, like in kingshot"): the land of <see cref="WorldLand"/>
    /// streamed in chunks round the camera, every place a little settlement, your village with its Hearth, rival
    /// cults' camps under their banners, and missionaries walking the tracks. It lives far off to the side of the
    /// village and has its own camera; <see cref="Open"/> swaps it in.
    /// </summary>
    public sealed class WorldStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 0f, 0f);
        private const float ModelScale = 0.5f;
        private const float PickPixels = 46f;
        private static readonly Color Haze = Shapes.Hex("#b8a47a");
        private static readonly Color WaterColour = Shapes.Hex("#5f8f99");

        private Camera _camera;
        private Camera _main;
        private MapCamera _rig;
        private WorldLand _land;
        private Transform _water;
        private Transform _overlay;
        private Transform _selection;
        private Material _groundBase;
        private readonly Dictionary<(int, int), Chunk> _chunks = new Dictionary<(int, int), Chunk>();
        private readonly Dictionary<(int, int), Task<ChunkData>> _building = new Dictionary<(int, int), Task<ChunkData>>();
        private readonly Dictionary<(int, int), Task<Merged>> _remerge = new Dictionary<(int, int), Task<Merged>>();
        private readonly Dictionary<string, PropMesh[]> _propMeshes = new Dictionary<string, PropMesh[]>();
        private readonly Dictionary<string, GameObject> _camps = new Dictionary<string, GameObject>();
        private readonly List<StageCamp> _campList = new List<StageCamp>();
        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Dictionary<int, Vector3> _anchors = new Dictionary<int, Vector3>();
        private GameObject _home;
        private int _homeLevel = -1;
        private readonly List<StageFaith> _faith = new List<StageFaith>();
        private readonly Dictionary<int, (GameObject Mine, GameObject Rival)> _rings = new Dictionary<int, (GameObject, GameObject)>();
        private Transform _ringRoot;
        private float _ringSync;
        private static Mesh _annulus;
        private int _seatId = -1;
        private float _tension;
        private bool _raidComing;
        private Transform _warBanner;
        private readonly List<Marcher> _patrols = new List<Marcher>();
        private readonly List<Marcher> _column = new List<Marcher>();
        private readonly List<Marcher> _carts = new List<Marcher>();
        private Transform _raidTrail;
        private readonly List<GameObject> _raidDots = new List<GameObject>();
        private List<Vector2> _warPath = new List<Vector2>();
        private int _selected = int.MinValue;

        public MapCamera Rig => _rig;

        public WorldLand Land => _land;

        public bool IsOpen => gameObject.activeSelf;

        public Camera Camera => _camera;

        /// <summary>The whole world small, for the minimap (null until it's been drawn, a moment after opening).</summary>
        public Texture2D Minimap { get; private set; }

        /// <summary>How far the land runs from its centre (the minimap covers ±this).</summary>
        public float Bound => _land == null ? 1f : (_land.Radius + 1.5f) * WorldLand.HexSize * Mathf.Sqrt(3f);

        private Task<Color32[]> _minimapTask;
        private const int MinimapSize = 128;

        public static WorldStage Create()
        {
            var go = new GameObject("World map");
            go.transform.position = Origin;
            var stage = go.AddComponent<WorldStage>();
            stage.Build();
            go.SetActive(false);
            return stage;
        }

        private void Build()
        {
            var cameraObject = new GameObject("Map camera");
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 220f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Haze;
            _camera.depth = 5f;
            _rig = new MapCamera(_camera, Origin);

            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(water.GetComponent<Collider>());
            water.name = "Water";
            water.transform.SetParent(transform, false);
            water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.localScale = new Vector3(400f, 400f, 1f);
            var waterMaterial = new Material(Shapes.Material(WaterColour));
            waterMaterial.SetFloat("_Smoothness", 0.75f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _water = water.transform;

            _overlay = new GameObject("Overlay").transform;
            _overlay.SetParent(transform, false);
            _selection = Shapes.Create(PrimitiveType.Cylinder, Palette.Highlight, Vector3.zero, new Vector3(2.3f, 0.02f, 2.3f), transform, "Selection").transform;
            _selection.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _selection.gameObject.SetActive(false);
            _groundBase = Resources.Load<Material>("BaseLit");
            _ringRoot = new GameObject("Faith rings").transform;
            _ringRoot.SetParent(transform, false);
            _warBanner = new GameObject("War banner").transform;
            _warBanner.SetParent(transform, false);
            Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, new Vector3(0f, 1f, 0f), new Vector3(0.06f, 1f, 0.06f), _warBanner, "Pole");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#8a2f24"), new Vector3(0.32f, 1.7f, 0f), new Vector3(0.6f, 0.42f, 0.03f), _warBanner, "Flag");
            Shapes.Create(PrimitiveType.Cube, Palette.Ember, new Vector3(0.32f, 1.7f, -0.02f), new Vector3(0.2f, 0.2f, 0.02f), _warBanner, "Device");
            _warBanner.gameObject.SetActive(false);
            _raidTrail = new GameObject("Raid trail").transform;
            _raidTrail.SetParent(transform, false);
        }

        /// <summary>Shows the land (switching from the village's camera), looking at <paramref name="focus"/>.</summary>
        public void Open(WorldLand land, Vector2 focus, Camera main)
        {
            foreach (var model in MergedModels)
            {
                Meshes(model); // cached here, read by the workers
            }

            if (land != _land)
            {
                Clear();
                _land = land;
                _rig.SetBound((land.Radius - 0.5f) * WorldLand.HexSize * Mathf.Sqrt(3f));
                if (Minimap != null)
                {
                    Destroy(Minimap);
                    Minimap = null;
                }

                var bound = Bound;
                _minimapTask = Task.Run(() =>
                {
                    var pixels = new Color32[MinimapSize * MinimapSize];
                    for (var j = 0; j < MinimapSize; j++)
                    {
                        for (var i = 0; i < MinimapSize; i++)
                        {
                            var at = new Vector2((i + 0.5f) / MinimapSize * 2f - 1f, (j + 0.5f) / MinimapSize * 2f - 1f) * bound;
                            var h = land.Height(at);
                            Color c = h < WorldLand.WaterLevel ? WaterColour : land.Colour(at);
                            pixels[j * MinimapSize + i] = c;
                        }
                    }

                    return pixels;
                });
            }

            _main = main;
            if (!IsOpen)
            {
                _rig.JumpTo(focus, MapCamera.HomeDistance);
            }

            gameObject.SetActive(true);
            if (_main != null)
            {
                _main.enabled = false;
            }
        }

        public void Close()
        {
            if (_main != null)
            {
                _main.enabled = true;
            }

            gameObject.SetActive(false);
        }

        /// <summary>Your village (its Hearth level), the rival camps in view and your missionaries.</summary>
        public void SetPeople(int hearthLevel, IReadOnlyList<StageCamp> camps, IReadOnlyList<StageWalker> walkers)
        {
            if (_land == null)
            {
                return;
            }

            if (hearthLevel != _homeLevel)
            {
                _homeLevel = hearthLevel;
                if (_home != null)
                {
                    Destroy(_home);
                }

                _home = _land.TryPlace(WorldMapPresenter.HomeId, out var home) ? Settle(home, _overlay) : null;
            }

            var campsChanged = camps.Count != _campList.Count || camps.Where((c, i) => c.Key != _campList[i].Key).Any();
            var oldSpots = CampSpots();
            foreach (var camp in camps)
            {
                if (!_camps.ContainsKey(camp.Key))
                {
                    _camps[camp.Key] = Camp(camp);
                }
            }

            foreach (var gone in _camps.Keys.Where(k => camps.All(c => c.Key != k)).ToList())
            {
                Destroy(_camps[gone]);
                _camps.Remove(gone);
            }

            _campList.Clear();
            _campList.AddRange(camps);
            foreach (var stale in _anchors.Keys.Where(k => k < 0).ToList())
            {
                _anchors.Remove(stale); // camp ids are their order in the window: they shift as it moves
            }

            foreach (var camp in camps)
            {
                _anchors[camp.Id] = AnchorOf(_land.Spot(camp.Q, camp.R), 1.2f);
            }

            if (campsChanged)
            {
                // Only the chunks a camp arrived in or left re-merge their trees, on a worker.
                var newSpots = CampSpots();
                var moved = oldSpots.Except(newSpots).Concat(newSpots.Except(oldSpots)).ToList();
                foreach (var (key, chunk) in _chunks)
                {
                    var min = new Vector2(key.Item1 * WorldLand.ChunkSize - 1.5f, key.Item2 * WorldLand.ChunkSize - 1.5f);
                    var max = min + Vector2.one * (WorldLand.ChunkSize + 3f);
                    if (moved.Any(m => m.x >= min.x && m.x <= max.x && m.y >= min.y && m.y <= max.y))
                    {
                        var props = chunk.Data.Props;
                        _remerge[key] = Task.Run(() => Merge(props, newSpots));
                    }
                }
            }

            while (_walkers.Count > walkers.Count)
            {
                _walkers[_walkers.Count - 1].Destroy();
                _walkers.RemoveAt(_walkers.Count - 1);
            }

            for (var i = 0; i < walkers.Count; i++)
            {
                if (i == _walkers.Count)
                {
                    _walkers.Add(new Walker(_overlay));
                }

                _walkers[i].Set(_land, walkers[i]);
            }
        }

        /// <summary>Puts the gold ring under a place (null: none).</summary>
        public void Select(int? id)
        {
            _selected = id ?? int.MinValue;
            var at = Vector2.zero;
            var found = id != null && TryLand(id.Value, out at);
            _selection.gameObject.SetActive(found);
            if (found)
            {
                _selection.localPosition = new Vector3(at.x, _land.Height(at) + 0.03f, at.y);
            }
        }

        /// <summary>Flies to the missionary bound for a place (or to the place, if none is on the map).</summary>
        public void FlyToMissionary(int nodeId)
        {
            var walker = _walkers.FirstOrDefault(w => w.NodeId == nodeId);
            if (walker != null)
            {
                _rig.FlyTo(walker.At);
            }
            else
            {
                FlyTo(nodeId);
            }
        }

        /// <summary>A cart of tithe trundles from your village to the lord's seat.</summary>
        public void SendTithe(int seatId)
        {
            if (_land == null)
            {
                return;
            }

            var path = _land.Path(WorldMapPresenter.HomeId, seatId);
            if (path.Count >= 2)
            {
                _carts.Add(new Marcher(_overlay, "haycart", path, 1.4f, 0f, pingPong: false, 0f, 1f, once: true));
            }
        }

        /// <summary>Back to your village, at the usual height.</summary>
        public void GoHome()
        {
            if (TryLand(WorldMapPresenter.HomeId, out var at))
            {
                _rig.FlyTo(at, MapCamera.HomeDistance);
            }
        }

        /// <summary>Your village's land position (for the minimap), if it's on this land.</summary>
        public Vector2? HomeAt => TryLand(WorldMapPresenter.HomeId, out var at) ? at : (Vector2?)null;

        /// <summary>Half the width of the ground in view (the minimap's view box).</summary>
        public float ViewHalfWidth()
        {
            if (_rig.TryScreenToLand(new Vector2(0f, _camera.pixelHeight * 0.5f), out var left))
            {
                return Mathf.Abs(_rig.Focus.x - left.x);
            }

            return _rig.Distance * 0.5f;
        }

        /// <summary>Flies to a place; <paramref name="aboveCard"/> leaves it in the top part of the screen, clear of a card.</summary>
        public void FlyTo(int id, bool aboveCard = false)
        {
            if (TryLand(id, out var at))
            {
                _rig.FlyTo(aboveCard ? at - new Vector2(0f, _rig.Distance * 0.2f) : at);
            }
        }

        /// <summary>Where a place's plate goes, from the view's top left (0–1 across and down); false when off screen.</summary>
        public bool TryViewport(int id, out Vector2 at)
        {
            at = default;
            if (!IsOpen || !_anchors.TryGetValue(id, out var anchor))
            {
                return false;
            }

            var point = _camera.WorldToViewportPoint(anchor);
            at = new Vector2(point.x, 1f - point.y);
            return point.z > 0f && point.x > -0.1f && point.x < 1.1f && point.y > -0.1f && point.y < 1.1f;
        }

        /// <summary>The place, camp or village nearest a tap (on its model or its label), or null.</summary>
        public int? Pick(Vector2 screen)
        {
            var best = (int?)null;
            var bestDistance = PickPixels * Mathf.Max(1f, Screen.dpi > 0 ? Screen.dpi / 160f : Screen.height / 1000f);
            foreach (var (id, anchor) in _anchors)
            {
                var top = _camera.WorldToScreenPoint(anchor);
                var foot = _camera.WorldToScreenPoint(anchor - Vector3.up * 1.2f);
                if (top.z <= 0f)
                {
                    continue;
                }

                var d = SegmentDistance(screen, foot, top);
                if (d < bestDistance)
                {
                    best = id;
                    bestDistance = d;
                }
            }

            return best;
        }

        /// <summary>The hex under the middle of the view (where to fetch the world's window).</summary>
        public (int Q, int R) FocusHex()
        {
            var at = _rig.Focus;
            var r = -at.y / (WorldLand.HexSize * 1.5f);
            var q = at.x / (WorldLand.HexSize * Mathf.Sqrt(3f)) - r / 2f;
            var s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds)
            {
                rq = -rr - rs;
            }
            else if (dr > ds)
            {
                rr = -rq - rs;
            }

            return (rq, rr);
        }

        /// <summary>How many hexes from the focus the view shows (to size the world's window).</summary>
        public int ViewRadiusHexes()
        {
            var corners = new[] { new Vector2(0, 0), new Vector2(Screen.width, 0), new Vector2(0, Screen.height), new Vector2(Screen.width, Screen.height) };
            var far = 0f;
            foreach (var corner in corners)
            {
                if (_rig.TryScreenToLand(corner, out var land))
                {
                    far = Mathf.Max(far, Vector2.Distance(land, _rig.Focus));
                }
                else
                {
                    far = Mathf.Max(far, _rig.Distance * 2f);
                }
            }

            return Mathf.CeilToInt(far / (WorldLand.HexSize * Mathf.Sqrt(3f)));
        }

        private void Update()
        {
            if (_land == null)
            {
                return;
            }

            _rig.Tick(Time.deltaTime);
            if (_minimapTask != null && _minimapTask.IsCompleted)
            {
                if (_minimapTask.Status == TaskStatus.RanToCompletion)
                {
                    Minimap = new Texture2D(MinimapSize, MinimapSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    Minimap.SetPixels32(_minimapTask.Result);
                    Minimap.Apply(false, true);
                }

                _minimapTask = null;
            }
            var focus = _rig.Focus;
            _water.localPosition = new Vector3(focus.x, WorldLand.WaterLevel - 0.04f, focus.y);
            RenderSettings.fogStartDistance = _rig.Distance + 14f;
            RenderSettings.fogEndDistance = _rig.Distance * 2.4f + 40f;
            Stream();
            foreach (var walker in _walkers)
            {
                walker.Tick(_land, Time.deltaTime);
            }

            UpdateRings();
            UpdateTension(Time.deltaTime);

            if (_selected != int.MinValue && _selection.gameObject.activeSelf)
            {
                var pulse = 2.2f + Mathf.Sin(Time.time * 4f) * 0.12f;
                _selection.localScale = new Vector3(pulse, 0.02f, pulse);
            }
        }

        /// <summary>Who holds what in the places in view (the rings the far view shows).</summary>
        public void SetFaith(IReadOnlyList<StageFaith> faith)
        {
            _faith.Clear();
            _faith.AddRange(faith);
            _ringSync = 0f;
        }

        /// <summary>
        /// The lord's temper on the land (spec 2026-10-15, point 6): past a third his war banner flies over his seat and
        /// grows; past that his patrols walk the road to your village; when a raid is coming a column marches on it.
        /// </summary>
        public void SetTension(int seatId, float fraction, bool raidComing)
        {
            if (_land == null)
            {
                return;
            }

            if (seatId != _seatId)
            {
                _seatId = seatId;
                ClearMarchers();
                _warPath = seatId >= 0 ? _land.Path(seatId, WorldMapPresenter.HomeId) : new List<Vector2>();
            }

            _tension = fraction;
            _raidComing = raidComing;
            var patrols = _warPath.Count < 2 || fraction < 0.35f ? 0 : Mathf.Min(4, 1 + (int)((fraction - 0.35f) / 0.18f));
            while (_patrols.Count > patrols)
            {
                _patrols[_patrols.Count - 1].Destroy();
                _patrols.RemoveAt(_patrols.Count - 1);
            }

            while (_patrols.Count < patrols)
            {
                var i = _patrols.Count;
                _patrols.Add(new Marcher(_overlay, "manatarms", _warPath, 0.55f + i * 0.07f, 0.15f + i * 0.21f, pingPong: true, 0.2f, 0.85f));
            }

            var column = raidComing && _warPath.Count >= 2 ? 6 : 0;
            while (_column.Count > column)
            {
                _column[_column.Count - 1].Destroy();
                _column.RemoveAt(_column.Count - 1);
            }

            while (_column.Count < column)
            {
                var i = _column.Count;
                _column.Add(new Marcher(_overlay, i % 3 == 0 ? "manatarms" : "levy", _warPath, 0.7f, 0.45f - i * 0.025f, pingPong: false, 0.3f, 0.93f));
            }
        }

        private void ClearMarchers()
        {
            foreach (var m in _patrols.Concat(_column).Concat(_carts))
            {
                m.Destroy();
            }

            _patrols.Clear();
            _column.Clear();
            _carts.Clear();
            foreach (var dot in _raidDots)
            {
                Destroy(dot);
            }

            _raidDots.Clear();
        }

        private void UpdateTension(float dt)
        {
            var banner = _seatId >= 0 && _tension >= 0.3f && TryLand(_seatId, out var seat);
            _warBanner.gameObject.SetActive(banner);
            if (banner)
            {
                TryLand(_seatId, out var at);
                var size = 0.8f + _tension * 1.4f + (_raidComing ? 0.5f : 0f);
                _warBanner.localPosition = new Vector3(at.x + 0.6f, _land.Height(at) + 0.6f, at.y + 0.3f);
                _warBanner.localScale = Vector3.one * size;
                _warBanner.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2.3f) * 12f, Mathf.Sin(Time.time * 3.1f) * 3f);
            }

            foreach (var m in _patrols.Concat(_column).Concat(_carts))
            {
                m.Tick(_land, dt);
            }

            foreach (var done in _carts.Where(c => c.Done).ToList())
            {
                done.Destroy();
                _carts.Remove(done);
            }

            // A red trail ahead of the column, the way it's going.
            var dots = _raidComing && _warPath.Count >= 2 ? 14 : 0;
            while (_raidDots.Count < dots)
            {
                var dot = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#a8322a"), Vector3.zero, new Vector3(0.14f, 0.01f, 0.14f), _raidTrail, "Dot");
                dot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                _raidDots.Add(dot);
            }

            for (var k = 0; k < _raidDots.Count; k++)
            {
                var shown = k < dots;
                _raidDots[k].SetActive(shown);
                if (shown)
                {
                    var t = Mathf.Lerp(0.55f, 1f, (k + Mathf.Repeat(Time.time * 0.8f, 1f)) / dots);
                    var p = WorldLand.Along(_warPath, t);
                    _raidDots[k].transform.localPosition = new Vector3(p.x, _land.Height(p) + 0.05f, p.y);
                }
            }
        }

        /// <summary>Rings on the ground round each place in view: yours inside (hi-vis), the strongest rival's outside.</summary>
        private void UpdateRings()
        {
            var show = Mathf.InverseLerp(20f, 30f, _rig.Distance);
            _ringRoot.gameObject.SetActive(show > 0.01f);
            if (show <= 0.01f)
            {
                return;
            }

            _ringRoot.localScale = new Vector3(1f, 1f, 1f);
            if ((_ringSync -= Time.deltaTime) > 0f)
            {
                return;
            }

            _ringSync = 0.5f;
            var wanted = new HashSet<int>();
            foreach (var f in _faith)
            {
                if (!TryLand(f.Id, out var at) || (f.Mine <= 0.005f && f.Rival <= 0.005f))
                {
                    continue;
                }

                wanted.Add(f.Id);
                if (!_rings.TryGetValue(f.Id, out var ring))
                {
                    ring = (Ring(new Color(0.91f, 1f, 0.23f)), Ring(Color.white));
                    _rings[f.Id] = ring;
                }

                // Inside the levelled ground round the place, so the hills don't swallow them.
                var h = _land.Height(at) + 0.08f;
                Place(ring.Mine, at, h, 0.9f, f.Mine);
                ring.Rival.GetComponent<Renderer>().sharedMaterial = Shapes.Material(f.RivalColour);
                Place(ring.Rival, at, h - 0.01f, 1.25f, f.Rival);
            }

            foreach (var gone in _rings.Keys.Where(k => !wanted.Contains(k)).ToList())
            {
                Destroy(_rings[gone].Mine);
                Destroy(_rings[gone].Rival);
                _rings.Remove(gone);
            }

            void Place(GameObject ring, Vector2 at, float height, float radius, float share)
            {
                ring.SetActive(share > 0.005f);
                var r = radius + share * 0.35f;
                ring.transform.localPosition = new Vector3(at.x, height, at.y);
                ring.transform.localScale = new Vector3(r, 1f, r);
            }
        }

        private GameObject Ring(Color colour)
        {
            if (_annulus == null)
            {
                _annulus = Annulus(0.8f, 48);
            }

            var go = new GameObject("Ring");
            go.transform.SetParent(_ringRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = _annulus;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Shapes.Material(colour);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A flat ring, outer radius 1, facing up.</summary>
        private static Mesh Annulus(float inner, int segments)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = d;
                vertices[i * 2 + 1] = d * inner;
                var n = (i + 1) % segments;
                triangles[i * 6] = i * 2;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = n * 2;
                triangles[i * 6 + 3] = n * 2;
                triangles[i * 6 + 4] = i * 2 + 1;
                triangles[i * 6 + 5] = n * 2 + 1;
            }

            var mesh = new Mesh { name = "Annulus", vertices = vertices, triangles = triangles };
            mesh.normals = vertices.Select(_ => Vector3.up).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- chunks ----------------

        private void Stream()
        {
            // What the camera sees of the ground (its four corners), plus a little: only that is grown.
            var focus = _rig.Focus;
            var lo = focus;
            var hi = focus;
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                var screen = new Vector2(corner.x * _camera.pixelWidth, corner.y * _camera.pixelHeight);
                if (!_rig.TryScreenToLand(screen, out var seen) || Vector2.Distance(seen, focus) > _rig.Distance * 3f)
                {
                    seen = focus + (seen - focus).normalized * _rig.Distance * 3f;
                }

                lo = Vector2.Min(lo, seen);
                hi = Vector2.Max(hi, seen);
            }

            const float margin = 3f;
            var min = new Vector2Int(Mathf.FloorToInt((lo.x - margin) / WorldLand.ChunkSize), Mathf.FloorToInt((lo.y - margin) / WorldLand.ChunkSize));
            var max = new Vector2Int(Mathf.FloorToInt((hi.x + margin) / WorldLand.ChunkSize), Mathf.FloorToInt((hi.y + margin) / WorldLand.ChunkSize));

            foreach (var key in _remerge.Keys.Where(k => _remerge[k].IsCompleted).ToList())
            {
                if (_remerge[key].Status == TaskStatus.RanToCompletion && _chunks.TryGetValue(key, out var chunk))
                {
                    chunk.SetProps(ToMesh(_remerge[key].Result));
                }

                _remerge.Remove(key);
            }

            // Finished builds become meshes (one per frame, so the map never stutters).
            var done = 0;
            foreach (var key in _building.Keys.ToList())
            {
                var task = _building[key];
                if (!task.IsCompleted || done >= 1)
                {
                    continue;
                }

                _building.Remove(key);
                if (task.Status == TaskStatus.RanToCompletion && key.Item1 >= min.x - 1 && key.Item1 <= max.x + 1 && key.Item2 >= min.y - 1 && key.Item2 <= max.y + 1)
                {
                    _chunks[key] = MakeChunk(key, task.Result);
                    done++;
                }
            }

            var wanted = new List<(int, int)>();
            for (var cx = min.x; cx <= max.x; cx++)
            {
                for (var cz = min.y; cz <= max.y; cz++)
                {
                    if (!_chunks.ContainsKey((cx, cz)) && !_building.ContainsKey((cx, cz)))
                    {
                        wanted.Add((cx, cz));
                    }
                }
            }

            var land = _land;
            var camps = CampSpots();
            foreach (var key in wanted.OrderBy(k => (new Vector2((k.Item1 + 0.5f) * WorldLand.ChunkSize, (k.Item2 + 0.5f) * WorldLand.ChunkSize) - focus).sqrMagnitude))
            {
                if (_building.Count >= 3)
                {
                    break;
                }

                var (cx, cz) = key;
                // Everything but the GPU upload happens on a worker: the land, its shading, and the merged trees and settlements.
                _building[key] = Task.Run(() => Grow(land, cx, cz, camps));
            }

            foreach (var key in _chunks.Keys.Where(k => k.Item1 < min.x - 2 || k.Item1 > max.x + 2 || k.Item2 < min.y - 2 || k.Item2 > max.y + 2).ToList())
            {
                foreach (var id in _chunks[key].PlaceIds)
                {
                    _anchors.Remove(id);
                }

                _chunks[key].Destroy();
                _chunks.Remove(key);
            }
        }

        private void Clear()
        {
            foreach (var chunk in _chunks.Values)
            {
                chunk.Destroy();
            }

            _chunks.Clear();
            _building.Clear();
            _remerge.Clear();
            foreach (var camp in _camps.Values)
            {
                Destroy(camp);
            }

            _camps.Clear();
            _campList.Clear();
            if (_home != null)
            {
                Destroy(_home);
            }

            _home = null;
            _homeLevel = -1;
            foreach (var walker in _walkers)
            {
                walker.Destroy();
            }

            _walkers.Clear();
            _anchors.Clear();
            foreach (var ring in _rings.Values)
            {
                Destroy(ring.Mine);
                Destroy(ring.Rival);
            }

            _rings.Clear();
            ClearMarchers();
            _seatId = -1;
        }

        private Chunk MakeChunk((int X, int Z) key, ChunkData data)
        {
            var n = WorldLand.ChunkCells + 1;
            var size = WorldLand.ChunkSize;
            var origin = new Vector2(key.X * size, key.Z * size);
            var vertices = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var p = WorldLand.ChunkPixels;
            for (var j = 0; j < n; j++)
            {
                for (var i = 0; i < n; i++)
                {
                    var u = i / (float)WorldLand.ChunkCells;
                    var v = j / (float)WorldLand.ChunkCells;
                    vertices[j * n + i] = new Vector3(origin.x + u * size, data.Heights[j * n + i], origin.y + v * size);
                    uvs[j * n + i] = new Vector2((0.5f + u * (p - 1)) / p, (0.5f + v * (p - 1)) / p);
                }
            }

            var triangles = new int[WorldLand.ChunkCells * WorldLand.ChunkCells * 6];
            var t = 0;
            for (var j = 0; j < WorldLand.ChunkCells; j++)
            {
                for (var i = 0; i < WorldLand.ChunkCells; i++)
                {
                    var a = j * n + i;
                    triangles[t++] = a;
                    triangles[t++] = a + n;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + n;
                    triangles[t++] = a + n + 1;
                }
            }

            var normals = data.Normals;
            var mesh = new Mesh { name = $"Land {key.X},{key.Z}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var texture = new Texture2D(p, p, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            texture.SetPixels32(data.Colours);
            texture.Apply(true, true);
            var material = new Material(_groundBase) { mainTexture = texture, color = Color.white };
            material.SetFloat("_Smoothness", 0.05f);

            var ground = new GameObject(mesh.name);
            ground.transform.SetParent(transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var trees = new GameObject("Trees");
            trees.transform.SetParent(ground.transform, false);
            trees.AddComponent<MeshFilter>();
            trees.AddComponent<MeshRenderer>().sharedMaterial = ArtLibrary.PaletteMaterial;
            var chunk = new Chunk { Ground = ground, Mesh = mesh, Texture = texture, Material = material, Data = data, Trees = trees };
            foreach (var id in data.PlaceIds)
            {
                if (_land.TryPlace(id, out var place))
                {
                    Anchor(place);
                    chunk.PlaceIds.Add(id);
                }
            }

            chunk.SetProps(ToMesh(data.Merged));

            return chunk;
        }

        /// <summary>
        /// The chunk's trees, rocks and mountains merged into one mesh (one draw for the lot), minus any standing where a
        /// camp has pitched.
        /// </summary>
        private Mesh MergeProps(List<LandProp> props) => ToMesh(Merge(props, CampSpots()));

        private List<Vector2> CampSpots() => _campList.Select(c => _land.Spot(c.Q, c.R)).ToList();

        /// <summary>
        /// A chunk grown on a worker thread: heights, normals from the land itself (so neighbouring chunks shade without
        /// a seam), colours, and its trees, rocks and settlements merged into one mesh's worth of arrays.
        /// </summary>
        private ChunkData Grow(WorldLand land, int cx, int cz, List<Vector2> camps)
        {
            var data = new ChunkData { Heights = land.ChunkHeights(cx, cz), Colours = land.ChunkColours(cx, cz), Props = land.Props(cx, cz) };
            var n = WorldLand.ChunkCells + 1;
            var size = WorldLand.ChunkSize;
            var origin = new Vector2(cx * size, cz * size);
            data.Normals = new Vector3[n * n];
            const float e = 0.5f;
            for (var j = 0; j < n; j++)
            {
                for (var i = 0; i < n; i++)
                {
                    var at = origin + new Vector2(i / (float)WorldLand.ChunkCells * size, j / (float)WorldLand.ChunkCells * size);
                    var dx = land.Height(at + new Vector2(e, 0f)) - land.Height(at - new Vector2(e, 0f));
                    var dz = land.Height(at + new Vector2(0f, e)) - land.Height(at - new Vector2(0f, e));
                    data.Normals[j * n + i] = new Vector3(-dx, 2f * e, -dz).normalized;
                }
            }

            // The settlements join the trees in the chunk's one mesh: far out, hundreds of places cost a single draw.
            foreach (var place in land.Places)
            {
                if (place.Type != null && place.At.x >= origin.x && place.At.x < origin.x + size && place.At.y >= origin.y && place.At.y < origin.y + size)
                {
                    data.Props.AddRange(Settlement(place));
                    data.PlaceIds.Add(place.Id);
                }
            }

            data.Merged = Merge(data.Props, camps);
            return data;
        }

        private static Mesh ToMesh(Merged merged)
        {
            var mesh = new Mesh { name = "Trees", indexFormat = merged.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(merged.Vertices);
            mesh.SetNormals(merged.Normals);
            mesh.SetUVs(0, merged.Uvs);
            mesh.SetTriangles(merged.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Props into one mesh's arrays (worker-safe: only the meshes cached on opening are read).</summary>
        private Merged Merge(List<LandProp> props, List<Vector2> camps)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var prop in props)
            {
                var at = new Vector2(prop.At.x, prop.At.z);
                if (camps.Any(c => Vector2.Distance(c, at) < 1.2f))
                {
                    continue;
                }

                var root = Matrix4x4.TRS(prop.At, Quaternion.Euler(0f, prop.Yaw, 0f), Vector3.one * prop.Scale);
                if (!_propMeshes.TryGetValue(prop.Model, out var parts))
                {
                    continue;
                }

                foreach (var part in parts)
                {
                    var m = root * part.Local;
                    var first = vertices.Count;
                    foreach (var v in part.Vertices)
                    {
                        vertices.Add(m.MultiplyPoint3x4(v));
                    }

                    foreach (var n in part.Normals)
                    {
                        normals.Add(m.MultiplyVector(n).normalized);
                    }

                    uvs.AddRange(part.Uvs);
                    foreach (var t in part.Triangles)
                    {
                        triangles.Add(first + t);
                    }
                }
            }

            return new Merged { Vertices = vertices, Normals = normals, Uvs = uvs, Triangles = triangles };
        }

        private static readonly string[] MergedModels =
        {
            "pine", "oak", "birch", "crag", "mountain", "cottage", "cottage_l2", "haycart", "well", "world_market", "world_abbey", "world_keep", "world_cathedral",
        };

        private PropMesh[] Meshes(string model)
        {
            if (_propMeshes.TryGetValue(model, out var parts))
            {
                return parts;
            }

            var prefab = Resources.Load<GameObject>("Art/Models/" + model);
            parts = prefab == null
                ? Array.Empty<PropMesh>()
                : prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.sharedMesh.isReadable)
                    .Select(f => new PropMesh
                    {
                        Local = f.transform.localToWorldMatrix, Vertices = f.sharedMesh.vertices, Normals = f.sharedMesh.normals,
                        Uvs = f.sharedMesh.uv, Triangles = f.sharedMesh.triangles,
                    }).ToArray();
            _propMeshes[model] = parts;
            return parts;
        }

        // ---------------- settlements ----------------

        /// <summary>A place's buildings, laid out round its spot: what stands there says what it is.</summary>
        private List<LandProp> Settlement(LandPlace place)
        {
            var props = new List<LandProp>();
            var yaw = Hash(place.Q, place.R) * 360f;
            var spin = Quaternion.Euler(0f, yaw, 0f);
            void Put(string model, float x, float z, float scale, float turn = 0f)
            {
                var offset = spin * new Vector3(x, 0f, z);
                var at = place.At + new Vector2(offset.x, offset.z);
                props.Add(new LandProp(model, new Vector3(at.x, _land.Height(at), at.y), yaw + turn, scale));
            }

            var s = ModelScale;
            switch (place.Type)
            {
                case null:
                    var level = Mathf.Clamp(_homeLevel, 1, 3);
                    Put($"hearth_l{level}", 0f, 0f, 0.3f, -yaw);
                    Put("cultist_hut", 0.95f, 0.35f, s, 200f);
                    Put("cottage", -0.9f, 0.4f, s, 160f);
                    if (level >= 2)
                    {
                        Put("cultist_hut", 0.1f, 1.0f, s, 180f);
                    }

                    break;
                case NodeType.Hamlet:
                    Put("cottage", -0.35f, 0.1f, s, 10f);
                    Put("cottage", 0.4f, -0.2f, s, -25f);
                    Put("haycart", 0.1f, 0.55f, s * 0.55f, 70f);
                    break;
                case NodeType.Village:
                    Put("cottage_l2", 0f, 0.35f, s * 0.85f, 0f);
                    Put("cottage", -0.6f, -0.2f, s, 30f);
                    Put("cottage", 0.6f, -0.25f, s, -20f);
                    Put("well", 0f, -0.45f, s, 0f);
                    break;
                case NodeType.MarketTown:
                    Put("world_market", 0f, 0f, 0.75f, 0f);
                    Put("cottage_l2", -0.75f, 0.55f, s * 0.85f, 25f);
                    Put("cottage_l2", 0.75f, 0.5f, s * 0.85f, -30f);
                    Put("cottage", 0.85f, -0.4f, s, -80f);
                    break;
                case NodeType.Abbey:
                    Put("world_abbey", 0f, 0f, 0.8f, 0f);
                    break;
                case NodeType.Keep:
                    Put("world_keep", 0f, 0f, 0.85f, -yaw);
                    break;
                case NodeType.Cathedral:
                    Put("world_cathedral", 0f, 0.1f, 0.8f, -yaw);
                    Put("cottage", -0.85f, -0.5f, s, 40f);
                    Put("cottage", 0.85f, -0.55f, s, -35f);
                    break;
            }

            return props;
        }

        private void Anchor(LandPlace place) =>
            _anchors[place.Id] = AnchorOf(place.At, place.Type == NodeType.Keep || place.Type == NodeType.Cathedral ? 1.9f : 1.2f);

        /// <summary>Your village: its own objects (the Hearth changes with its level), with the cult's flag.</summary>
        private GameObject Settle(LandPlace place, Transform parent)
        {
            var root = new GameObject("Your village");
            root.transform.SetParent(parent, false);
            Anchor(place);
            foreach (var prop in Settlement(place))
            {
                var go = ArtLibrary.Instantiate(prop.Model, root.transform);
                if (go != null)
                {
                    go.transform.localPosition = prop.At;
                    go.transform.localRotation = Quaternion.Euler(0f, prop.Yaw, 0f);
                    go.transform.localScale = Vector3.one * prop.Scale;
                }
            }

            var pole = Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, Vector3.zero, new Vector3(0.05f, 0.75f, 0.05f), root.transform, "Pole");
            var flag = Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#46618c"), Vector3.zero, new Vector3(0.42f, 0.28f, 0.03f), root.transform, "Flag");
            var foot = place.At + new Vector2(-0.75f, -0.55f);
            pole.transform.localPosition = new Vector3(foot.x, _land.Height(foot) + 0.75f, foot.y);
            flag.transform.localPosition = pole.transform.localPosition + new Vector3(0.22f, 0.55f, 0f);
            return root;
        }

        private GameObject Camp(StageCamp camp)
        {
            var at = _land.Spot(camp.Q, camp.R);
            var go = ArtLibrary.Instantiate("world_camp", _overlay);
            if (go == null)
            {
                go = new GameObject("Camp");
                go.transform.SetParent(_overlay, false);
            }

            go.name = $"Camp {camp.Key}";
            var ground = _land.Height(at);
            var wet = ground < WorldLand.WaterLevel + 0.15f;
            ground = Mathf.Max(ground, WorldLand.WaterLevel + 0.15f);
            go.transform.localPosition = new Vector3(at.x, ground, at.y);
            go.transform.localRotation = Quaternion.Euler(0f, Hash(camp.Q, camp.R) * 360f, 0f);
            go.transform.localScale = Vector3.one * 0.85f;
            if (wet)
            {
                // Pitched where the land runs wet (the server never sees the lakes): a little mud islet keeps their feet dry.
                var islet = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#6b5a3a"), Vector3.zero, new Vector3(1.9f, 0.4f, 1.7f), _overlay, "Islet");
                islet.transform.localPosition = new Vector3(at.x, ground - 0.4f, at.y);
                islet.transform.SetParent(go.transform, true);
            }
            var banner = ArtLibrary.FindPart(go.transform, "Banner");
            if (banner != null)
            {
                foreach (var renderer in banner.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = Shapes.Material(camp.Colour);
                }
            }

            return go;
        }

        private Vector3 AnchorOf(Vector2 at, float height) => Origin + new Vector3(at.x, _land.Height(at) + height, at.y);

        private bool TryLand(int id, out Vector2 at)
        {
            at = default;
            if (!_anchors.TryGetValue(id, out var anchor))
            {
                // Not grown yet (far off): the land still knows where it stands.
                if (_land == null || !_land.TryPlace(id, out var place))
                {
                    return false;
                }

                at = place.At;
                return true;
            }

            at = new Vector2(anchor.x - Origin.x, anchor.z - Origin.z);
            return true;
        }

        private static float Hash(int q, int r)
        {
            unchecked
            {
                var h = (uint)q * 0x9e3779b1u ^ (uint)r * 0x85ebca77u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                return (h & 0xffff) / 65535f;
            }
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private void OnDestroy() => Clear();

        private sealed class Merged
        {
            public List<Vector3> Vertices;
            public List<Vector3> Normals;
            public List<Vector2> Uvs;
            public List<int> Triangles;
        }

        private sealed class ChunkData
        {
            public Vector3[] Normals;
            public Merged Merged;
            public readonly List<int> PlaceIds = new List<int>();
            public float[] Heights;
            public Color32[] Colours;
            public List<LandProp> Props;
        }

        private sealed class PropMesh
        {
            public Matrix4x4 Local;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[] Triangles;
        }

        private sealed class Chunk
        {
            public GameObject Ground;
            public Mesh Mesh;
            public Texture2D Texture;
            public Material Material;
            public ChunkData Data;
            public GameObject Trees;
            public readonly List<int> PlaceIds = new List<int>();

            public void SetProps(Mesh props)
            {
                var filter = Trees.GetComponent<MeshFilter>();
                if (filter.sharedMesh != null)
                {
                    UnityEngine.Object.Destroy(filter.sharedMesh);
                }

                filter.sharedMesh = props;
            }

            public void Destroy()
            {
                SetProps(null);
                UnityEngine.Object.Destroy(Ground);
                UnityEngine.Object.Destroy(Mesh);
                UnityEngine.Object.Destroy(Texture);
                UnityEngine.Object.Destroy(Material);
            }
        }

        /// <summary>A missionary walking the tracks, with a dotted trail to where they're going.</summary>
        private sealed class Walker
        {
            private const float DotSpacing = 0.38f;
            private readonly GameObject _figure;
            private readonly PartAnimator _animator;
            private readonly Transform _trail;
            private readonly List<GameObject> _dots = new List<GameObject>();
            private List<Vector2> _path = new List<Vector2>();
            private StageWalker _model;
            private Vector2 _at;
            private bool _placed;
            private float _preach;

            public Walker(Transform parent)
            {
                _figure = ArtLibrary.Instantiate("cultist", parent) ?? Shapes.Create(PrimitiveType.Capsule, Shapes.Hex("#46618c"), Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), parent);
                _figure.name = "Missionary";
                _figure.transform.localScale = Vector3.one * 0.4f;
                if (_figure.GetComponentsInChildren<Transform>().Any(t => t.name == "Body"))
                {
                    _animator = _figure.AddComponent<PartAnimator>();
                    _animator.Bind();
                }

                _trail = new GameObject("Trail").transform;
                _trail.SetParent(parent, false);
            }

            public int NodeId => _model?.NodeId ?? -1;

            public Vector2 At => _at;

            public void Set(WorldLand land, StageWalker model)
            {
                var newPath = _model == null || _model.NodeId != model.NodeId;
                _model = model;
                if (newPath)
                {
                    _path = land.Path(WorldMapPresenter.HomeId, model.NodeId);
                    _placed = false;
                }
            }

            public void Tick(WorldLand land, float dt)
            {
                if (_model == null || _path.Count == 0)
                {
                    return;
                }

                var fraction = _model.Phase == MissionaryPhase.Travelling ? _model.Fraction : _model.Phase == MissionaryPhase.Returning ? 1f - _model.Fraction : 1f;
                var target = WorldLand.Along(_path, fraction);
                if (_model.Phase == MissionaryPhase.Preaching)
                {
                    target += new Vector2(0.75f, -0.55f); // in front of the place, holding forth
                }

                var previous = _at;
                _at = _placed ? Vector2.MoveTowards(_at, target, dt * 1.2f) : target;
                _placed = true;
                var moving = (_at - previous).sqrMagnitude > 0.000001f;
                _figure.transform.localPosition = new Vector3(_at.x, land.Height(_at), _at.y);
                var ahead = _model.Phase == MissionaryPhase.Returning ? WorldLand.Along(_path, Mathf.Max(0f, fraction - 0.02f)) : WorldLand.Along(_path, Mathf.Min(1f, fraction + 0.02f));
                var look = _model.Phase == MissionaryPhase.Preaching ? Vector2.down : ahead - _at;
                if (look.sqrMagnitude > 0.0001f)
                {
                    var rotation = Quaternion.LookRotation(new Vector3(look.x, 0f, look.y));
                    _figure.transform.localRotation = Quaternion.Slerp(_figure.transform.localRotation, rotation, 1f - Mathf.Exp(-dt * 8f));
                }

                if (_animator != null)
                {
                    _animator.IsMoving = moving || _model.Phase != MissionaryPhase.Preaching;
                    if (_model.Phase == MissionaryPhase.Preaching && (_preach -= dt) <= 0f)
                    {
                        _preach = 2.4f;
                        _animator.Attack(); // a sermon, with gestures
                    }
                }

                Trail(land, fraction);
            }

            /// <summary>Dots from the walker to where they're heading (none while preaching).</summary>
            private void Trail(WorldLand land, float fraction)
            {
                var points = new List<Vector2>();
                if (_model.Phase != MissionaryPhase.Preaching)
                {
                    var total = 0f;
                    for (var k = 1; k < _path.Count; k++)
                    {
                        total += Vector2.Distance(_path[k - 1], _path[k]);
                    }

                    var from = fraction;
                    var to = _model.Phase == MissionaryPhase.Travelling ? 1f : 0f;
                    var count = Mathf.FloorToInt(Mathf.Abs(to - from) * total / DotSpacing);
                    var phase = Time.time * 0.6f % 1f; // the dots crawl towards the goal
                    for (var k = 1; k <= Mathf.Min(count, 60); k++)
                    {
                        var t = from + Mathf.Sign(to - from) * (k - phase) * DotSpacing / Mathf.Max(total, 0.01f);
                        points.Add(WorldLand.Along(_path, t));
                    }
                }

                while (_dots.Count < points.Count)
                {
                    var dot = Shapes.Create(PrimitiveType.Cylinder, Palette.Linen, Vector3.zero, new Vector3(0.12f, 0.01f, 0.12f), _trail, "Dot");
                    dot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    _dots.Add(dot);
                }

                for (var k = 0; k < _dots.Count; k++)
                {
                    var shown = k < points.Count;
                    _dots[k].SetActive(shown);
                    if (shown)
                    {
                        _dots[k].transform.localPosition = new Vector3(points[k].x, land.Height(points[k]) + 0.04f, points[k].y);
                    }
                }
            }

            public void Destroy()
            {
                UnityEngine.Object.Destroy(_figure);
                UnityEngine.Object.Destroy(_trail.gameObject);
            }
        }
        /// <summary>One of the lord's men on the road between his seat and your village.</summary>
        private sealed class Marcher
        {
            private readonly GameObject _figure;
            private readonly PartAnimator _animator;
            private readonly List<Vector2> _path;
            private readonly float _speed;
            private readonly bool _pingPong;
            private readonly float _from;
            private readonly float _to;
            private readonly float _length;
            private readonly bool _once;
            private float _t;
            private int _direction = 1;

            public Marcher(Transform parent, string model, List<Vector2> path, float speed, float start, bool pingPong, float from, float to, bool once = false)
            {
                _once = once;
                _figure = ArtLibrary.Instantiate(model, parent) ?? Shapes.Create(PrimitiveType.Capsule, Palette.ManAtArms, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), parent);
                _figure.transform.localScale = Vector3.one * 0.4f;
                if (_figure.GetComponentsInChildren<Transform>().Any(t => t.name == "Body"))
                {
                    _animator = _figure.AddComponent<PartAnimator>();
                    _animator.Bind();
                    _animator.IsMoving = true;
                }

                _path = path;
                _speed = speed;
                _pingPong = pingPong;
                _from = from;
                _to = to;
                for (var k = 1; k < path.Count; k++)
                {
                    _length += Vector2.Distance(path[k - 1], path[k]);
                }

                _t = Mathf.Lerp(from, to, Mathf.Repeat(start, 1f));
            }

            public void Tick(WorldLand land, float dt)
            {
                if (_length <= 0f)
                {
                    return;
                }

                _t += _direction * _speed * dt / _length;
                if (_t > _to)
                {
                    if (_pingPong)
                    {
                        _t = _to;
                        _direction = -1;
                    }
                    else if (_once)
                    {
                        _t = _to;
                        Done = true;
                    }
                    else
                    {
                        _t = _from;
                    }
                }
                else if (_t < _from)
                {
                    _t = _from;
                    _direction = 1;
                }

                var at = WorldLand.Along(_path, _t);
                var ahead = WorldLand.Along(_path, Mathf.Clamp01(_t + _direction * 0.01f));
                _figure.transform.localPosition = new Vector3(at.x, land.Height(at), at.y);
                var look = ahead - at;
                if (look.sqrMagnitude > 0.00001f)
                {
                    _figure.transform.localRotation = Quaternion.LookRotation(new Vector3(look.x, 0f, look.y));
                }
            }

            /// <summary>A one-way trip (a tithe cart) has arrived.</summary>
            public bool Done { get; private set; }

            public void Destroy() => UnityEngine.Object.Destroy(_figure);
        }
    }
}
