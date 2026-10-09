using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    public enum GamePhase
    {
        Dusk,
        Night,
        Dawn,
    }

    /// <summary>
    /// Prototype game loop: Dusk (build towers) → Night (live deterministic battle) → Dawn (result + server-style replay check).
    /// All game rules live in CultOfComfort.Simulation; this class only translates input into commands and drives the views.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float TickSeconds = 1f / BalanceTable.TicksPerSecond;
        private const int MaxStepsPerFrame = 8;
        private const int StartingScrap = 380;
        private const int RaidBudget = 54;
        private const int RaidWaves = 4;
        private const float ReferenceScreenHeight = 720f;

        /// <summary>Extra inset beyond the OS safe area, for rounded display corners (fraction of screen height).</summary>
        private const float CornerMargin = 0.04f;

        private static readonly Dictionary<TowerType, int> TowerCost = new Dictionary<TowerType, int>
        {
            [TowerType.SaplingFlinger] = 70,
            [TowerType.SlurryChute] = 50,
        };

        private readonly List<PlacedTower> _towers = new List<PlacedTower>();
        private readonly List<Rect> _uiRects = new List<Rect>();
        private readonly Queue<string> _announcements = new Queue<string>();
        private IReadOnlyList<GridPosition> _rocks;
        private readonly List<Vector2> _pointers = new List<Vector2>();
        private Camera _camera;
        private CameraRig _rig;
        private GestureRecognizer _gestures;
        private GestureFrame _gesture;
        private BoardView _board;
        private BattleView _battleView;
        private GamePhase _phase = GamePhase.Dusk;
        private TowerType? _selectedTower = TowerType.SaplingFlinger;
        private int _scrap = StartingScrap;
        private int _night = 1;
        private Battle _battle;
        private BattleSetup _setup;
        private BattleResult _serverReplay;
        private float _accumulator;
        private int _speed = 1;
        private string _announcement = "Tap a tile to build. Towers reroute the road — use that.";
        private float _announcementUntil = float.MaxValue;
        private float _guiScale = 1f;
        private Rect _safe = new Rect(0f, 0f, 1280f, ReferenceScreenHeight);
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _button;
        private GUIStyle _box;

        public GamePhase Phase => _phase;

        /// <summary>True when the server-style replay of the last Night matched the live result.</summary>
        public bool? LastNightVerified { get; private set; }

        private void Start()
        {
            Application.targetFrameRate = 60;
            PointerInput.Enable();
            _rocks = DemoMap.CreateRocks();

            _board = gameObject.AddComponent<BoardView>();
            _board.Build(_rocks);
            _battleView = gameObject.AddComponent<BattleView>();
            _battleView.Announcement += Announce;

            SetUpCamera();
            RefreshBoard();
        }

        private void Update()
        {
            ReadGestures();
            if (_phase == GamePhase.Dusk)
            {
                UpdateDusk();
            }
            else if (_phase == GamePhase.Night)
            {
                UpdateNight();
            }

            _board.SetHearthWarmth(_battle == null ? 1f : _battle.HearthHealth / (float)_battle.HearthMaxHealth);
            _rig.SetShake(Random.insideUnitSphere * _battleView.CameraShake);
        }

        /// <summary>One-finger drag pans, two-finger pinch zooms (and pans), mouse wheel zooms. Taps come out on release.</summary>
        private void ReadGestures()
        {
            PointerInput.GetPressedPointers(_pointers);
            _gesture = _gestures.Update(_pointers, IsOverUi);

            if (_gesture.Panned)
            {
                _rig.Pan(_gesture.PanFrom, _gesture.PanTo);
            }

            if (_gesture.Pinched)
            {
                _rig.Zoom(_gesture.PinchRatio, _gesture.PinchCenter);
            }

            var scroll = PointerInput.ScrollDelta();
            if (Mathf.Abs(scroll) > 0.01f && PointerInput.TryGetHover(out var mouse) && !IsOverUi(mouse))
            {
                _rig.Zoom(Mathf.Pow(1.0015f, scroll), mouse);
            }
        }

        // ---------------- Dusk: building ----------------

        private void UpdateDusk()
        {
            if (!_gestures.IsDragging && PointerInput.TryGetHover(out var hover) && !IsOverUi(hover) && TryScreenToTile(hover, out var hoverTile))
            {
                _board.ShowCursor(hoverTile, CanToggle(hoverTile, out _));
            }
            else
            {
                _board.ShowCursor(null, false);
            }

            if (!_gesture.Tapped || !TryScreenToTile(_gesture.TapPosition, out var tile))
            {
                return;
            }

            if (!CanToggle(tile, out var reason))
            {
                Announce(reason);
                return;
            }

            var existing = _towers.FindIndex(t => t.Position == tile);
            if (existing >= 0)
            {
                _scrap += TowerCost[_towers[existing].Type];
                _towers.RemoveAt(existing);
            }
            else
            {
                _scrap -= TowerCost[_selectedTower.Value];
                _towers.Add(new PlacedTower(_selectedTower.Value, tile));
            }

            RefreshBoard();
        }

        /// <summary>Tap on an empty tile builds; tap on a tower demolishes (full refund — the cult is frugal).</summary>
        private bool CanToggle(GridPosition tile, out string reason)
        {
            reason = null;
            if (_towers.Any(t => t.Position == tile))
            {
                return true;
            }

            if (tile == DemoMap.Spawn || tile == DemoMap.Hearth || _rocks.Contains(tile))
            {
                reason = "Can't build there.";
                return false;
            }

            if (_selectedTower == null || _scrap < TowerCost[_selectedTower.Value])
            {
                reason = "Not enough Scrap. Lord Wulfric's horseshoes won't salvage themselves.";
                return false;
            }

            try
            {
                Layout(_towers.Append(new PlacedTower(_selectedTower.Value, tile))).Validate();
            }
            catch (InvalidLayoutException)
            {
                reason = "That would seal the road. The Bishop's lawyers would have a field day.";
                return false;
            }

            return true;
        }

        private void RefreshBoard()
        {
            _board.SetTowers(_towers);
            _board.ShowRoad(Layout(_towers).ResolveEnemyPath());
        }

        public void StartNight()
        {
            _setup = new BattleSetup(Layout(_towers), (ulong)(946 + _night), RaidBudget, RaidWaves, new List<PlayerInput>());
            _battle = new Battle(_setup);
            _battleView.Begin(_battle);
            _board.ShowCursor(null, false);
            _accumulator = 0f;
            _phase = GamePhase.Night;
            Announce("Night falls. Tap the ground to move the Prophet.");
        }

        // ---------------- Night: live battle ----------------

        private void UpdateNight()
        {
            if (_gesture.Tapped && TryScreenToTile(_gesture.TapPosition, out var tile))
            {
                _battle.Intervene(PlayerActionType.MoveLeader, tile);
            }

            _accumulator += Time.deltaTime * _speed;
            var steps = 0;
            while (_accumulator >= TickSeconds && !_battle.IsFinished && steps++ < MaxStepsPerFrame)
            {
                _battleView.BeforeStep();
                _battle.Step();
                _battleView.AfterStep();
                _accumulator -= TickSeconds;
            }

            _board.ShowLeaderDestination(_battle.Leader.IsMoving ? _battle.Leader.Destination : (GridPosition?)null);
            _battleView.Render(Mathf.Clamp01(_accumulator / TickSeconds));

            if (_battle.IsFinished)
            {
                EndNight();
            }
        }

        private void EndNight()
        {
            // What the server does with /api/combat/resolve: replay the submitted input log and compare.
            _serverReplay = Battle.Simulate(_setup with { Inputs = _battle.InputLog.ToList() });
            LastNightVerified = _serverReplay == _battle.Result;
            _board.ShowLeaderDestination(null);
            _phase = GamePhase.Dawn;
            _announcement = null;
        }

        private void BackToDusk()
        {
            _battleView.Clear();
            _battle = null;
            _night++;
            _phase = GamePhase.Dusk;
            Announce("Dawn. Rebuild, rearrange, prepare for tomorrow's disappointment.");
        }

        // ---------------- Helpers ----------------

        private static BattleLayout Layout(IEnumerable<PlacedTower> towers) =>
            new BattleLayout(DemoMap.Width, DemoMap.Height, DemoMap.Spawn, DemoMap.Hearth, towers, DemoMap.CreateRocks());

        private void SetUpCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                _camera = new GameObject("Main Camera").AddComponent<Camera>();
                _camera.tag = "MainCamera";
            }

            var center = new Vector3((DemoMap.Width - 1) / 2f, 0f, -(DemoMap.Height - 1) / 2f);
            _camera.orthographic = false;
            _camera.fieldOfView = 44f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Shapes.Hex("#14110d");
            _rig = new CameraRig(
                _camera,
                focus: center + new Vector3(0f, 0f, -0.6f),
                offset: new Vector3(0f, 16.5f, -10.4f),
                boardMin: new Vector2(0f, -(DemoMap.Height - 1)),
                boardMax: new Vector2(DemoMap.Width - 1, 0f));

            // ~2 mm of finger wobble still counts as a tap (dpi is 0 on some devices; fall back to screen height).
            var slop = Screen.dpi > 0 ? Screen.dpi * 0.08f : Screen.height * 0.02f;
            _gestures = new GestureRecognizer(slop);
        }

        private bool TryScreenToTile(Vector2 screen, out GridPosition tile)
        {
            tile = default;
            if (!_rig.TryScreenToGround(screen, out var point))
            {
                return false;
            }

            tile = new GridPosition(Mathf.RoundToInt(point.x), Mathf.RoundToInt(-point.z));
            return tile.X >= 0 && tile.Y >= 0 && tile.X < DemoMap.Width && tile.Y < DemoMap.Height;
        }

        private void Announce(string text)
        {
            _announcement = text;
            _announcementUntil = Time.time + 3.5f;
        }

        /// <summary>IMGUI buttons don't block world taps, so remember where we drew UI last frame.</summary>
        private bool IsOverUi(Vector2 screen)
        {
            var gui = new Vector2(screen.x, Screen.height - screen.y) / _guiScale;
            return _uiRects.Any(r => r.Contains(gui));
        }

        // ---------------- HUD (IMGUI: zero setup, good enough for a prototype) ----------------

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                _uiRects.Clear();
            }

            EnsureStyles();
            _guiScale = Screen.height / ReferenceScreenHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1f));
            _safe = SafeGuiRect();
            var width = _safe.width;

            // Everything below is laid out relative to the safe rect, clear of cutouts and rounded corners.
            GUI.BeginGroup(_safe);
            DrawHeader();
            switch (_phase)
            {
                case GamePhase.Dusk:
                    DrawDuskBar(width);
                    break;
                case GamePhase.Night:
                    DrawNightBar(width);
                    break;
                case GamePhase.Dawn:
                    DrawDawnCard(width);
                    break;
            }

            if (_announcement != null && Time.time < _announcementUntil)
            {
                GUI.Label(new Rect(width / 2f - 330f, 70f, 660f, 30f), _announcement, _label);
            }

            GUI.EndGroup();
        }

        /// <summary>Screen.safeArea (pixels, bottom-left origin) plus a corner margin, converted to scaled GUI space.</summary>
        private Rect SafeGuiRect()
        {
            var area = Screen.safeArea;
            var margin = Screen.height * CornerMargin;
            var left = (area.xMin + margin) / _guiScale;
            var right = (Screen.width - area.xMax + margin) / _guiScale;
            var top = (Screen.height - area.yMax + margin * 0.5f) / _guiScale;
            var bottom = (area.yMin + margin * 0.5f) / _guiScale;
            return new Rect(left, top, Screen.width / _guiScale - left - right, Screen.height / _guiScale - top - bottom);
        }

        private void DrawHeader()
        {
            var phase = _phase == GamePhase.Dusk ? $"Dusk · Night {_night}" : _phase == GamePhase.Night ? $"Night {_night}" : "Dawn";
            Panel(new Rect(0f, 0f, 350f, 94f));
            GUI.Label(new Rect(12f, 4f, 320f, 30f), "Cult of Comfort", _title);
            if (Button(new Rect(360f, 0f, 110f, 44f), "Recenter"))
            {
                _rig.Reset();
            }

            GUI.Label(new Rect(12f, 30f, 320f, 24f), _phase == GamePhase.Dusk ? $"{phase} · Scrap {_scrap}" : phase, _label);

            if (_phase == GamePhase.Dusk && DeterminismProbe.Matches.HasValue)
            {
                var probe = DeterminismProbe.Matches.Value ? "Deterministic: OK" : "Determinism: MISMATCH";
                GUI.Label(new Rect(12f, 56f, 330f, 24f), $"{probe} · {DeterminismProbe.LastHash}", _small);
            }

            if (_battle != null)
            {
                var fraction = _battle.HearthHealth / (float)_battle.HearthMaxHealth;
                GUI.Label(new Rect(12f, 54f, 120f, 24f), $"Hearth {_battle.HearthHealth}", _label);
                GUI.DrawTexture(new Rect(118f, 63f, 210f, 8f), Texture2D.grayTexture);
                var previous = GUI.color;
                GUI.color = Color.Lerp(Palette.Invalid, Palette.Ember, fraction);
                GUI.DrawTexture(new Rect(118f, 63f, 210f * fraction, 8f), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        private void DrawDuskBar(float width)
        {
            var y = _safe.height - 70f;
            Panel(new Rect(0f, y - 8f, width, 72f));
            var x = 12f;
            foreach (var type in new[] { TowerType.SaplingFlinger, TowerType.SlurryChute })
            {
                var label = (type == TowerType.SaplingFlinger ? "Sapling Flinger" : "Slurry Chute") + $"  ·  {TowerCost[type]}";
                var selected = _selectedTower == type;
                if (Button(new Rect(x, y, 250f, 56f), selected ? $"▶ {label}" : label))
                {
                    _selectedTower = type;
                }

                x += 260f;
            }

            GUI.Label(new Rect(x + 6f, y + 4f, width - x - 266f, 50f), "Tap empty ground to build, tap a tower to demolish.", _label);
            if (Button(new Rect(width - 248f, y, 236f, 56f), "Light the Lamps  →"))
            {
                StartNight();
            }
        }

        private void DrawNightBar(float width)
        {
            var y = _safe.height - 70f;
            Panel(new Rect(0f, y - 8f, width, 72f));

            var sluice = _battle.IsSluiceReady ? $"Open Sluice  ·  {_battle.SluiceChargesLeft} left"
                : _battle.SluiceChargesLeft == 0 ? "Cistern empty" : $"Refilling… {Seconds(_battle.SluiceReadyAtTick)}s";
            if (Button(new Rect(12f, y, 230f, 56f), sluice))
            {
                _battle.Intervene(PlayerActionType.OpenSluiceGate);
            }

            var voice = _battle.IsVoiceReady ? "Phantom Voice" : $"Hoarse… {Seconds(_battle.Leader.VoiceReadyAtTick)}s";
            if (Button(new Rect(252f, y, 230f, 56f), voice))
            {
                _battle.Intervene(PlayerActionType.PhantomVoice);
            }

            if (Button(new Rect(492f, y, 90f, 56f), $"{_speed}×"))
            {
                _speed = _speed == 1 ? 2 : _speed == 2 ? 4 : 1;
            }

            var alive = _battle.Enemies.Count(e => e.IsActive);
            GUI.Label(new Rect(598f, y + 4f, width - 610f, 50f),
                $"Wave {Mathf.Max(1, _battle.CurrentWave)}/{_battle.WaveCount} · {alive} on the road · Tap the ground to move the Prophet.", _label);
        }

        private void DrawDawnCard(float width)
        {
            var result = _battle.Result;
            var rect = new Rect(width / 2f - 280f, _safe.height / 2f - 170f, 560f, 340f);
            Panel(rect);
            var won = result.Outcome == BattleOutcome.Won;
            GUI.Label(new Rect(rect.x + 24f, rect.y + 18f, 520f, 40f), won ? "The Hearth stands" : "The Hearth went cold", _title);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 56f, 520f, 30f),
                $"Stars {result.Stars}/3   ·   Hearth {result.HearthHealth}/{result.HearthMaxHealth}", _label);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 86f, 520f, 60f), won
                ? "The attackers left damp, confused and noticeably cleaner."
                : "Wulfric's men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.", _label);

            var match = _serverReplay == result;
            GUI.Label(new Rect(rect.x + 24f, rect.y + 150f, 520f, 110f),
                $"{result.EnemiesDefeated} defeated · {result.EnemiesLeaked} reached the Hearth · {_battle.InputLog.Count} commands logged\n" +
                $"Client hash:   {result.StateHashHex}\n" +
                $"Server replay: {_serverReplay.StateHashHex}  {(match ? "VERIFIED" : "MISMATCH")}", _label);

            if (Button(new Rect(rect.x + 24f, rect.yMax - 72f, 512f, 52f), "Back to Dusk"))
            {
                BackToDusk();
            }
        }

        private float Seconds(int readyAtTick) => Mathf.Ceil((readyAtTick - _battle.Tick) / (float)BalanceTable.TicksPerSecond);

        private bool Button(Rect rect, string text)
        {
            _uiRects.Add(new Rect(rect.position + _safe.position, rect.size));
            return GUI.Button(rect, text, _button);
        }

        private void Panel(Rect rect)
        {
            _uiRects.Add(new Rect(rect.position + _safe.position, rect.size));
            GUI.Box(rect, GUIContent.none, _box);
        }

        private void EnsureStyles()
        {
            if (_label != null)
            {
                return;
            }

            var panel = new Texture2D(1, 1);
            panel.SetPixel(0, 0, new Color(0.09f, 0.08f, 0.06f, 0.88f));
            panel.Apply();

            _box = new GUIStyle(GUI.skin.box) { normal = { background = panel } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, normal = { textColor = Shapes.Hex("#e9e1d3") } };
            _small = new GUIStyle(_label) { fontSize = 14, normal = { textColor = Shapes.Hex("#9c907d") } };
            _title = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Palette.Ember } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        }
    }
}
