using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Contracts;
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
        private NightMood _mood;
        private float _darkness;
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
        private PlacementTool _tool;     // the ghost of a building being placed or moved
        private readonly BubbleDirector _bubbles = new BubbleDirector();
        private int _barkRoll;
        private DragMode _dragMode;      // what the finger on the board is doing this gesture
        private bool _ghostShown;

        private enum DragMode { None, Pan, Ghost, Paint }
        private readonly List<GridPosition> _stroke = new List<GridPosition>(); // road tiles under the finger
        private int? _cardBuilding;
        private GridPosition? _cardTerrain;
        private bool _cardVisitor;          // the visitor's card is open (spec 2026-10-20 visitors)
        private long _greetedVisitorSlot = -1;
        private NightPlan _plan;
        private NightReport _lastReport;
        private bool _showingReport;
        private int _openLevel;
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
        private PlayerActionType? _aim; // an aimed skill is armed: the next tap on the board says where (Hot Stones, laser, glass)
        private IntroDirector _intro;
        private AudioDirector _audio;
        private WorldMapView _map;
        private WorldStage _stage;            // the world map in 3D (spec 2026-10-15 real map)
        private bool _mapHeld;
        private WorldMapModel _mapModel;
        private bool _pendingTithe;
        private MissionHouseView _house;
        private LordHallView _hall;
        private int _sendPreacher;            // who the next sermon is for (picked in the Mission House or on the card); 0: the first free
        private int _cardPreacher;
        private int _cardNode;
        private int _seenDemand = int.MinValue; // the demand last seen (MinValue: nothing seen yet, so no toast on launch)
        private int _metDemand = -1;
        private int _pendingFocus = -1;      // a missionary to fly to once the map's land is up
        private readonly List<(Vector2 At, Color Colour)> _miniMarks = new List<(Vector2, Color)>();           // "Tithes" was tapped before the land arrived: open the lord's seat once it has
        private (int Q, int R, int Radius)? _stageWindow; // the window last asked for from where the map camera looks
        private WorldDto _world;              // the shared world's window, as the server last sent it (null: Marchlands, or not fetched yet)
        private int _worldRadius = SharedWorldPresenter.DefaultRadius;
        private float _worldFetchedAt = -999f;
        private bool _worldFetching;
        private FeatureSheetView _sheet;
        private bool _devTools; // the owner's testing gate is open on this device (lamps any time, dev buttons)

        public GamePhase Phase => _phase;

        /// <summary>True when the server-style replay of the last Night matched the live result.</summary>
        public bool? LastNightVerified { get; private set; }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // the screen never dims or locks while the game is open (owner 2026-10-15)
            PointerInput.Enable();
            _rocks = VillageMap.Demo.Rocks;

            _audio = gameObject.AddComponent<AudioDirector>();
            _board = gameObject.AddComponent<BoardView>();
            _board.LevelledUp += () => _audio.Play("build_done");
            _board.Build(_rocks);
            _battleView = gameObject.AddComponent<BattleView>();
            _battleView.Announcement += Announce;
            _battleView.Floater += FloatNote;
            _battleView.SkillUsed += ShowFirstUseTip;
            _battleView.StructureDamaged += (id, fraction, wrecked) => _board.ShowStructureHealth(id, fraction, wrecked);

            SetUpCamera();
            _mood = NightMood.Capture(_camera, NightMood.FindSun());
            SetUpHud();
            _village = VillageComposition.CreateGateway();
            // Dev tools: on a device that runs the rules itself (editor, tests), or one holding the owner's dev key.
            _devTools = _village is RemoteBaseGateway remoteVillage ? remoteVillage.Developer : Debug.isDebugBuild;
            _hud.SetDevTools(_devTools);
            LoadVillage(); // no opening toast: the coach and the errand note say what to do (spec 2026-10-16 one thing at a time)
            PlayIntroOnFirstLaunch();
        }

        /// <summary>The story so far (GDD 05): plays once per device, over the loading village; the HUD waits.</summary>
        private void PlayIntroOnFirstLaunch()
        {
            var progress = VillageComposition.CreateIntroProgress();
            if (!progress.Seen)
            {
                PlayIntro(progress);
            }
        }

        private void PlayIntro(IIntroProgress progress)
        {
            if (_intro != null || _phase != GamePhase.Dusk)
            {
                return;
            }

            _document.rootVisualElement.style.display = DisplayStyle.None;
            _intro = gameObject.AddComponent<IntroDirector>();
            _intro.Finished += () =>
            {
                progress.MarkSeen();
                _intro = null;
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
                _framePending = 2;
                IntroFinished?.Invoke();
            };
            _intro.Play(_camera, _document.panelSettings);
        }

        private async void ShowVillageCode()
        {
            if (!(_village is RemoteBaseGateway remote))
            {
                _hud.ShowVillageCode("This village lives on this device only.");
                return;
            }

            _hud.ShowVillageCode("Asking the server…");
            var code = await remote.RecoveryCodeAsync(_cts.Token);
            if (code == null)
            {
                _hud.ShowVillageCode("No connection. Try again when you're online.");
                return;
            }

            GUIUtility.systemCopyBuffer = code;
            _hud.ShowVillageCode(code, "Copied. Write it down: type it on a new phone to bring this village back. Showing a new code retires the old one.");
        }

        private async void RecoverVillage(string code)
        {
            if (!(_village is RemoteBaseGateway remote) || string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            _hud.ShowVillageCode("Looking for that village…");
            var moved = await remote.RecoverAsync(code.Trim(), _cts.Token);
            _hud.ShowRecoverResult(moved, moved ? "Welcome back. The village is yours again on this phone." : "No village with that code (or no connection).");
            if (moved)
            {
                _snapshot = null; // the recovered village may be at a lower version: don't let "newer only" keep the old one up
                _roadVersion = -1;
                RefreshSnapshot();
            }
        }

        // ---------------- the Marchlands (spec 2026-10-14 world map) ----------------

        private void SetUpMap(VisualElement root)
        {
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/WorldMap"));
            _map = new WorldMapView(root);
            _stage = WorldStage.Create();
            _map.Closed += CloseMap;
            _map.NodeTapped += id =>
            {
                _stage.Select(id);
                _stage.FlyTo(id, aboveCard: true);
                ShowPlaceCard(id);
            };
            _map.PreacherPicked += id =>
            {
                _sendPreacher = id;
                ShowPlaceCard(_cardNode);
            };
            _map.TitheRequested += OpenSeat;
            _map.MissionariesClicked += OpenMissionHouse;
            _map.DemandDelivered += DeliverDemand;
            _map.CardClosed += () => _stage.Select(null);
            _map.PreachClicked += async (node, hours) =>
            {
                var world = _world;
                var preacher = _cardPreacher;
                var outcome = await SendVillage(world != null
                    ? new SendWorldMissionary(Guid.NewGuid(), node, hours, SharedWorldPresenter.DistanceTo(world, node), preacher)
                    : (BaseCommand)new SendMissionary(Guid.NewGuid(), node, hours, preacher));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    _map.CloseCard();
                    _stage.Select(null);
                    _stage.GoHome(); // watch them set off (the card had pushed the view aside)
                    _sendPreacher = 0;
                    _map.SetStatus(null);
                    var name = world != null ? SharedWorldPresenter.MapFor(world).Node(node)?.Name : DomainMap.Marchlands.Node(node)?.Name;
                    Announce($"{(preacher > 0 ? Preachers.Name(preacher) : "A missionary")} sets off for {name}, with sandwiches.");
                }
            };
            _house = new MissionHouseView(root);
            _house.CardFor = (node, preacher) => _snapshot == null ? null
                : _world != null ? SharedWorldPresenter.Card(_snapshot, BuildingTable.Default, _world, node, preacher)
                : WorldMapPresenter.Card(_snapshot, BuildingTable.Default, node, preacher);
            _house.PreachClicked += async (node, hours, preacher) =>
            {
                var world = _world;
                var outcome = await SendVillage(world != null
                    ? new SendWorldMissionary(Guid.NewGuid(), node, hours, SharedWorldPresenter.DistanceTo(world, node), preacher)
                    : (BaseCommand)new SendMissionary(Guid.NewGuid(), node, hours, preacher));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce($"{(preacher > 0 ? Preachers.Name(preacher) : "A missionary")} sets off for {PlaceName(node, world != null)}, with sandwiches.");
                    RenderMissionHouse();
                }
                else
                {
                    Announce(outcome.Reason ?? "They won't go.");
                }
            };
            _house.TaleChosen += async (tale, choice) =>
            {
                var outcome = await SendVillage(new ChooseTale(Guid.NewGuid(), tale, choice));
                if (outcome.Status != CommandStatus.Accepted)
                {
                    Announce(outcome.Reason ?? "The story trails off.");
                    return;
                }

                Announce(outcome.Collected.IsZero ? "A good story, if nothing else." : $"Into the stores: {VillagePresenter.ShortCost(outcome.Collected)}.");
                RenderMissionHouse();
            };
            _house.MapClicked += () =>
            {
                _house.Show(false);
                OpenFeature("map");
            };
            _house.ShowClicked += node =>
            {
                _house.Show(false);
                _pendingFocus = node; // flown to once the land is up (it may still be on its way from the server)
                OpenFeature("map");
                if (_stage.IsOpen)
                {
                    _stage.FlyToMissionary(node);
                    _pendingFocus = -1;
                }
            };
            _map.CollectClicked += async () =>
            {
                if (_world != null && _village is RemoteBaseGateway remote)
                {
                    var collect = await remote.CollectWorldAsync(_cts.Token);
                    var taken = collect == null ? default : new ResourceBag(collect.timber, collect.peat, collect.scrap, collect.devotion);
                    Announce(collect == null ? "No connection: the offerings will wait."
                        : taken.IsZero ? collect.reason : $"The faithful send {VillagePresenter.ShortCost(taken)}.");
                    RefreshSnapshot();
                    _ = FetchWorldAsync(_world.viewQ, _world.viewR);
                    return;
                }

                var outcome = await SendVillage(new CollectOfferings(Guid.NewGuid()));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce($"The faithful of the Marchlands send {VillagePresenter.ShortCost(outcome.Collected)}.");
                }
            };
            _map.Zoomed += step => _stage.Rig.Zoom(step > 0 ? 0.7f : 1.43f, new Vector2(Screen.width / 2f, Screen.height / 2f));
            _map.Recentred += () => _stage.GoHome();
            _map.HomeClicked += CloseMap; // back to the village (the house at the top re-centres the map)
            _map.MinimapPicked += at => _stage.Rig.FlyTo(at);
            _map.TitheClicked += PayTithe;
            _hall = new LordHallView(root);
            _hall.DeliverClicked += DeliverDemand;
            _hall.TitheClicked += PayTithe;
            _hall.PleaClicked += Plead;
            _hall.ShowSeatClicked += () =>
            {
                _hall.Show(false);
                OpenFeature("seat");
            };
            _sheet = new FeatureSheetView(root);
            _sheet.ActionClicked += OnSheetAction;
            _hud.FeatureClicked += OpenFeature;
        }

        /// <summary>Opens a frame feature: the map (Map, Missionaries, Tithes) or a sheet (Research, Shop, Cult).</summary>
        private void OpenFeature(string id)
        {
                if (_phase != GamePhase.Dusk || _snapshot == null)
                {
                    return;
                }

                if (id == "research" || id == "shop" || id == "cult" || id == "prophet")
                {
                    CloseMap();
                    _house.Show(false);
                    _hall.Show(false);
                    _sheet.Show(id, SheetFor(id));
                    return;
                }

                if (id == "missionaries")
                {
                    OpenMissionHouse();
                    return;
                }

                if (id == "tithes")
                {
                    OpenHall();
                    return;
                }

                _sheet.Close();
                _house.Show(false);
                _hall.Show(false);
                _map.Show(true);
                RenderMap();
                if (_village is RemoteBaseGateway)
                {
                    _ = FetchWorldAsync(_world?.viewQ, _world?.viewR);
                }

                if (id == "seat")
                {
                    _pendingTithe = true;
                    OpenSeat();
                }
                else if (!Pref(MapHelpSeenKey))
                {
                    _map.ShowHelp(); // the first visit explains what the lands are for (owner 2026-10-14: "totally unclear")
                    SetPref(MapHelpSeenKey, true);
                }
        }

        /// <summary>The Mission House (spec 2026-10-15): the preachers, what they're doing, what the road told them.</summary>
        private void OpenMissionHouse()
        {
            if (_snapshot == null)
            {
                return;
            }

            CloseMap();
            _sheet.Close();
            _hall.Show(false);
            if (_world == null && _village is RemoteBaseGateway)
            {
                _ = FetchWorldAsync(null, null); // place names on the shared world come with it; the house redraws when it lands
            }

            RenderMissionHouse();
            _house.Show(true);
        }

        /// <summary>The lord's hall (spec 2026-10-16): his temper, his demand, what you can try on him, the tithe.</summary>
        private void OpenHall()
        {
            if (_snapshot == null)
            {
                return;
            }

            CloseMap();
            _sheet.Close();
            _house.Show(false);
            if (_world == null && _village is RemoteBaseGateway)
            {
                _ = FetchWorldAsync(null, null); // his name comes with the world; the hall redraws when it lands
            }

            RenderHall();
            _hall.Show(true);
        }

        private void RenderHall()
        {
            if (_snapshot == null)
            {
                return;
            }

            string lord;
            string seat;
            if (_world != null)
            {
                var map = SharedWorldPresenter.MapFor(_world);
                lord = SharedWorldPresenter.Authority(map, _world).Lord;
                seat = SharedWorldPresenter.Seat(map, _world)?.Name;
            }
            else if (_snapshot.Domain?.World == null)
            {
                lord = Ruler.Full;
                seat = DomainMap.Marchlands.Nodes.FirstOrDefault(n => n.Type == NodeType.Keep)?.Name;
            }
            else
            {
                lord = "the lord";
                seat = null;
            }

            _hall.Render(LordHallPresenter.Build(_snapshot, BuildingTable.Default, lord, seat, _snapshot.AsOfUtc));
        }

        private async void PayTithe(ResourceBag amount)
        {
            var hadRaid = _snapshot?.RaidWarningEndsAtUtc != null;
            var outcome = await SendVillage(new PayTithe(Guid.NewGuid(), amount));
            if (outcome.Status != CommandStatus.Accepted)
            {
                Announce(outcome.Reason ?? "He won't take it.");
                return;
            }

            _map.CloseCard();
            if (_mapModel != null && _mapModel.SeatId >= 0 && _stage.IsOpen)
            {
                _stage.SendTithe(_mapModel.SeatId); // a cart trundles off to his seat
            }

            var lord = LordName();
            Announce(hadRaid && _snapshot?.RaidWarningEndsAtUtc == null
                ? $"{lord} counts the money twice and calls the raid off. For now."
                : $"{lord} pockets the tithe. His temper cools a little.");
            if (_hall.IsOpen)
            {
                RenderHall();
            }
        }

        private async void Plead(int demandId, Plea plea)
        {
            var outcome = await SendVillage(new PleadWithLord(Guid.NewGuid(), demandId, plea));
            if (outcome.Status != CommandStatus.Accepted)
            {
                Announce(outcome.Reason ?? "He isn't listening.");
                return;
            }

            var demand = _snapshot?.Domain?.Demand;
            var lord = LordName();
            Announce(plea switch
            {
                Plea.Stall => $"{lord} drums his fingers. Fine: {Demands.StallHours} more hours.",
                Plea.Flatter => $"{lord} reads the poem twice. He's blushing.",
                _ when demand?.Haggle == DemandHaggle.Won => $"{lord} grumbles, and settles for {VillagePresenter.ShortCost(demand.Amount)}.",
                _ => $"{lord} is insulted. His temper flares (+{Demands.RefusePenalty}).",
            });
            if (_hall.IsOpen)
            {
                RenderHall();
            }
        }

        private void RenderMissionHouse()
        {
            if (_snapshot == null)
            {
                return;
            }

            var places = _world != null ? SharedWorldPresenter.Places(_snapshot, BuildingTable.Default, _world)
                : _snapshot.Domain?.World == null ? WorldMapPresenter.Places(_snapshot, BuildingTable.Default)
                : null; // on a shared world, waiting for the server
            _house.Render(MissionHousePresenter.Build(_snapshot, BuildingTable.Default, PlaceName, _snapshot.AsOfUtc, WalkSeconds, places));
        }

        /// <summary>The walk to a place, one way, in seconds.</summary>
        private double WalkSeconds(int nodeId, bool inWorld) => BuildingTable.Default.Domain.MinutesPerHex * 60.0
            * (inWorld ? (_world != null ? SharedWorldPresenter.DistanceTo(_world, nodeId) : 0) : DomainMap.Marchlands.Node(nodeId)?.Distance ?? 0);

        /// <summary>A place's name, on the shared world or in the Marchlands.</summary>
        private string PlaceName(int nodeId, bool inWorld) =>
            inWorld ? (_world != null ? SharedWorldPresenter.MapFor(_world).Node(nodeId)?.Name : null) ?? "a far place"
                : DomainMap.Marchlands.Node(nodeId)?.Name ?? "somewhere";

        /// <summary>The card for a place: preach options for the picked preacher; at the lord's seat, his demand and the tithe.</summary>
        private void ShowPlaceCard(int id)
        {
            if (_snapshot == null)
            {
                return;
            }

            var card = _world != null
                ? SharedWorldPresenter.Card(_snapshot, BuildingTable.Default, _world, id, _sendPreacher)
                : WorldMapPresenter.Card(_snapshot, BuildingTable.Default, id, _sendPreacher);
            if (_mapModel != null && id == _mapModel.SeatId)
            {
                WorldMapPresenter.AddTithe(card, _mapModel); // the tithe (and any demand) is paid at his seat
            }

            _cardNode = id;
            _cardPreacher = card.SelectedPreacher;
            _map.ShowCard(card);
        }

        private async void DeliverDemand(int demandId)
        {
            var hadRaid = _snapshot?.RaidWarningEndsAtUtc != null;
            var outcome = await SendVillage(new MeetDemand(Guid.NewGuid(), demandId));
            if (outcome.Status != CommandStatus.Accepted)
            {
                Announce(outcome.Reason ?? "He won't take it.");
                return;
            }

            _metDemand = demandId;
            _map.CloseCard();
            if (_mapModel != null && _mapModel.SeatId >= 0 && _stage.IsOpen)
            {
                _stage.SendTithe(_mapModel.SeatId); // the cart goes off with exactly what he asked for
            }

            var lord = LordName();
            Announce(hadRaid && _snapshot?.RaidWarningEndsAtUtc == null
                ? $"{lord} gets exactly what he asked for, and calls the raid off. He almost smiles."
                : $"{lord} gets exactly what he asked for. His temper cools a lot.");
            if (_hall.IsOpen)
            {
                RenderHall();
            }
        }

        private string LordName()
        {
            return Ruler.Cap(Ruler.Full);
        }

        /// <summary>The lord's demands as they come and go: a toast when one arrives, another if it ran out unpaid; a dot on Tithes while one is open.</summary>
        private void WatchDemand()
        {
            var demand = _snapshot?.Domain?.Demand;
            _hud.SetFeatureBadge("tithes", demand != null);
            var id = demand?.Id ?? -1;
            if (id == _seenDemand)
            {
                return;
            }

            if (demand != null && _seenDemand != int.MinValue)
            {
                Announce($"{LordName()} demands {VillagePresenter.ShortCost(demand.Amount)}. Deliver within {VillagePresenter.Duration((long)(demand.DueAtUtc - _snapshot.AsOfUtc).TotalSeconds)}, or else. (Tithes)");
            }
            else if (demand == null && _seenDemand > 0 && _metDemand != _seenDemand)
            {
                Announce($"You ignored {LordName()}'s demand. His temper flares (+{Demands.MissPenalty}).");
            }

            _seenDemand = id;
        }

        private SheetModel SheetFor(string id) => id switch
        {
            "research" => FeatureSheetPresenter.Research(_snapshot, _snapshot.AsOfUtc, confirmStop: IsArmed("research-stop")),
            "shop" => FeatureSheetPresenter.Market(_snapshot, BuildingTable.Default),
            "prophet" => FeatureSheetPresenter.Prophet(_snapshot),
            _ => FeatureSheetPresenter.Cult(_snapshot),
        };

        private string _armed;
        private float _armedUntil;

        /// <summary>
        /// A second tap within a few seconds does it; the first only says what will happen. For undoing hours of work
        /// with one finger (cancel a build, stop a study).
        /// </summary>
        private bool Armed(string what, string warning)
        {
            if (_armed == what && Time.unscaledTime < _armedUntil)
            {
                _armed = null;
                return true;
            }

            _armed = what;
            _armedUntil = Time.unscaledTime + 4f;
            Announce(warning);
            RefreshVillage(); // the button itself says "Sure?" (the toast can sit behind a sheet)
            if (_sheet.Shown == "research")
            {
                _sheet.Show(_sheet.Shown, SheetFor(_sheet.Shown));
            }

            return false;
        }

        private bool IsArmed(string what) => _armed == what && Time.unscaledTime < _armedUntil;

        private async void OnSheetAction(string action)
        {
            var parts = action.Split(':');
            if (parts[0] == "perk" && parts.Length == 2)
            {
                var outcome = await SendVillage(new ChooseProphetPerk(Guid.NewGuid(), parts[1]));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce($"The Prophet takes up {ProphetBook.Perk(parts[1])?.Name.ToLowerInvariant()}. He looks pleased with himself.");
                    _sheet.Show("prophet", SheetFor("prophet"));
                }
            }
            else if (action == "hearth:upgrade")
            {
                var outcome = await SendVillage(new UpgradeHearth(Guid.NewGuid()));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce("The faithful pile more stones round the fire. It'll be grander soon.");
                }
            }
            else if (action == "research-stop")
            {
                if (Armed(action, "Tap Stop again to stop reading. The cost comes back; the time doesn't."))
                {
                    var outcome = await SendVillage(new CancelResearch(Guid.NewGuid()));
                    if (outcome.Status == CommandStatus.Accepted)
                    {
                        Announce("The Prophet closes the manual and pretends he'd finished.");
                    }
                }
            }
            else if (parts[0] == "research" && parts.Length == 2)
            {
                var outcome = await SendVillage(new StartResearch(Guid.NewGuid(), parts[1]));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce($"The Prophet opens the manual at \"{BuildingTable.Default.Research.Get(parts[1])?.Name}\" and frowns.");
                }
            }
            else if (parts[0] == "trade" && parts.Length == 4
                     && Enum.TryParse<ResourceType>(parts[1], out var give) && Enum.TryParse<ResourceType>(parts[2], out var get) && int.TryParse(parts[3], out var amount))
            {
                var outcome = await SendVillage(new Trade(Guid.NewGuid(), give, get, amount));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce($"The trolley rattles off and comes back with {amount} {get}.");
                }
            }
        }

        /// <summary>
        /// Shortcuts appear only when they open (spec 2026-10-16 one thing at a time): the first time one does while
        /// you play (not on load), a toast says what it is.
        /// </summary>
        private void OpenFeature(string id, bool open)
        {
            if (_hud.SetFeatureOpen(id, open) && open && _featuresSeeded)
            {
                Announce(id switch
                {
                    "map" => "New: the Map. See the lands around you and who rules them.",
                    "missionaries" => "New: Missionaries. Send a preacher to win a village over.",
                    "tithes" => "New: Tithes. Keep the lord sweet before he sends men.",
                    "research" => "New: Research. Study better ways of doing things.",
                    _ => $"New: {LockedFeatures.Get(id).Label}.",
                });
            }
        }

        private bool _featuresSeeded;

        /// <summary>Keeps the map and sheets current while open; opens the shortcuts as the village earns them.</summary>
        private void RefreshMap()
        {
            var open = _snapshot?.Domain?.Unlocked == true;
            foreach (var id in new[] { "map", "missionaries", "tithes" })
            {
                OpenFeature(id, open);
            }

            OpenFeature("research", _snapshot != null && _snapshot.HearthLevel >= 2);
            _featuresSeeded |= _snapshot != null;
            WatchDemand();
            _hud.SetFeatureBadge("missionaries", _snapshot?.Domain?.Tales?.Count > 0);
            if (_house != null && _house.IsOpen && _snapshot != null)
            {
                RenderMissionHouse();
            }

            if (_hall != null && _hall.IsOpen && _snapshot != null)
            {
                if (_phase != GamePhase.Dusk)
                {
                    _hall.Show(false);
                }
                else
                {
                    RenderHall();
                }
            }

            _hud.SetFeatureOpen("shop", true);
            _hud.SetFeatureOpen("cult", true);
            if (_sheet?.Shown != null && _snapshot != null)
            {
                if (_phase != GamePhase.Dusk)
                {
                    _sheet.Close();
                }
                else
                {
                    _sheet.Show(_sheet.Shown, SheetFor(_sheet.Shown));
                }
            }

            if (_map != null && _map.IsOpen && _snapshot != null)
            {
                if (_phase != GamePhase.Dusk)
                {
                    CloseMap();
                    return;
                }

                RenderMap();
                if (_village is RemoteBaseGateway && Time.time - _worldFetchedAt > 30f)
                {
                    _ = FetchWorldAsync(_world?.viewQ, _world?.viewR); // rivals preach too: keep the window fresh
                }
            }
        }

        /// <summary>The shared world when the server has sent it; the Marchlands for a village that isn't on a world (offline play).</summary>
        private void RenderMap()
        {
            WorldMapModel model;
            if (_world != null)
            {
                model = SharedWorldPresenter.Build(_snapshot, BuildingTable.Default, _world, _snapshot.AsOfUtc);
            }
            else if (_snapshot.Domain?.World == null)
            {
                model = WorldMapPresenter.Build(_snapshot, BuildingTable.Default, _snapshot.AsOfUtc);
            }
            else
            {
                model = new WorldMapModel
                {
                    Title = "The wider world", MissionaryLine = "The world map needs a connection. The faithful will wait.",
                    OfferingsLine = string.Empty, TensionLine = $"Tension {_snapshot.Tension}/{BuildingTable.Default.Tension.Max}",
                };
            }

            _mapModel = model;
            _map.Render(model);
            StageMap(model);
        }

        /// <summary>Flies to the lord's seat and opens its card, where the tithe is paid (no seat known: the old tithe sheet).</summary>
        private void OpenSeat()
        {
            if (_mapModel?.Land == null || !_stage.IsOpen)
            {
                return; // StageMap opens it once the land has arrived (_pendingTithe)
            }

            _pendingTithe = false;
            if (_mapModel.SeatId >= 0)
            {
                _map.Tap(_mapModel.SeatId);
            }
            else
            {
                _map.ShowTitheSheet();
            }
        }

        /// <summary>Shows the model on the 3D land: opens it the first time (looking at your village), then keeps the people current.</summary>
        private void StageMap(WorldMapModel model)
        {
            if (model.Land == null)
            {
                _map.SetStatus(_village is RemoteBaseGateway ? "Finding your way to the wider world…" : null);
                return;
            }

            _map.SetStatus(_sendPreacher > 0 ? $"Tap a place to send {Preachers.Name(_sendPreacher)}." : null);
            if (!_stage.IsOpen || _stage.Land != model.Land)
            {
                _hud.SetMapOpen(true);
                var home = model.Land.TryPlace(WorldMapPresenter.HomeId, out var place) ? place.At : Vector2.zero;
                _stage.Open(model.Land, home, _camera);
            }

            var camps = model.Nodes.Where(n => n.Camp).Select(n => new StageCamp
            {
                Id = n.Id, Key = n.CultId, Q = n.Q, R = n.R,
                Colour = n.RivalColour >= 0 ? SharedWorldPresenter.CultColours[n.RivalColour % SharedWorldPresenter.CultColours.Length] : Color.grey,
            }).ToList();
            var walkers = model.Missionaries.Select(m => new StageWalker { NodeId = m.NodeId, Phase = m.Phase, Fraction = m.Fraction }).ToList();
            _stage.SetPeople(_snapshot.HearthLevel, camps, walkers);
            _stage.SetFaith(model.Nodes.Where(n => !n.Home && !n.Camp).Select(n => new StageFaith
            {
                Id = n.Id, Mine = n.Influence / 100f, Rival = n.RivalShare / 100f,
                RivalColour = n.RivalColour >= 0 ? SharedWorldPresenter.CultColours[n.RivalColour % SharedWorldPresenter.CultColours.Length] : Color.grey,
            }).ToList());
            _stage.SetTension(model.SeatId, model.TensionFraction, model.RaidComing);
            // The minimap's dots: every lord's seat in the world (dark), and the rival camps in view (their colours).
            _miniMarks.Clear();
            _miniMarks.AddRange(model.Land.Places.Where(p => p.Type == NodeType.Keep || p.Type == NodeType.Cathedral).Select(p => (p.At, new Color(0.35f, 0.12f, 0.1f, 0.65f))));
            _miniMarks.AddRange(camps.Select(c => (model.Land.Spot(c.Q, c.R), c.Colour)));
            if (_pendingTithe)
            {
                OpenSeat();
            }

            if (_pendingFocus >= 0)
            {
                _stage.FlyToMissionary(_pendingFocus);
                _pendingFocus = -1;
            }
        }

        private void CloseMap()
        {
            _map.Show(false);
            _stage.Close();
            _pendingTithe = false;
            _sendPreacher = 0;
            _hud.SetMapOpen(false);
            _stageWindow = null;
        }

        /// <summary>
        /// Fingers on the open map move its camera (a flick glides on); a tap picks the place under it, or closes the
        /// card. As the view moves, the server is asked for the window round where the camera looks.
        /// </summary>
        private void UpdateMap()
        {
            PointerInput.GetPressedPointers(_pointers);
            _gesture = _gestures.Update(_pointers, _map.IsOverPanel);
            var rig = _stage.Rig;
            if (_gesture.Panned)
            {
                rig.Drag(_gesture.PanFrom, _gesture.PanTo, Time.deltaTime);
                _mapHeld = true;
            }

            if (_pointers.Count == 0 && _mapHeld)
            {
                _mapHeld = false;
                rig.Release();
            }

            if (_gesture.Pinched)
            {
                rig.Zoom(_gesture.PinchRatio, _gesture.PinchCenter);
            }

            var scroll = PointerInput.ScrollDelta();
            if (Mathf.Abs(scroll) > 0.01f && PointerInput.TryGetHover(out var mouse) && !_map.IsOverPanel(mouse))
            {
                rig.Zoom(Mathf.Pow(1.0015f, scroll), mouse);
            }

            if (_gesture.Tapped)
            {
                var picked = _stage.Pick(_gesture.TapPosition);
                if (picked != null && _map.HasPlate(picked.Value))
                {
                    _map.Tap(picked.Value);
                }
                else
                {
                    _map.CloseCard();
                }
            }

            _map.PlaceLabels(id => _stage.TryViewport(id, out var at) ? at : (Vector2?)null, rig.Distance);
            _map.SetMinimap(_stage.Minimap, _stage.Bound);
            _map.UpdateMinimap(rig.Focus, _stage.ViewHalfWidth(), _stage.HomeAt, _miniMarks);
            if (_world != null && _village is RemoteBaseGateway)
            {
                var (q, r) = _stage.FocusHex();
                var radius = Mathf.Clamp(_stage.ViewRadiusHexes() + 1, SharedWorldPresenter.MinRadius, 14);
                var last = _stageWindow ?? (_world.viewQ, _world.viewR, _world.viewRadius);
                var moved = CultOfComfort.Simulation.World.Hex.Distance(q, r, last.Q, last.R);
                if (moved >= 2 || radius > last.Radius || radius < last.Radius - 3)
                {
                    _stageWindow = (q, r, radius);
                    _worldRadius = radius;
                    _ = FetchWorldAsync(q, r);
                }
            }
        }

        /// <summary>Asks the server for the window around (q, r) (null: around the village), joining a world the first time.</summary>
        private async Task FetchWorldAsync(int? q, int? r)
        {
            if (!(_village is RemoteBaseGateway remote))
            {
                return;
            }

            if (_worldFetching)
            {
                _worldNext = (q, r); // a pan or zoom while a fetch is out: fetch that window next, not never
                return;
            }

            _worldFetching = true;
            _worldFetchedAt = Time.time;
            try
            {
                var world = await remote.WorldAsync(q, r, _worldRadius, _cts.Token);
                if (world != null)
                {
                    _world = world;
                    Ruler.Set(SharedWorldPresenter.Authority(SharedWorldPresenter.MapFor(world), world).Lord);
                    if (_map.IsOpen && _snapshot != null)
                    {
                        RefreshSnapshot();
                        RenderMap();
                    }

                    if (_house.IsOpen && _snapshot != null)
                    {
                        RenderMissionHouse();
                    }

                    if (_hall.IsOpen && _snapshot != null)
                    {
                        RenderHall();
                    }
                }
            }
            finally
            {
                _worldFetching = false;
            }

            if (_worldNext is var (nq, nr) && _worldNext != null)
            {
                _worldNext = null;
                await FetchWorldAsync(nq, nr);
            }
        }

        private (int? Q, int? R)? _worldNext;

        /// <summary>Raised when the first-launch intro ends (played out or skipped).</summary>
        public event Action IntroFinished;

        /// <summary>The intro while it plays (tests and the tutorial look at it).</summary>
        public IntroDirector Intro => _intro;

        private void OnDestroy() => _cts.Cancel();

        private void Update()
        {
            if (_intro != null)
            {
                return; // the intro has the camera and the screen
            }

            TrackOrientation();
            if (_stage != null && _stage.IsOpen && _map.IsOpen)
            {
                UpdateMap();
            }
            else
            {
                ReadGestures();
            }

            if (_phase == GamePhase.Dusk)
            {
                UpdateDusk();
                FloatIncome();
            }
            else if (_phase == GamePhase.Night)
            {
                UpdateNight();
            }

            UpdateNightfall();
            _board.SetHearthWarmth(_battle == null ? 1f : _battle.HearthHealth / (float)_battle.HearthMaxHealth);
            _rig.SetShake(UnityEngine.Random.insideUnitSphere * _battleView.CameraShake);
            // The haze keeps its distance from the camera, so zooming out over the valley doesn't drown it in fog.
            if (_stage == null || !_stage.IsOpen)
            {
                RenderSettings.fogStartDistance = _rig.Distance + 12f;
                RenderSettings.fogEndDistance = _rig.Distance * 2.2f + 40f;
            }
            UpdateHud();
        }

        private readonly Dictionary<int, float> _income = new Dictionary<int, float>();

        /// <summary>
        /// Producers float what they've put in the stores ("+3 Timber"): resources stack themselves now (spec
        /// 2026-10-14 auto-collect), so the village shows it instead of asking for taps. A busy lean-to speaks up about
        /// every half minute, a slow hut whenever a whole Devotion is in.
        /// </summary>
        private void FloatIncome()
        {
            if (_snapshot == null || _intro != null)
            {
                return;
            }

            foreach (var b in _snapshot.Buildings)
            {
                var resource = BuildingTable.Default.Spec(b.Type).Produces;
                if (resource == null || b.PerHour <= 0 || b.Wrecked || b.IsUpgrading)
                {
                    continue;
                }

                _income.TryGetValue(b.Id, out var made);
                made += b.PerHour * Time.deltaTime / 3600f;
                if (made >= Mathf.Max(1f, b.PerHour / 120f))
                {
                    var whole = Mathf.FloorToInt(made);
                    FloatNote(Shapes.ToWorld(b.Position) + Vector3.up * 1.4f, $"+{whole} {resource}");
                    made -= whole;
                }

                _income[b.Id] = made;
            }
        }

        /// <summary>Night falls (and Dawn breaks) over ~1.5 s rather than with a cut.</summary>
        private void UpdateNightfall()
        {
            var target = _phase == GamePhase.Night ? 1f : 0f;
            if (Mathf.Approximately(_darkness, target))
            {
                return;
            }

            _darkness = Mathf.MoveTowards(_darkness, target, Time.deltaTime / 1.5f);
            _mood.Apply(_darkness);
            _board.SetNight(_darkness);
        }

        /// <summary>One-finger drag pans, two-finger pinch zooms (and pans), mouse wheel zooms. Taps come out on release.</summary>
        private void ReadGestures()
        {
            PointerInput.GetPressedPointers(_pointers);
            _gesture = _gestures.Update(_pointers, IsOverUi);

            // One finger: drags the ghost if it starts on it, paints with a paint tool, otherwise pans. Two fingers pan and zoom.
            if (_pointers.Count == 0)
            {
                _dragMode = DragMode.None;
                _tool?.Release();
            }

            var oneFinger = _phase == GamePhase.Dusk && _pointers.Count == 1;
            if (_gesture.Panned)
            {
                if (_dragMode == DragMode.None)
                {
                    _dragMode = oneFinger && _tool != null && TryScreenToTile(_gesture.PanFrom, out var grab) && _tool.TryGrab(grab, BuildingTable.Default) ? DragMode.Ghost
                        : oneFinger && _buildSelection != null && BuildingTable.IsPaintable(_buildSelection.Value) ? DragMode.Paint
                        : DragMode.Pan;
                }

                if (_dragMode == DragMode.Ghost && TryScreenToTile(_gesture.PanTo, out var dragTo))
                {
                    _tool.DragTo(dragTo);
                }
                else if (_dragMode == DragMode.Paint && _pointers.Count == 1 && TryScreenToTile(_gesture.PanFrom, out var from) && TryScreenToTile(_gesture.PanTo, out var to))
                {
                    if (_stroke.Count == 0)
                    {
                        _stroke.Add(from);
                    }

                    ExtendStroke(_stroke, to);
                    ShowStroke();
                }
                else if (_dragMode == DragMode.Pan || _pointers.Count > 1)
                {
                    _rig.Pan(_gesture.PanFrom, _gesture.PanTo);
                }
            }

            if (_stroke.Count > 0 && _pointers.Count == 0)
            {
                var stroke = _stroke.ToArray();
                _stroke.Clear();
                _board.ShowPaintPreview(null, _ => true);
                PaintStroke(stroke);
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
            if (_tool != null && _snapshot != null)
            {
                ShowGhost();
            }
            else
            {
                if (_ghostShown)
                {
                    _ghostShown = false;
                    _board.ShowGhost(null, 0, null, false, false);
                    _hud.ShowGhostActions(null, null, false);
                }

                if (hasPointer && TryScreenToTile(hover, out var hoverTile))
                {
                    _board.ShowCursor(hoverTile, LooksBuildable(hoverTile));
                }
                else
                {
                    _board.ShowCursor(null, false);
                }
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

        /// <summary>A quick local hint for the cursor (the village's own rule, from the snapshot); the gateway has the final word.</summary>
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

            return VillageMap.Demo.PlacementProblem(_snapshot, BuildingTable.Default.Spec(_buildSelection.Value), tile, false) == null;
        }

        /// <summary>The ghost on the board, and ✓ ✗ ↻ beside it with the reason it won't fit (if it won't).</summary>
        private void ShowGhost()
        {
            _ghostShown = true;
            var table = BuildingTable.Default;
            var tiles = _tool.Tiles(table);
            var problem = _tool.Problem(_snapshot, VillageMap.Demo, table);
            var level = _tool.MovingId != null ? _snapshot.Building(_tool.MovingId.Value).Level : 1;
            _board.ShowGhost(_tool.Type, level, tiles, _tool.Rotated, problem == null, _tool.Flipped);
            var towerType = BuildingTable.Default.Spec(_tool.Type).Tower;
            _board.ShowReach(towerType is { } t ? Shapes.ToWorld(tiles[0]) : (Vector3?)null,
                towerType is { } r ? (float)BalanceTable.Default.TowerAt(r, level, manned: false).Range.ToDouble() : 0f);
            var (centre, size) = BoardView.FootprintBox(tiles);
            var spec = table.Spec(_tool.Type);
            var needsRoad = problem == null && !spec.IsTower && !BuildingTable.IsRoad(spec.Type) && !BuildingTable.IsBarrier(spec.Type)
                && spec.Type is not (BuildingType.Tree or BuildingType.Stone) && BuildingTable.NeedsRoad(spec.Type) && !VillageMap.Demo.WouldConnect(_snapshot, tiles);
            _hud.ShowGhostActions(GhostBox(centre, size), problem, CanTurn(spec),
                needsRoad ? "No road here yet: it won't work until you paint one to it." : null);
        }

        /// <summary>A world point in UI panel coordinates, or null when it's behind the camera or there's no panel yet.</summary>
        /// <summary>
        /// The ghost's box on screen (panel coordinates): its footprint corners at the ground and a model's height up,
        /// so ✓ ✗ Turn can sit clear of it (they used to cover a small ghost, and a drag on them missed it).
        /// </summary>
        private Rect? GhostBox(Vector3 centre, Vector2 size)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var dx in new[] { -0.5f, 0.5f })
            foreach (var dz in new[] { -0.5f, 0.5f })
            foreach (var up in new[] { 0f, 1.2f })
            {
                var p = PanelPoint(centre + new Vector3(dx * size.x, up, dz * size.y));
                if (p == null)
                {
                    return null;
                }

                xMin = Mathf.Min(xMin, p.Value.x);
                xMax = Mathf.Max(xMax, p.Value.x);
                yMin = Mathf.Min(yMin, p.Value.y);
                yMax = Mathf.Max(yMax, p.Value.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Vector2? PanelPoint(Vector3 world)
        {
            var panel = _document != null ? _document.rootVisualElement.panel : null;
            var screen = _camera.WorldToScreenPoint(world);
            if (panel == null || screen.z < 0f)
            {
                return null;
            }

            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        /// <summary>The tile in the middle of the clear part of the screen (where a new ghost appears).</summary>
        /// <summary>Light the Lamps waits until the first-Night errand, unless the Authority has already announced a raid.</summary>
        private bool LampsWait() =>
            _snapshot != null && _snapshot.RaidWarningEndsAtUtc == null && ErrandNote.LampsWait(_snapshot.Errand, BuildingTable.Default.Errands.Errands);

        /// <summary>The errand note: claim a done errand, otherwise point at what to do next.</summary>
        private async void OnErrandClicked()
        {
            var note = ErrandNote.From(_snapshot?.Errand);
            if (note == null || _phase != GamePhase.Dusk)
            {
                return;
            }

            try
            {
                switch (note.Hint)
                {
                    case ErrandHint.Claim:
                        var outcome = await SendVillage(new ClaimErrand(Guid.NewGuid()));
                        if (outcome.Status == CommandStatus.Accepted)
                        {
                            Announce($"Errand done! {note.Reward}".TrimEnd());
                            Bark(BarkKind.CultistErrandDone, "errand", At(Shapes.ToWorld(VillageMap.Demo.Hearth, 1.8f)));
                        }

                        break;
                    case ErrandHint.BuildCard when note.Type != null:
                        _hud.Hint(note.Type, Time.time);
                        break;
                    case ErrandHint.StartNight:
                        _hud.Hint(null, Time.time);
                        break;
                    case ErrandHint.Map:
                        OpenFeature("map");
                        break;
                    case ErrandHint.Research:
                        OpenFeature("research");
                        break;
                    case ErrandHint.Hearth:
                    case ErrandHint.Building:
                        var building = note.Hint == ErrandHint.Hearth
                            ? _snapshot.Buildings.First(b => b.Type == BuildingType.Hearth)
                            : _snapshot.Buildings.Where(b => b.Type == note.Type).OrderBy(b => b.Level).FirstOrDefault();
                        if (building == null)
                        {
                            _hud.Hint(note.Type, Time.time); // nothing to point at yet: build one first
                            break;
                        }

                        BringIntoView(building.Position);
                        _cardBuilding = building.Id;
                        _cardTerrain = null;
                        RefreshVillage();
                        break;
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

        /// <summary>Pans so the tile sits in the middle of the clear view, unless it is already in sight.</summary>
        private void BringIntoView(GridPosition tile)
        {
            var clear = _hud.ClearViewport() ?? new Rect(0f, 0.25f, 1f, 0.6f);
            var screen = _camera.WorldToScreenPoint(Shapes.ToWorld(tile));
            if (screen.z > 0f && clear.Contains(new Vector2(screen.x / Screen.width, screen.y / Screen.height)))
            {
                return;
            }

            _rig.Pan(screen, new Vector2(clear.center.x * Screen.width, clear.center.y * Screen.height));
        }

        private GridPosition CentreTile()
        {
            var clear = _hud.ClearViewport() ?? new Rect(0f, 0.25f, 1f, 0.6f);
            var screen = new Vector2(clear.center.x * Screen.width, clear.center.y * Screen.height);
            return TryScreenToTile(screen, out var tile) ? tile : VillageMap.Demo.Hearth;
        }

        private void CancelTool()
        {
            _tool = null;
            if (_buildSelection != null && !BuildingTable.IsPaintable(_buildSelection.Value))
            {
                _buildSelection = null;
            }

            _board.HideBuilding(null);
            _ghostShown = false;
            _board.ShowGhost(null, 0, null, false, false);
            _hud.ShowGhostActions(null, null, false);
            RefreshVillage();
        }

        private async void ConfirmTool()
        {
            if (_tool == null)
            {
                return;
            }

            try
            {
                var tool = _tool;
                var outcome = await SendVillage(tool.Command());
                if (outcome.Status != CommandStatus.Accepted || _tool != tool)
                {
                    return;
                }

                CancelTool();
                var placed = _snapshot.BuildingAt(tool.Anchor);
                if (tool.MovingId == null && placed != null && !placed.Connected)
                {
                    Say("Not connected yet. Paint a road from it to the Hearth, or to the raiders' road.", prophet: false);
                }
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        /// <summary>Adds tiles from the stroke's last tile to <paramref name="to"/>, one orthogonal step at a time (no diagonal gaps).</summary>
        private static void ExtendStroke(List<GridPosition> stroke, GridPosition to)
        {
            var last = stroke[stroke.Count - 1];
            while (last != to)
            {
                last = last.X != to.X ? new GridPosition(last.X + Math.Sign(to.X - last.X), last.Y) : new GridPosition(last.X, last.Y + Math.Sign(to.Y - last.Y));
                if (!stroke.Contains(last))
                {
                    stroke.Add(last);
                }
            }
        }

        private void ShowStroke()
        {
            var spec = BuildingTable.Default.Spec(_buildSelection.Value);
            _board.ShowPaintPreview(_stroke, t => _snapshot == null || VillageMap.Demo.PlacementProblem(_snapshot, spec, t, false) == null
                                                   || BuildingTable.IsRoad(_snapshot.BuildingAt(t)?.Type ?? BuildingType.Hearth));
        }

        /// <summary>Lays the selected road along a stroke (its points joined up), in one command.</summary>
        private async void PaintStroke(IReadOnlyList<GridPosition> points)
        {
            if (_buildSelection == null || !BuildingTable.IsPaintable(_buildSelection.Value) || points.Count == 0)
            {
                return;
            }

            var tiles = new List<GridPosition> { points[0] };
            for (var i = 1; i < points.Count; i++)
            {
                ExtendStroke(tiles, points[i]);
            }

            try
            {
                await SendVillage(new PaintTiles(Guid.NewGuid(), _buildSelection.Value, tiles));
            }
            catch (Exception e)
            {
                VillageFailed(e);
            }
        }

        /// <summary>Build with the selected card, otherwise collect from a building or open its card.</summary>
        private async void HandleDayTap(GridPosition tile)
        {
            try
            {
                Accept(await _village.LoadAsync(_cts.Token)); // a tap always sees what is ready right now
                if (_tool != null)
                {
                    if (!_tool.Tiles(BuildingTable.Default).Contains(tile))
                    {
                        CancelTool(); // a tap off the ghost puts it away: nothing is ever built by a stray tap
                    }

                    return;
                }

                if (_buildSelection != null && BuildingTable.IsPaintable(_buildSelection.Value))
                {
                    await SendVillage(new PaintTiles(Guid.NewGuid(), _buildSelection.Value, new[] { tile })); // paint tools stay on: tap or drag, then Done
                    return;
                }

                _cardVisitor = VillagePresenter.VisitorTile(_snapshot, VillageMap.Demo) == tile;
                if (_cardVisitor)
                {
                    _cardBuilding = null;
                    _cardTerrain = null;
                    RefreshVillage();
                    return;
                }

                var building = _snapshot.BuildingAt(tile);
                if (building == null)
                {
                    _cardBuilding = null;
                    _cardTerrain = VillagePresenter.TerrainCard(_snapshot, VillageMap.Demo, BuildingTable.Default, tile) != null ? tile : (GridPosition?)null;
                    if (_cardTerrain == null && VillageMap.Demo.IsBedrock(tile))
                    {
                        Say("That's bedrock. It was here before the Romans and it will be here after the Prophet.", prophet: true);
                    }

                    RefreshVillage();
                    return;
                }

                _cardTerrain = null;

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
                _audio.Play("error");
            }
            else
            {
                _audio.Play(command switch
                {
                    PlaceBuilding _ or PaintTiles _ or MoveBuilding _ or UpgradeBuilding _ or UpgradeHearth _ or RepairBuilding _ or RepairAll _ => "place",
                    Collect _ when !result.Outcome.Collected.IsZero => "collect",
                    ClaimErrand _ => "claim",
                    _ => null,
                });
            }

            RefreshVillage();
            return result.Outcome;
        }

        private async void LoadVillage()
        {
            try
            {
                Accept(await _village.LoadAsync(_cts.Token));
                // The day turns at 04:00 on this phone's clock (spec 2026-10-15 daily errands); quietly, a no-op when unchanged.
                var offset = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);
                var clock = await _village.SendAsync(new SetDayClock(Guid.NewGuid(), offset), _cts.Token);
                Accept(clock.Snapshot);
                if (_snapshot.NightInProgress != null)
                {
                    // The app closed mid-Night: the towers finish it on their own.
                    await SendVillage(new ResolveNight(Guid.NewGuid(), _snapshot.NightInProgress.Night, new List<PlayerInput>()));
                }

                _night = _snapshot.LastNightRecorded + 1; // carry on where the save left off
                if (_snapshot.Domain?.World != null)
                {
                    _ = FetchWorldAsync(null, null); // who rules here comes with the world: the tape and the Night name him
                }

                if (_snapshot.Errand?.Index == 0 && _snapshot.Buildings.Count == 1)
                {
                    // A brand-new village: the Prophet says one line, then the errand note takes over.
                    _hud.Announce("A cosy spot. Let's make it cosier. Build us a woodcutter, would you?", Time.time, 6f, prophet: true);
                }

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
                var before = _snapshot;
                _snapshot = snapshot;
                if (before != null && _phase == GamePhase.Dusk)
                {
                    BarkAboutTheVillage(before, snapshot);
                }
            }
        }

        // ---------------- barks: what everyone shouts ----------------

        private void Bark(BarkKind kind, string speaker, Func<Vector3?> where) => Say(roll => BarkBook.Line(kind, roll), speaker, where);

        private void Say(Func<int, string> line, string speaker, Func<Vector3?> where)
        {
            if (_hud == null || !_bubbles.TrySpeak(speaker, Time.time))
            {
                return;
            }

            _hud.Bubble(line(_barkRoll++), () => where() is Vector3 world ? PanelPoint(world) : null, Time.time);
        }

        private Func<Vector3?> RaiderAt(int id)
        {
            var battle = _battle;
            return () =>
            {
                var enemy = battle?.Enemies.FirstOrDefault(e => e.Id == id);
                return enemy != null && enemy.IsActive ? Shapes.ToWorld(battle.PositionOf(enemy), 1.5f) : (Vector3?)null;
            };
        }

        private Func<Vector3?> ProphetAt()
        {
            var battle = _battle;
            return () => battle != null ? Shapes.ToWorld(battle.Leader.Position, 1.8f) : (Vector3?)null;
        }

        private static Func<Vector3?> At(Vector3 world) => () => world;

        private void BarkAbout(IReadOnlyList<BattleEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case EnemySpawned spawned when spawned.EnemyId % 3 == 1 || spawned.Type == EnemyType.Lord:
                        Say(roll => BarkBook.Arrival(spawned.Type, roll), $"raider-{spawned.EnemyId}", RaiderAt(spawned.EnemyId));
                        break;
                    case StructureHit hit:
                        var target = _battle.Structures.FirstOrDefault(st => st.Id == hit.StructureId);
                        if (target != null && target.IsBarrier)
                        {
                            var onRoad = target.Tiles.Any(t => _battle.Path.Contains(t));
                            Bark(onRoad ? BarkKind.RaiderBashesGate : BarkKind.RaiderHacksWall, $"raider-{hit.EnemyId}", RaiderAt(hit.EnemyId));
                        }

                        break;
                    case InterventionUsed used when used.Type == PlayerActionType.OpenSluiceGate:
                        Bark(BarkKind.ProphetSluice, "prophet", ProphetAt());
                        var soaked = _battle.Enemies.FirstOrDefault(x => x.IsActive);
                        if (soaked != null)
                        {
                            Bark(BarkKind.RaiderWashedBack, $"raider-{soaked.Id}", RaiderAt(soaked.Id));
                        }

                        break;
                    case InterventionUsed used when used.Type == PlayerActionType.PhantomVoice:
                        Bark(BarkKind.ProphetVoice, "prophet", ProphetAt());
                        break;
                    case InterventionUsed used when used.Type == PlayerActionType.HotStones:
                        Bark(BarkKind.ProphetStones, "prophet", ProphetAt());
                        break;
                    case LeaderSwooned _:
                        Bark(BarkKind.ProphetSwoons, "prophet", ProphetAt());
                        break;
                    case WaveStarted wave when wave.Wave > 1:
                        Bark(BarkKind.ProphetWave, "prophet", ProphetAt());
                        break;
                    case HearthDamaged reached:
                        Bark(BarkKind.RaiderReachesHearth, $"raider-{reached.EnemyId}", At(Shapes.ToWorld(VillageMap.Demo.Hearth, 1.6f)));
                        break;
                }
            }
        }

        /// <summary>The cultists comment on what changed in the village: a building finished, newcomers, the cold, a raid coming.</summary>
        private void BarkAboutTheVillage(BaseSnapshot before, BaseSnapshot after)
        {
            var hearth = At(Shapes.ToWorld(VillageMap.Demo.Hearth, 1.8f));
            foreach (var b in after.Buildings)
            {
                var old = before.Buildings.FirstOrDefault(x => x.Id == b.Id);
                if (old != null && old.Level == 0 && b.Level > 0 && !BuildingTable.IsRoad(b.Type))
                {
                    Bark(BarkKind.CultistBuilt, $"cultist-{b.Id}", At(BoardView.FootprintBox(b.Tiles).Centre + Vector3.up * 1.6f));
                }
            }

            if (after.Population > before.Population)
            {
                Bark(BarkKind.CultistNewcomer, "cultist-hearth", hearth);
            }

            if (after.Cold && !before.Cold)
            {
                Bark(BarkKind.CultistCold, "cultist-hearth", hearth);
            }

            if (after.RaidWarningEndsAtUtc != null && before.RaidWarningEndsAtUtc == null)
            {
                Bark(BarkKind.CultistRaidComing, "cultist-hearth", hearth);
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

        private async void SendSimple(BaseCommand command)
        {
            try
            {
                await SendVillage(command);
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

            if (_snapshot.HearthLevel != _openLevel)
            {
                var found = RelicNews.Found(_openLevel, _snapshot.HearthLevel);
                if (_openLevel > 0 && _openLevel < BuildingTable.Default.Domain.UnlockHearth && _snapshot.HearthLevel >= BuildingTable.Default.Domain.UnlockHearth)
                {
                    found = (found == null ? string.Empty : found + " ") + "And the Marchlands are open: tap the Map and send a missionary.";
                }
                if (found != null)
                {
                    _hud.Announce(found, Time.time, 7f, prophet: true);
                }

                _openLevel = _snapshot.HearthLevel;
                _board.SetOpenLevel(_openLevel);
                FrameStage();
            }

            if (_phase == GamePhase.Dusk && _snapshot.UnseenReport != null && !_showingReport)
            {
                _showingReport = true; // the raid played itself while we were away: show it once
                _phase = GamePhase.Dawn;
                _hud.ShowPhase(_phase);
                _hud.ShowAwayReport(_snapshot.UnseenReport);
            }

            _board.SetBuildings(_snapshot.Buildings, _snapshot.HearthLevel, BuildingTable.Default, _snapshot.AsOfUtc);
            _board.SetRaidersRoad(_snapshot.RaidersRoad);
            _board.SetTerrain(_snapshot);
            if (_snapshot.Version != _roadVersion)
            {
                _roadVersion = _snapshot.Version;
                var map = VillageMap.Demo;
                _board.ShowRoad(map.OpenRoads(_snapshot.HearthLevel).SelectMany(map.RoadAt).Distinct().ToList()); // every open road, painted
                _board.ShowScout(map.OpenRoads(_snapshot.HearthLevel).Count > 1 ? map.SpawnOf(_snapshot.NextRoad) : (GridPosition?)null);
            }

            _hud.SetBuildCards(VillagePresenter.BuildCards(_snapshot, BuildingTable.Default), _buildSelection);
            var dusk = _phase == GamePhase.Dusk;
            _hud.ShowPaintDone(dusk && _buildSelection != null && BuildingTable.IsPaintable(_buildSelection.Value));
            _hud.ShowRepairAll(dusk && _tool == null ? VillagePresenter.RepairAllLabel(_snapshot, BuildingTable.Default) : null);
            var lampsWait = LampsWait();
            _hud.SetStartLabel(lampsWait ? "Lamps can wait · build first" : VillagePresenter.StartLabel(_snapshot));
            _hud.SetStartWaiting(lampsWait);
            _hud.ShowErrand(dusk && _tool == null ? ErrandNote.From(_snapshot.Errand) : null); // placing: the note steps aside for the ghost
            _hud.ShowDaily(dusk && _tool == null && _snapshot.Errand == null ? DailyNote.From(_snapshot.Daily, _snapshot.AsOfUtc) : null);
            _board.SetCosmetics(_snapshot.Daily?.Cosmetics);
            var hasCard = _cardBuilding != null && _snapshot.Buildings.Any(b => b.Id == _cardBuilding.Value);
            var terrainCard = _cardTerrain != null ? VillagePresenter.TerrainCard(_snapshot, VillageMap.Demo, BuildingTable.Default, _cardTerrain.Value) : null;
            _cardVisitor &= _snapshot.Visitor != null; // they may have gone while the card was open
            var card = _cardVisitor ? VillagePresenter.VisitorCard(_snapshot)
                : hasCard ? VillagePresenter.Card(_snapshot, BuildingTable.Default, _cardBuilding.Value) : terrainCard;
            _board.ShowVisitor(_snapshot.Visitor?.Offer.Kind, VillagePresenter.VisitorTile(_snapshot, VillageMap.Demo));
            _hud.SetProphetBadge(_snapshot.Prophet?.Choice != null);
            if (_snapshot.Visitor is { } visitor && visitor.Slot != _greetedVisitorSlot && _phase == GamePhase.Dusk)
            {
                _greetedVisitorSlot = visitor.Slot;
                Say($"{visitor.Offer.Who} is at the Hearth. Tap them to see what they want.", prophet: false);
            }
            if (card?.CancelLabel != null && IsArmed("cancel-build:" + _cardBuilding))
            {
                card.CancelLabel = "Sure? Tap again to cancel"; // the warning on the button itself: a toast hides behind the card
            }

            _hud.ShowBuildingCard(card);
            if (_tool == null)
            {
                var shown = hasCard ? _snapshot.Building(_cardBuilding.Value) : null;
                var tower = shown != null ? BuildingTable.Default.Spec(shown.Type).Tower : null;
                _board.ShowReach(tower != null ? Shapes.ToWorld(shown.Position) : (Vector3?)null,
                    tower != null ? (float)BalanceTable.Default.TowerAt(tower.Value, shown.Level, manned: false).Range.ToDouble() : 0f);
            }

            RefreshMap();
        }

        /// <summary>Asks the village for tonight's plan (the raid that is coming, or an early one) and fights exactly that.</summary>
        public async void StartNight()
        {
            try
            {
                if (_phase != GamePhase.Dusk || _village == null)
                {
                    return;
                }

                var outcome = await SendVillage(new BeginNight(Guid.NewGuid()));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    BeginFight(outcome.Plan);
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

        private void BeginFight(NightPlan plan)
        {
            _buildSelection = null;
            _tool = null;
            _board.HideBuilding(null);
            _hud.ShowPaintDone(false);
            _hud.ShowErrand(null); // the errand waits for Dawn
            _hud.ShowDaily(null);
            _hud.ShowRepairAll(null);
            _cardBuilding = null;
            _hud.ShowBuildingCard(null);
            _board.ClearStructureHealth();
            FrameRateLogger.Mark("BeginFight");
            _plan = plan;
            _night = plan.Night;
            _setup = plan.ToBattleSetup(VillageMap.Demo, new List<PlayerInput>());
            _battle = new Battle(_setup);
            _battleView.Begin(_battle);
            _audio.SetNight(true);
            _audio.Play("lamps");
            _framePending = 2; // the Night strip is taller than the Dusk one: re-fit the board once the HUD has laid out
            _board.ShowCursor(null, false);
            _accumulator = 0f;
            _phase = GamePhase.Night;
            FrameStage(); // in close on the road for the fight
            _hud.ShowPhase(_phase);
            Announce("Night falls. Tap the ground to move the Prophet.");
        }

        // ---------------- Night: live battle ----------------

        private readonly HashSet<int> _openGates = new HashSet<int>();

        /// <summary>A gate swings open while the Prophet or a cultist is within a step and a half of it.</summary>
        private void OpenGatesForFriends()
        {
            _openGates.Clear();
            if (_battle != null && _snapshot != null)
            {
                var friends = new List<Vector2> { new Vector2((float)_battle.Leader.Position.X.ToDouble(), (float)_battle.Leader.Position.Y.ToDouble()) };
                foreach (var defender in _battle.Defenders)
                {
                    if (defender.IsUp)
                    {
                        friends.Add(new Vector2((float)defender.Position.X.ToDouble(), (float)defender.Position.Y.ToDouble()));
                    }
                }

                foreach (var gate in _snapshot.Buildings.Where(b => b.Type == BuildingType.Gate && !b.Wrecked))
                {
                    if (gate.Tiles.Any(t => friends.Any(f => (f - new Vector2(t.X, t.Y)).sqrMagnitude <= 2.25f)))
                    {
                        _openGates.Add(gate.Id);
                    }
                }
            }

            _board.SetGatesOpen(_openGates);
        }

        private void UpdateNight()
        {
            if (_aim != null && !AimReady(_aim.Value))
            {
                SetAim(null); // He fainted mid-aim (or the sun set).
            }

            if (_gesture.Tapped && TryScreenToTile(_gesture.TapPosition, out var tile))
            {
                if (_aim != null)
                {
                    _battle.Intervene(_aim.Value, tile);
                    SetAim(null);
                }
                else
                {
                    _battle.Intervene(PlayerActionType.MoveLeader, tile);
                }
            }

            ShowStonesTarget();
            OpenGatesForFriends();

            _accumulator += Time.deltaTime * _speed;
            var steps = 0;
            while (_accumulator >= TickSeconds && !_battle.IsFinished && steps++ < MaxStepsPerFrame)
            {
                _battleView.BeforeStep();
                _battle.Step();
                _battleView.AfterStep();
                BarkAbout(_battle.LastTickEvents);
                foreach (var battleEvent in _battle.LastTickEvents)
                {
                    _audio.Play(SoundBook.For(battleEvent));
                }
                _accumulator -= TickSeconds;
            }

            _board.ShowLeaderDestination(_battle.Leader.IsMoving ? _battle.Leader.Destination : (GridPosition?)null);
            _battleView.Render(Mathf.Clamp01(_accumulator / TickSeconds));

            if (_battle.IsFinished)
            {
                EndNight();
            }
        }

        /// <summary>
        /// Hot Stones aims itself at the biggest knot of raiders in reach (owner 2026-10-15: aiming by finger "is not
        /// really handy"); with nobody in reach it falls back to tapping a tile inside the pebble ring.
        /// </summary>
        private void ToggleAimingStones()
        {
            if (_battle != null && _aim == null && _battle.IsHotStonesReady && _battle.BestHotStonesTarget() is { } target)
            {
                _battle.Intervene(PlayerActionType.HotStones, target);
                return;
            }

            ToggleAim(PlayerActionType.HotStones, "Nobody in reach yet: tap a tile inside the pebble ring.");
        }

        /// <summary>Arms an aimed skill (or disarms it on a second press); the next tap on the board fires it.</summary>
        private void ToggleAim(PlayerActionType skill, string prompt)
        {
            if (_battle == null || !AimReady(skill))
            {
                return;
            }

            SetAim(_aim == skill ? (PlayerActionType?)null : skill);
            if (_aim != null)
            {
                Announce(prompt);
            }
        }

        private RelicButtonState RelicButton(Relic relic, string name, int readyAtTick, bool ready, PlayerActionType? aimed, bool swooning)
        {
            if (!_battle.Relics.HasFlag(relic))
            {
                return default;
            }

            var aiming = aimed != null && _aim == aimed;
            var text = swooning ? "Fainted" : aiming ? "Pick" : ready ? (_battle.AutoRelics ? $"{name} · auto" : name) : $"{name} {Seconds(readyAtTick)}s";
            return new RelicButtonState(true, text, ready, aiming);
        }

        private bool AimReady(PlayerActionType skill) => skill switch
        {
            PlayerActionType.HotStones => _battle.IsHotStonesReady,
            PlayerActionType.LaserPointer => _battle.IsLaserReady,
            PlayerActionType.SunFire => _battle.IsSunFireReady,
            _ => false,
        };

        private static float AimReach(PlayerActionType skill) => (float)(skill switch
        {
            PlayerActionType.LaserPointer => BalanceTable.Default.Relics.LaserReach,
            PlayerActionType.SunFire => BalanceTable.Default.Relics.SunFireReach,
            _ => BalanceTable.Default.HotStones.Reach,
        }).ToDouble();

        private void SetAim(PlayerActionType? skill)
        {
            _aim = skill;
            _battleView.ShowReach(skill != null, skill != null ? AimReach(skill.Value) : 0f);
            if (skill == null)
            {
                _board.ShowCursor(null, false);
            }
        }

        /// <summary>While aiming, highlight the tile under the cursor or finger: amber in reach, red out of it.</summary>
        private void ShowStonesTarget()
        {
            if (_aim == null)
            {
                return;
            }

            var hasPointer = _gestures.TryGetPendingTap(out var pointer) || (PointerInput.TryGetHover(out pointer) && !IsOverUi(pointer));
            if (!hasPointer || !TryScreenToTile(pointer, out var tile))
            {
                _board.ShowCursor(null, false);
                return;
            }

            var reach = AimReach(_aim.Value);
            var leader = Shapes.ToWorld(_battle.Leader.Position);
            _board.ShowCursor(tile, (Shapes.ToWorld(tile) - leader).sqrMagnitude <= reach * reach);
        }

        /// <summary>Sends only the input log; the village replays the Night itself and its result must match ours.</summary>
        private async void EndNight()
        {
            SetAim(null);
            _board.ShowLeaderDestination(null);
            _phase = GamePhase.Dawn;
            _hud.ShowPhase(_phase);
            _hud.Announce(string.Empty, Time.time, 0f);
            _serverReplay = null;
            LastNightVerified = null; // until this Night's replays are in
            var replaySetup = _setup with { Inputs = _battle.InputLog.ToList() };
            try
            {
                _serverReplay = await Task.Run(() => Battle.Simulate(replaySetup), _cts.Token); // a whole Night: off the main thread
                var outcome = await SendVillage(new ResolveNight(Guid.NewGuid(), _plan.Night, _battle.InputLog.ToList()));
                _lastReport = outcome.Report;
                LastNightVerified = outcome.Report != null && outcome.Report.StateHashHex == _battle.Result.StateHashHex && _serverReplay == _battle.Result;
            }
            catch (OperationCanceledException)
            {
                return; // the game is closing
            }
            catch (Exception e)
            {
                LastNightVerified = false;
                VillageFailed(e);
            }

            _hud.ShowDawn(_battle.Result, LastNightVerified == true ? _battle.Result : (_serverReplay ?? _battle.Result) with { StateHash = 0 }, _battle.InputLog.Count);
        }

        private async void BackToDusk()
        {
            var report = _lastReport;
            var acknowledge = _showingReport;
            _lastReport = null;
            _showingReport = false;
            FrameRateLogger.Mark("BackToDusk");
            _battleView.Clear();
            _board.ClearStructureHealth();
            _battle = null;
            _framePending = 2;
            _audio.SetNight(false);
            _phase = GamePhase.Dusk;
            FrameStage(); // back to the whole village
            _hud.ShowPhase(_phase);
            Announce(report != null && report.Salvage > 0
                ? $"Dawn. {report.Salvage} Scrap salvaged from {Ruler.Short}'s dropped horseshoes."
                : "Dawn. Rebuild, rearrange, prepare for tomorrow's disappointment.");
            try
            {
                if (acknowledge)
                {
                    await SendVillage(new AcknowledgeReport(Guid.NewGuid()));
                }

                _night = (_snapshot?.LastNightRecorded ?? 0) + 1;
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

        // ---------------- Helpers ----------------

        /// <summary>The tutorial's step this frame (Dusk only, no card or report in the way) and, for a building, where it is on screen.</summary>
        private const string DrawerCollapsedKey = "hud.drawer.collapsed";
        private const string MapHelpSeenKey = "map.help.seen";
        /// <summary>The errand note is open (2026-10-16: it starts as one line; "the errands take lots of space").</summary>
        private const string ErrandOpenKey = "hud.errand.open";

        private static bool Pref(string key) => PlayerPrefs.GetInt(key, 0) == 1;

        private static void SetPref(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        private CoachStep CoachNow(out Vector2? point)
        {
            point = null;
            if (_phase != GamePhase.Dusk || _snapshot == null || _showingReport)
            {
                return CoachStep.None;
            }

            var step = TutorialCoach.Step(_snapshot.Errand, _tool?.Type, _snapshot.Buildings, BuildingTable.Default,
                studying: _snapshot.Research?.Any(t => t.State == TechState.Studying) == true, shielded: _snapshot.ShieldEndsAtUtc != null);
            if (_tool != null && step.Target != CoachTarget.GhostConfirm)
            {
                return CoachStep.None; // placing something else: the ghost has the player's attention (the note steps aside too)
            }
            if (step.Target == CoachTarget.Building && step.BuildingId != null)
            {
                var building = _snapshot.Buildings.FirstOrDefault(b => b.Id == step.BuildingId.Value);
                var panel = _document.rootVisualElement.panel;
                if (building == null || panel == null)
                {
                    return CoachStep.None;
                }

                var centre = building.Tiles.Aggregate(Vector3.zero, (sum, t) => sum + Shapes.ToWorld(t)) / building.Tiles.Count;
                var screen = _camera.WorldToScreenPoint(centre + Vector3.up * 1.4f);
                if (screen.z <= 0f)
                {
                    return CoachStep.None;
                }

                point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            }

            return step;
        }

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
            // Golden-Mud haze: the world fades into the sky instead of ending in blackness.
            var haze = Shapes.Hex("#b8a47a");
            _camera.backgroundColor = haze;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 85f;
            _rig = new CameraRig(
                _camera,
                offset: new Vector3(0f, 16.5f, -10.4f),
                boardMin: new Vector2(0f, -(VillageMap.Demo.Height - 1)),
                boardMax: new Vector2(VillageMap.Demo.Width - 1, 0f));
            // Pan and zoom out over the whole valley: locked forest, the edge and the river (WorldScenery.RiverPath).
            var (roamMin, roamMax) = VillageFraming.RoamBounds(VillageMap.Demo);
            _rig.SetRoam(roamMin, roamMax);

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
            return VillageMap.Demo.Contains(tile);
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
            settings.referenceDpi = 146f; // 2026-10-14: ~10% bigger than the 160 dp reference (owner: text a little bigger)
            settings.fallbackDpi = 160f;

            var document = gameObject.AddComponent<UIDocument>();
            _document = document;
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Hud"));

            _hud = new HudView(root);
            root.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button)
                {
                    _audio.Play("tap", 0.6f);
                }
            }, TrickleDown.TrickleDown);
            _hud.SetAudio(_audio.SoundOn, _audio.MusicOn);
            _hud.SoundToggled += () =>
            {
                _audio.SetSound(!_audio.SoundOn);
                _hud.SetAudio(_audio.SoundOn, _audio.MusicOn);
            };
            _hud.MusicToggled += () =>
            {
                _audio.SetMusic(!_audio.MusicOn);
                _hud.SetAudio(_audio.SoundOn, _audio.MusicOn);
            };
            _hud.BuildCardSelected += type =>
            {
                _tool = null;
                _board.HideBuilding(null);
                _buildSelection = type;
                _cardBuilding = null;
                if (type != null && !BuildingTable.IsPaintable(type.Value) && _snapshot != null)
                {
                    var near = type == BuildingType.ComfyAltar && VillageMap.Demo.StoneCircle is { } circle
                        ? circle // it goes in the stone circle and nowhere else: start it there
                        : CentreTile(); // owner 2026-10-14: the ghost appears where the player is looking
                    _tool = PlacementTool.StartNew(type.Value, near, _snapshot, VillageMap.Demo, BuildingTable.Default);
                    BringIntoView(_tool.Anchor);
                }

                RefreshVillage();
            };
            _hud.GhostConfirmClicked += ConfirmTool;
            _hud.ReplayIntroClicked += () => PlayIntro(VillageComposition.CreateIntroProgress());
            SetUpMap(root);
            _hud.VillageCodeClicked += ShowVillageCode;
            _hud.RecoverClicked += RecoverVillage;
            _hud.GhostCancelClicked += CancelTool;
            _hud.GhostRotateClicked += () => _tool?.Rotate();
            _hud.PaintDoneClicked += () =>
            {
                _buildSelection = null;
                RefreshVillage();
            };
            _hud.MoveClicked += () =>
            {
                var building = _cardBuilding != null ? _snapshot?.Buildings.FirstOrDefault(b => b.Id == _cardBuilding.Value) : null;
                if (building == null)
                {
                    return;
                }

                _cardBuilding = null;
                _buildSelection = null;
                _tool = PlacementTool.StartMove(building);
                _board.HideBuilding(building.Id);
                RefreshVillage();
            };
            _hud.RepairAllClicked += () => SendSimple(new RepairAll(Guid.NewGuid()));
            _hud.UpgradeClicked += () =>
            {
                if (_cardVisitor)
                {
                    _cardVisitor = false;
                    SendSimple(new AnswerVisitor(Guid.NewGuid(), true));
                    return;
                }

                if (_cardTerrain != null)
                {
                    var tile = _cardTerrain.Value;
                    if (VillageMap.Demo.RoadTiles(_snapshot.HearthLevel).Contains(tile))
                    {
                        SendSimple(new PaveRaidersRoad(Guid.NewGuid())); // the card stays: the next paving shows on it
                        return;
                    }

                    _cardTerrain = null;
                    SendSimple(new ClearTerrain(Guid.NewGuid(), tile));
                    return;
                }

                SendFromCard(b => b.Type == BuildingType.Hearth ? (BaseCommand)new UpgradeHearth(Guid.NewGuid()) : new UpgradeBuilding(Guid.NewGuid(), b.Id));
            };
            _hud.DemolishClicked += () =>
            {
                if (_cardVisitor)
                {
                    _cardVisitor = false;
                    SendSimple(new AnswerVisitor(Guid.NewGuid(), false)); // sent on their way
                    return;
                }

                SendFromCard(b => new DemolishBuilding(Guid.NewGuid(), b.Id), closeCard: true);
            };
            _hud.FinishNowClicked += () => SendFromCard(_ => new FinishConstructionNow(Guid.NewGuid()));
            _hud.HurryClicked += () => SendFromCard(b => new SpendTowels(Guid.NewGuid(), TowelUse.Hurry, b.Id + 1)); // this building (id + 1), not just the first to finish
            _hud.DutyClicked += () => SendFromCard(b => new SetDuty(Guid.NewGuid(), b.Id, !b.Patrol));
            _hud.NotableClicked += () => SendFromCard(b =>
            {
                var (_, id, target) = VillagePresenter.NotableButton(_snapshot, BuildingTable.Default, b);
                return new AssignNotable(Guid.NewGuid(), id ?? string.Empty, target);
            });
            _hud.PackUpClicked += () => SendFromCard(b => new PackUpTower(Guid.NewGuid(), b.Id), closeCard: true);
            _hud.PackUpAllClicked += () => SendFromCard(_ => new PackUpTowers(Guid.NewGuid()), closeCard: true);
            _hud.CancelBuildClicked += () =>
            {
                if (Armed("cancel-build:" + _cardBuilding, "Tap again to cancel. The cost comes back; the time doesn't."))
                {
                    SendFromCard(b => new CancelConstruction(Guid.NewGuid(), b.Id));
                }
            };
            _hud.RepairClicked += () => SendFromCard(b => new RepairBuilding(Guid.NewGuid(), b.Id));
            _hud.CrewChanged += crew => SendFromCard(b => new SetCrew(Guid.NewGuid(), b.Id, crew));
            _hud.DevRaiseTensionClicked += () => SendSimple(new DebugRaiseTension(Guid.NewGuid(), 50));
            _hud.DevSkipWarningClicked += () => SendSimple(new DebugSkipWarning(Guid.NewGuid()));
            _hud.DevFillStoresClicked += () => SendSimple(new DebugFillStores(Guid.NewGuid()));
            _hud.DevEndShieldClicked += () => SendSimple(new DebugEndShield(Guid.NewGuid()));
            _hud.BuildingCardClosed += () =>
            {
                _cardBuilding = null;
                _cardTerrain = null;
                _cardVisitor = false;
            };
            _hud.StartNightClicked += () =>
            {
                if (LampsWait() && !_devTools)
                {
                    Announce("The lamps can wait. Build first: follow the errand note.");
                    return;
                }

                StartNight();
            };
            _hud.ErrandClicked += OnErrandClicked;
            _hud.DailyClaimClicked += async slot =>
            {
                var outcome = await SendVillage(new ClaimDaily(Guid.NewGuid(), slot));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    _audio.Play("claim");
                    Announce($"The Prophet ticks it off the list. +{VillagePresenter.ShortCost(outcome.Collected)}");
                }
            };
            _hud.DailySwapClicked += async slot =>
            {
                var outcome = await SendVillage(new SpendTowels(Guid.NewGuid(), TowelUse.Reroll, slot));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    Announce("Two Towels change hands. The Prophet crosses something out and writes something else.");
                }
            };
            _hud.ThermosClicked += async () =>
            {
                var before = _snapshot?.Daily?.Cosmetics.Count ?? 0;
                var outcome = await SendVillage(new ClaimThermos(Guid.NewGuid()));
                if (outcome.Status == CommandStatus.Accepted)
                {
                    _audio.Play("claim");
                    var week = (_snapshot?.Daily?.Cosmetics.Count ?? 0) > before;
                    Announce(week
                        ? $"A whole week of lists! {DailyBook.StreakTowels} more Towels, and something new by the Hearth."
                        : $"The Prophet unscrews the Thermos. Tea, mostly. +{DailyBook.ThermosTowels} Towels.");
                }
            };
            _hud.PillClicked += key =>
            {
                if (_snapshot != null)
                {
                    Announce(VillagePresenter.PillLine(_snapshot, BuildingTable.Default, key));
                }
            };
            _hud.SluiceClicked += () => _battle?.Intervene(PlayerActionType.OpenSluiceGate);
            _hud.VoiceClicked += () => _battle?.Intervene(PlayerActionType.PhantomVoice);
            _hud.StonesClicked += ToggleAimingStones;
            // Tonight's relics fire themselves (owner 2026-10-15: "laser should act automatically"); a Night planned
            // before that still aims them by hand.
            _hud.LaserClicked += () =>
            {
                if (_battle?.AutoRelics == true) Announce("The laser picks its own raider: whoever's at a building, or nearest the Hearth.");
                else ToggleAim(PlayerActionType.LaserPointer, "The Finger of God: tap a raider inside the ring.");
            };
            _hud.TorchClicked += () =>
            {
                if (_battle?.AutoRelics == true) Announce("The torch lights itself when raiders crowd the Prophet.");
                else _battle?.Intervene(PlayerActionType.DynamoTorch);
            };
            _hud.SunFireClicked += () =>
            {
                if (_battle?.AutoRelics == true) Announce("The magnifier finds the thickest knot of raiders while the sun's up.");
                else ToggleAim(PlayerActionType.SunFire, "Sun-fire: tap raiders inside the ring while the sun is up.");
            };
            _hud.SpeedClicked += () => _speed = _speed == 1 ? 2 : _speed == 2 ? 4 : 1;
            _hud.RecenterClicked += () => _rig.Reset();
            _hud.BackToDuskClicked += BackToDusk;
            _hud.ResetTipsClicked += () =>
            {
                _tips.Reset();
                SetPref(ErrandOpenKey, true);
                _hud.SetErrandFolded(false);
                Announce("Tips are back. The Prophet clears his throat.");
            };
            // Folding things away (owner 2026-10-14), remembered on the device.
            _hud.SetDrawerCollapsed(Pref(DrawerCollapsedKey));
            _hud.SetErrandFolded(!Pref(ErrandOpenKey));
            _hud.DrawerToggleClicked += () =>
            {
                var collapsed = !_hud.DrawerCollapsed;
                _hud.SetDrawerCollapsed(collapsed);
                SetPref(DrawerCollapsedKey, collapsed);
                _framePending = 2;
            };
            _hud.ErrandFoldClicked += () =>
            {
                var folded = !_hud.ErrandFolded;
                _hud.SetErrandFolded(folded);
                SetPref(ErrandOpenKey, !folded);
                RefreshVillage();
            };
            _hud.ShowPhase(_phase);
        }

        private void UpdateHud()
        {
            if (_village is RemoteBaseGateway remote)
            {
                _hud.SetCloudStatus(remote.Online
                    ? remote.PendingCount == 0 ? "Cloud save: synced ✓" : $"Cloud save: sending {remote.PendingCount}…"
                    : remote.PendingCount == 0 ? "Cloud save: offline" : $"Cloud save: offline · {remote.PendingCount} waiting");
            }

            var coach = CoachNow(out var coachPoint);
            _hud.ShowCoach(coach, coachPoint);
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
                var sluice = _battle.IsSluiceReady ? $"Sluice ×{_battle.SluiceChargesLeft}"
                    : _battle.SluiceChargesLeft == 0 ? "Cistern empty" : $"Sluice {Seconds(_battle.SluiceReadyAtTick)}s";
                var leader = _battle.Leader;
                var swooning = leader.IsSwooning(_battle.Tick);
                var voice = swooning ? "Fainted" : _battle.IsVoiceReady ? "Voice" : $"Voice {Seconds(leader.VoiceReadyAtTick)}s";
                var aimingStones = _aim == PlayerActionType.HotStones;
                var stones = swooning ? "Fainted" : aimingStones ? "Pick a tile" : _battle.IsHotStonesReady ? "Hot Stones" : $"Stones {Seconds(leader.HotStonesReadyAtTick)}s";
                var alive = _battle.Enemies.Count(e => e.IsActive);
                var hint = swooning ? $"The Prophet is face down in the mud. Up in {Seconds(leader.SwoonedUntilTick)}s: tap where he should run."
                    : aimingStones ? "Tap a tile inside the pebble ring, or Hot Stones again to cancel."
                    : _aim == PlayerActionType.LaserPointer ? "Tap a raider inside the ring: the red dot blinds it. Tap the pointer again to cancel."
                    : _aim == PlayerActionType.SunFire ? "Tap raiders inside the ring: the glass sets them alight. Tap it again to cancel."
                    : "Tap the ground to move the Prophet. Enemies stop to fight him.";
                _hud.UpdateNight(new NightHudState(
                    _night, _battle.HearthHealth, _battle.HearthMaxHealth, leader.Health, leader.MaxHealth,
                    sluice, _battle.IsSluiceReady, voice, _battle.IsVoiceReady, stones, _battle.IsHotStonesReady || aimingStones, aimingStones, _speed,
                    _battle.CurrentWave, _battle.WaveCount, alive, hint,
                    RelicButton(Relic.LaserPointer, "Laser", leader.LaserReadyAtTick, _battle.IsLaserReady, PlayerActionType.LaserPointer, swooning),
                    leader.IsTorchLit(_battle.Tick)
                        ? new RelicButtonState(_battle.Relics.HasFlag(Relic.DynamoTorch), $"Lit {Seconds(leader.TorchLitUntilTick)}s", false)
                        : RelicButton(Relic.DynamoTorch, "Torch", leader.TorchReadyAtTick, _battle.IsTorchReady, null, swooning),
                    !_battle.IsSunUp && _battle.Relics.HasFlag(Relic.Magnifier)
                        ? new RelicButtonState(true, "Sun's down", false)
                        : RelicButton(Relic.Magnifier, "Sun-fire", leader.SunFireReadyAtTick, _battle.IsSunFireReady, PlayerActionType.SunFire, swooning)));
            }
        }

        /// <summary>
        /// On first frame and on every rotation: restack the HUD, then re-frame the camera once UI Toolkit has laid
        /// the new HUD out (a couple of frames later), so the board fits between the status panel and the bar.
        /// </summary>
        /// <summary>Captures render at a phone's size without one: the screen the layout and the camera frame for.</summary>
        public Vector2Int? ScreenOverride { get; set; }

        /// <summary>The camera's stage: the raiders' road at Night, the village by day. Framed again on the next frames.</summary>
        private void FrameStage()
        {
            var (min, max) = _phase == GamePhase.Night ? VillageFraming.NightBounds(VillageMap.Demo, _plan?.Road ?? 0) : VillageFraming.HomeBounds(VillageMap.Demo, _openLevel);
            _rig.SetBounds(min, max);
            _framePending = 2;
        }

        private void TrackOrientation()
        {
            var size = ScreenOverride ?? new Vector2Int(Screen.width, Screen.height);
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

        /// <summary>
        /// Everything turns, square or not, so a Slurry Chute can face the gate (owner 2026-10-16: "we should be able to
        /// always rotate them"); only fence and wall pieces don't, as they join up with their neighbours by themselves.
        /// </summary>
        private static bool CanTurn(BuildingSpec spec) =>
            spec.Type == BuildingType.Gate || (spec.Type != BuildingType.WattleFence && spec.Type != BuildingType.WoodenWall);

        private float Seconds(int readyAtTick) => Mathf.Ceil((readyAtTick - _battle.Tick) / (float)BalanceTable.TicksPerSecond);


        private bool IsOverUi(Vector2 screen) => _hud.IsOverUi(screen);
    }
}
