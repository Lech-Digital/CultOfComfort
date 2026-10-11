using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using System.Threading;
using System.Threading.Tasks;
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
        private const int RaidBudget = 54;
        private const int RaidWaves = 4;

        /// <summary>Portrait turns the board so its long axis runs up the screen: spawn on top, Hearth at the bottom.</summary>
        private const float PortraitYaw = -90f;

        private const float VillageRefreshSeconds = 0.5f;

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
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private IBaseGateway _village;
        private BaseSnapshot _snapshot;
        private long _roadVersion = -1;
        private BuildingType? _buildSelection;
        private int? _cardBuilding;
        private float _villageRefreshAt;
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
            _battleView.StructureDamaged += (id, fraction, wrecked) => _board.ShowStructureHealth(id, fraction, wrecked);

            SetUpCamera();
            SetUpHud();
            _village = VillageComposition.CreateGateway();
            LoadVillage();
            Announce("Build from the drawer below; tap a building to collect or upgrade it.");
        }

        private void OnDestroy() => _cts.Cancel();

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
            _rig.SetShake(UnityEngine.Random.insideUnitSphere * _battleView.CameraShake);
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

        // ---------------- Day: the village ----------------

        private void UpdateDusk()
        {
            // Mouse: the tile under the cursor. Touch: the tile under the finger, before release, so you see what you'll hit.
            var hasPointer = _gestures.TryGetPendingTap(out var hover) || (!_gestures.IsDragging && PointerInput.TryGetHover(out hover) && !IsOverUi(hover));
            if (hasPointer && TryScreenToTile(hover, out var hoverTile))
            {
                _board.ShowCursor(hoverTile, LooksBuildable(hoverTile));
            }
            else
            {
                _board.ShowCursor(null, false);
            }

            if (Time.time >= _villageRefreshAt)
            {
                _villageRefreshAt = Time.time + VillageRefreshSeconds;
                RefreshSnapshot();
            }

            if (_gesture.Tapped && TryScreenToTile(_gesture.TapPosition, out var tile))
            {
                HandleDayTap(tile);
            }
        }

        /// <summary>A quick local hint for the cursor; the gateway has the final word (e.g. sealing the road).</summary>
        private bool LooksBuildable(GridPosition tile)
        {
            if (_snapshot == null)
            {
                return false;
            }

            if (_buildSelection == null)
            {
                return _snapshot.BuildingAt(tile) != null;
            }

            return tile != DemoMap.Spawn && tile != DemoMap.Hearth && !_rocks.Contains(tile) && _snapshot.BuildingAt(tile) == null;
        }

        /// <summary>Build with the selected card, otherwise collect from a building or open its card.</summary>
        private async void HandleDayTap(GridPosition tile)
        {
            try
            {
                Accept(await _village.LoadAsync(_cts.Token)); // a tap always sees what is ready right now
                if (_buildSelection != null)
                {
                    var type = _buildSelection.Value;
                    var outcome = await SendVillage(new PlaceBuilding(Guid.NewGuid(), type, tile));
                    if (outcome.Status == CommandStatus.Accepted && !BuildingTable.Default.Spec(type).IsTower)
                    {
                        _buildSelection = null; // one building per pick; towers stay selected for a row of them
                        RefreshVillage();
                    }

                    return;
                }

                var building = _snapshot.BuildingAt(tile);
                if (building == null)
                {
                    _cardBuilding = null;
                    RefreshVillage();
                    return;
                }

                if (building.Ready >= 1 && !building.Wrecked) // a wreck always opens its card (to repair it)
                {
                    var outcome = await SendVillage(new Collect(Guid.NewGuid(), building.Id));
                    if (outcome.Status == CommandStatus.Accepted)
                    {
                        FloatNote(Shapes.ToWorld(tile) + Vector3.up * 1.4f, $"+{outcome.Collected}");
                    }

                    // Storage full (rejected) or only part of it fitted: open the card so it can still be upgraded or demolished.
                    var after = _snapshot.Buildings.FirstOrDefault(b => b.Id == building.Id);
                    if (outcome.Status == CommandStatus.Accepted && (after == null || after.Ready < 1))
                    {
                        return;
                    }
                }

                _cardBuilding = building.Id;
                RefreshVillage();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        /// <summary>Every village change goes through the gateway (device today, server later).</summary>
        private async Task<CommandOutcome> SendVillage(BaseCommand command)
        {
            var result = await _village.SendAsync(command, _cts.Token);
            Accept(result.Snapshot);
            if (result.Outcome.Status == CommandStatus.Rejected)
            {
                Say(result.Outcome.Reason, prophet: true);
            }

            RefreshVillage();
            return result.Outcome;
        }

        private async void LoadVillage()
        {
            try
            {
                Accept(await _village.LoadAsync(_cts.Token));
                _night = _snapshot.LastNightRecorded + 1; // carry on where the save left off
                RefreshVillage();
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                VillageFailed(e);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>Takes a snapshot only if it is newer than what is on screen (a slow read must not roll it back).</summary>
        private void Accept(BaseSnapshot snapshot)
        {
            if (VillagePresenter.IsNewer(snapshot, _snapshot))
            {
                _snapshot = snapshot;
            }
        }

        private void VillageFailed(Exception e)
        {
            Debug.LogException(e);
            Say("The village didn't answer. The Prophet taps the tin can and tries again.", prophet: true);
        }

        private async void RefreshSnapshot()
        {
            try
            {
                if (_village != null)
                {
                    Accept(await _village.LoadAsync(_cts.Token));
                    RefreshVillage();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        private async void SendFromCard(Func<BuildingView, BaseCommand> command, bool closeCard = false)
        {
            try
            {
                if (_cardBuilding == null || _snapshot == null)
                {
                    return;
                }

                await SendVillage(command(_snapshot.Building(_cardBuilding.Value)));
                if (closeCard)
                {
                    _cardBuilding = null;
                    RefreshVillage();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        /// <summary>Pushes the latest snapshot into the board and the HUD; the road only when the layout changed.</summary>
        private void RefreshVillage()
        {
            if (_snapshot == null)
            {
                return;
            }

            _board.SetBuildings(_snapshot.Buildings, _snapshot.HearthLevel, BuildingTable.Default, _snapshot.AsOfUtc);
            if (_snapshot.Version != _roadVersion)
            {
                _roadVersion = _snapshot.Version;
                _board.ShowRoad(VillageMap.Demo.ToBattleLayout(_snapshot, BuildingTable.Default).ResolveEnemyPath());
            }

            _hud.SetBuildCards(VillagePresenter.BuildCards(_snapshot, BuildingTable.Default), _buildSelection);
            var hasCard = _cardBuilding != null && _snapshot.Buildings.Any(b => b.Id == _cardBuilding.Value);
            _hud.ShowBuildingCard(hasCard ? VillagePresenter.Card(_snapshot, BuildingTable.Default, _cardBuilding.Value) : null);
        }

        public void StartNight()
        {
            _buildSelection = null;
            _cardBuilding = null;
            _hud.ShowBuildingCard(null);
            var layout = _snapshot != null ? VillageMap.Demo.ToBattleLayout(_snapshot, BuildingTable.Default) : VillageMap.Demo.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>());
            _setup = new BattleSetup(layout, (ulong)(946 + _night), RaidBudget, RaidWaves, new List<PlayerInput>())
            {
                ExtraSluiceCharges = _snapshot?.SluiceBonus ?? 0,
            };
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

        private async void BackToDusk()
        {
            var stars = _battle?.Result?.Stars ?? 0;
            var wrecked = _battle?.Structures.Where(s => s.IsWrecked).Select(s => s.Id).ToList() ?? new List<int>();
            var night = _night;
            _battleView.Clear();
            _battle = null;
            _framePending = 2;
            _night++;
            _phase = GamePhase.Dusk;
            _hud.ShowPhase(_phase);
            Announce("Dawn. Rebuild, rearrange, prepare for tomorrow's disappointment.");
            try
            {
                var outcome = await SendVillage(new RecordNightResult(Guid.NewGuid(), night, stars, wrecked));
                if (outcome.Status == CommandStatus.Accepted && outcome.Collected.Scrap > 0)
                {
                    Announce($"Dawn. {outcome.Collected.Scrap} Scrap salvaged from Wulfric's dropped horseshoes.");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        // ---------------- Helpers ----------------

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

            _hud = new HudView(root);
            _hud.BuildCardSelected += type =>
            {
                _buildSelection = type;
                _cardBuilding = null;
                RefreshVillage();
            };
            _hud.UpgradeClicked += () => SendFromCard(b => b.Type == BuildingType.Hearth ? (BaseCommand)new UpgradeHearth(Guid.NewGuid()) : new UpgradeBuilding(Guid.NewGuid(), b.Id));
            _hud.DemolishClicked += () => SendFromCard(b => new DemolishBuilding(Guid.NewGuid(), b.Id), closeCard: true);
            _hud.FinishNowClicked += () => SendFromCard(_ => new FinishConstructionNow(Guid.NewGuid()));
            _hud.RepairClicked += () => SendFromCard(b => new RepairBuilding(Guid.NewGuid(), b.Id));
            _hud.BuildingCardClosed += () => _cardBuilding = null;
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
            _hud.UpdateFrame(_snapshot != null ? VillagePresenter.Frame(_snapshot) : DemoEconomy.Frame(0, 1));
            if (_phase == GamePhase.Dusk)
            {
                var probe = DeterminismProbe.Matches switch
                {
                    true => $"Deterministic: OK · {DeterminismProbe.LastHash}",
                    false => $"Determinism: MISMATCH · {DeterminismProbe.LastHash}",
                    _ => string.Empty,
                };
                _hud.UpdateDusk(_night, probe);
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
