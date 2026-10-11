using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using UnityEngine;
using UnityEngine.UIElements;

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

        /// <summary>Portrait turns the board so its long axis runs up the screen: spawn on top, Hearth at the bottom.</summary>
        private const float PortraitYaw = -90f;

        private static readonly Dictionary<TowerType, int> TowerCost = new Dictionary<TowerType, int>
        {
            [TowerType.SaplingFlinger] = 70,
            [TowerType.SlurryChute] = 50,
        };

        private readonly List<PlacedTower> _towers = new List<PlacedTower>();
        private IReadOnlyList<GridPosition> _rocks;
        private readonly List<Vector2> _pointers = new List<Vector2>();
        private Camera _camera;
        private UIDocument _document;
        private readonly TipBook _tips = new TipBook(new PlayerPrefsTipStore());
        private const float TipSeconds = 7f;
        private float _tipUntil;
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
        private HudView _hud;
        private Vector2Int _screenSize;
        private int _framePending;
        private bool _aimingStones;

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
            _battleView.Floater += FloatNote;
            _battleView.SkillUsed += ShowFirstUseTip;

            SetUpCamera();
            SetUpHud();
            RefreshBoard();
            Announce("Tap a tile to build. Towers reroute the road — use that.");
        }

        private void Update()
        {
            TrackOrientation();
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
            UpdateHud();
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
            // Mouse: the tile under the cursor. Touch: the tile under the finger, before release, so you see what you'll hit.
            var hasPointer = _gestures.TryGetPendingTap(out var hover) || (!_gestures.IsDragging && PointerInput.TryGetHover(out hover) && !IsOverUi(hover));
            if (hasPointer && TryScreenToTile(hover, out var hoverTile))
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
                Say(reason, prophet: true);
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
            _framePending = 2; // the Night strip is taller than the Dusk one: re-fit the board once the HUD has laid out
            _board.ShowCursor(null, false);
            _accumulator = 0f;
            _phase = GamePhase.Night;
            _hud.ShowPhase(_phase);
            Announce("Night falls. Tap the ground to move the Prophet.");
        }

        // ---------------- Night: live battle ----------------

        private void UpdateNight()
        {
            if (_aimingStones && !_battle.IsHotStonesReady)
            {
                SetAimingStones(false); // He fainted mid-aim.
            }

            if (_gesture.Tapped && TryScreenToTile(_gesture.TapPosition, out var tile))
            {
                if (_aimingStones)
                {
                    _battle.Intervene(PlayerActionType.HotStones, tile);
                    SetAimingStones(false);
                }
                else
                {
                    _battle.Intervene(PlayerActionType.MoveLeader, tile);
                }
            }

            ShowStonesTarget();

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

        private void ToggleAimingStones()
        {
            if (_battle == null || !_battle.IsHotStonesReady)
            {
                return;
            }

            SetAimingStones(!_aimingStones);
            if (_aimingStones)
            {
                Announce("Hot Stone Therapy: tap a tile inside the pebble ring.");
            }
        }

        private void SetAimingStones(bool aiming)
        {
            _aimingStones = aiming;
            _battleView.ShowReach(aiming);
            if (!aiming)
            {
                _board.ShowCursor(null, false);
            }
        }

        /// <summary>While aiming, highlight the tile under the cursor or finger: amber in reach, red out of it.</summary>
        private void ShowStonesTarget()
        {
            if (!_aimingStones)
            {
                return;
            }

            var hasPointer = _gestures.TryGetPendingTap(out var pointer) || (PointerInput.TryGetHover(out pointer) && !IsOverUi(pointer));
            if (!hasPointer || !TryScreenToTile(pointer, out var tile))
            {
                _board.ShowCursor(null, false);
                return;
            }

            var reach = (float)BalanceTable.Default.HotStones.Reach.ToDouble();
            var leader = Shapes.ToWorld(_battle.Leader.Position);
            _board.ShowCursor(tile, (Shapes.ToWorld(tile) - leader).sqrMagnitude <= reach * reach);
        }

        private void EndNight()
        {
            SetAimingStones(false);
            // What the server does with /api/combat/resolve: replay the submitted input log and compare.
            _serverReplay = Battle.Simulate(_setup with { Inputs = _battle.InputLog.ToList() });
            LastNightVerified = _serverReplay == _battle.Result;
            _board.ShowLeaderDestination(null);
            _phase = GamePhase.Dawn;
            _hud.ShowPhase(_phase);
            _hud.ShowDawn(_battle.Result, _serverReplay, _battle.InputLog.Count);
            _hud.Announce(string.Empty, Time.time, 0f);
        }

        private void BackToDusk()
        {
            _battleView.Clear();
            _battle = null;
            _framePending = 2;
            _night++;
            _phase = GamePhase.Dusk;
            _hud.ShowPhase(_phase);
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

            _camera.orthographic = false;
            _camera.fieldOfView = 44f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Shapes.Hex("#14110d");
            _rig = new CameraRig(
                _camera,
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

        /// <summary>World announcements and Prophet quips wait while a first-use tip is up: a tip is shown once ever.</summary>
        private void Announce(string text) => Say(text, prophet: false);

        private void Say(string text, bool prophet)
        {
            if (Time.time < _tipUntil)
            {
                return;
            }

            _hud.Announce(text, Time.time, prophet: prophet);
        }

        private void ShowFirstUseTip(PlayerActionType skill)
        {
            var tip = _tips.Take(skill);
            if (tip != null)
            {
                _hud.Announce(tip, Time.time, TipSeconds, prophet: true);
                _tipUntil = Time.time + TipSeconds;
            }
        }

        /// <summary>Projects a world point into the HUD panel and floats a note there.</summary>
        private void FloatNote(Vector3 world, string text)
        {
            var screen = _camera.WorldToScreenPoint(world);
            var panel = _document.rootVisualElement.panel;
            if (screen.z <= 0f || panel == null)
            {
                return;
            }

            _hud.Float(text, RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)), Time.time);
        }

        // ---------------- HUD & orientation ----------------

        private void SetUpHud()
        {
            // Created in code (not an asset) to keep the project code-first. Constant physical size: 1 unit ≈ 1 dp.
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/CocTheme");
            settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            settings.referenceDpi = 160f;
            settings.fallbackDpi = 160f;

            var document = gameObject.AddComponent<UIDocument>();
            _document = document;
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Hud"));

            _hud = new HudView(root, new Dictionary<TowerType, (string, int)>
            {
                [TowerType.SaplingFlinger] = ("Sapling Flinger", TowerCost[TowerType.SaplingFlinger]),
                [TowerType.SlurryChute] = ("Slurry Chute", TowerCost[TowerType.SlurryChute]),
            });
            _hud.TowerSelected += type => _selectedTower = type;
            _hud.StartNightClicked += StartNight;
            _hud.SluiceClicked += () => _battle?.Intervene(PlayerActionType.OpenSluiceGate);
            _hud.VoiceClicked += () => _battle?.Intervene(PlayerActionType.PhantomVoice);
            _hud.StonesClicked += ToggleAimingStones;
            _hud.SpeedClicked += () => _speed = _speed == 1 ? 2 : _speed == 2 ? 4 : 1;
            _hud.RecenterClicked += () => _rig.Reset();
            _hud.BackToDuskClicked += BackToDusk;
            _hud.ResetTipsClicked += () =>
            {
                _tips.Reset();
                Announce("Tips are back. The Prophet clears his throat.");
            };
            _hud.ShowPhase(_phase);
        }

        private void UpdateHud()
        {
            _hud.Tick(Time.time);
            _hud.UpdateFrame(DemoEconomy.Frame(_scrap, 1));
            if (_phase == GamePhase.Dusk)
            {
                var probe = DeterminismProbe.Matches switch
                {
                    true => $"Deterministic: OK · {DeterminismProbe.LastHash}",
                    false => $"Determinism: MISMATCH · {DeterminismProbe.LastHash}",
                    _ => string.Empty,
                };
                _hud.UpdateDusk(_night, _scrap, _selectedTower, TowerCost, probe);
            }
            else if (_phase == GamePhase.Night)
            {
                var sluice = _battle.IsSluiceReady ? $"Open Sluice · {_battle.SluiceChargesLeft}"
                    : _battle.SluiceChargesLeft == 0 ? "Cistern empty" : $"Sluice {Seconds(_battle.SluiceReadyAtTick)}s";
                var leader = _battle.Leader;
                var swooning = leader.IsSwooning(_battle.Tick);
                var voice = swooning ? "Fainted" : _battle.IsVoiceReady ? "Phantom Voice" : $"Hoarse {Seconds(leader.VoiceReadyAtTick)}s";
                var stones = swooning ? "Fainted" : _aimingStones ? "Pick a tile" : _battle.IsHotStonesReady ? "Hot Stones" : $"Heating {Seconds(leader.HotStonesReadyAtTick)}s";
                var alive = _battle.Enemies.Count(e => e.IsActive);
                var hint = swooning ? $"The Prophet is face down in the mud. Up in {Seconds(leader.SwoonedUntilTick)}s: tap where he should run."
                    : _aimingStones ? "Tap a tile inside the pebble ring, or Hot Stones again to cancel."
                    : "Tap the ground to move the Prophet. Enemies stop to fight him.";
                _hud.UpdateNight(new NightHudState(
                    _night, _battle.HearthHealth, _battle.HearthMaxHealth, leader.Health, leader.MaxHealth,
                    sluice, _battle.IsSluiceReady, voice, _battle.IsVoiceReady, stones, _battle.IsHotStonesReady || _aimingStones, _aimingStones, _speed,
                    _battle.CurrentWave, _battle.WaveCount, alive, hint));
            }
        }

        /// <summary>
        /// On first frame and on every rotation: restack the HUD, then re-frame the camera once UI Toolkit has laid
        /// the new HUD out (a couple of frames later), so the board fits between the status panel and the bar.
        /// </summary>
        private void TrackOrientation()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _screenSize)
            {
                _screenSize = size;
                _hud.SetPortrait(size.y > size.x);
                _framePending = 2;
            }

            if (_framePending > 0 && --_framePending == 0)
            {
                var portrait = size.y > size.x;
                var fallback = portrait ? Rect.MinMaxRect(0.03f, 0.25f, 0.97f, 0.85f) : Rect.MinMaxRect(0.03f, 0.2f, 0.97f, 0.8f);
                _rig.Frame(portrait ? PortraitYaw : 0f, _hud.ClearViewport() ?? fallback);
                _rig.Reset();
            }
        }

        private float Seconds(int readyAtTick) => Mathf.Ceil((readyAtTick - _battle.Tick) / (float)BalanceTable.TicksPerSecond);


        private bool IsOverUi(Vector2 screen) => _hud.IsOverUi(screen);
    }
}
