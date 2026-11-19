using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>One relic button on the Night bar: hidden until the relic is found.</summary>
    public readonly struct RelicButtonState
    {
        public RelicButtonState(bool shown, string text, bool ready, bool aiming = false, float cooldown = 0f)
        {
            Cooldown = cooldown;
            Shown = shown;
            Text = text;
            Ready = ready;
            Aiming = aiming;
        }

        public bool Shown { get; }
        public string Text { get; }
        public bool Ready { get; }
        public bool Aiming { get; }

        /// <summary>How much of its cooldown is left (0: ready, 1: just used), for the chip's ring.</summary>
        public float Cooldown { get; }
    }

    /// <summary>A gap between waves as the HUD shows it (spec 2026-11-17 fun first, C2–C3).</summary>
    public sealed class NightGap
    {
        public int NextWave;
        public int SecondsLeft;

        /// <summary>How much of the gap is left (1 → 0): the call button's ring.</summary>
        public float Left;

        /// <summary>The boons on offer (title, what it does); empty once one is picked.</summary>
        public (string Title, string Text)[] Boons = System.Array.Empty<(string, string)>();

        /// <summary>The boon picked for the coming wave, or null.</summary>
        public string Picked;
    }

    /// <summary>What the Night bar shows. Built by the controller each frame from the live battle.</summary>
    public readonly struct NightHudState
    {
        public NightHudState(
            int night, int hearth, int hearthMax, int prophet, int prophetMax, string sluice, bool sluiceReady, string voice, bool voiceReady,
            string stones, bool stonesReady, bool aiming, int speed, int wave, int waveCount, int alive, string status,
            RelicButtonState laser = default, RelicButtonState torch = default, RelicButtonState sunFire = default,
            float sluiceCooldown = 0f, float voiceCooldown = 0f, float stonesCooldown = 0f, int defeated = 0,
            NightGap gap = null, string objective = null)
        {
            Defeated = defeated;
            Gap = gap;
            Objective = objective;
            SluiceCooldown = sluiceCooldown;
            VoiceCooldown = voiceCooldown;
            StonesCooldown = stonesCooldown;
            Laser = laser;
            Torch = torch;
            SunFire = sunFire;
            Night = night;
            Hearth = hearth;
            HearthMax = hearthMax;
            Prophet = prophet;
            ProphetMax = prophetMax;
            Sluice = sluice;
            SluiceReady = sluiceReady;
            Voice = voice;
            VoiceReady = voiceReady;
            Stones = stones;
            StonesReady = stonesReady;
            Aiming = aiming;
            Speed = speed;
            Wave = wave;
            WaveCount = waveCount;
            Alive = alive;
            Status = status;
        }

        /// <summary>How much of each skill's cooldown is left (0: ready, 1: just used), for the shade that drains away.</summary>
        public float SluiceCooldown { get; }

        public float VoiceCooldown { get; }

        public float StonesCooldown { get; }

        public int Night { get; }
        public int Hearth { get; }
        public int HearthMax { get; }
        public int Prophet { get; }
        public int ProphetMax { get; }
        public string Sluice { get; }
        public bool SluiceReady { get; }
        public string Voice { get; }
        public bool VoiceReady { get; }
        public string Stones { get; }
        public bool StonesReady { get; }

        /// <summary>Hot Stones is armed: the next tap on the board picks where they land.</summary>
        public bool Aiming { get; }
        public int Speed { get; }
        public int Wave { get; }
        public int WaveCount { get; }
        public int Alive { get; }

        /// <summary>Raiders driven off so far tonight (nobody dies: they run home) (spec 2026-11-17 fun first, B7).</summary>
        public int Defeated { get; }

        /// <summary>Between waves (spec 2026-11-17 fun first, C2–C3): the boons and the call, or null during a wave.</summary>
        public NightGap Gap { get; }

        /// <summary>"Third star: no building wrecked ✓", or null for a Night planned before.</summary>
        public string Objective { get; }

        /// <summary>The one-line hint under the skills.</summary>
        public string Status { get; }

        public RelicButtonState Laser { get; }
        public RelicButtonState Torch { get; }
        public RelicButtonState SunFire { get; }
    }

    /// <summary>
    /// The HUD (spec: HUD Kit & Game Frame), built in code with UI Toolkit and styled by Resources/UI/Hud.uss.
    /// Layout C on parchment: a frame on top (resource pills, portrait, settings) with a strip under it (Tension at
    /// Dusk, waves + health at Night), shortcut columns at the sides, and a bottom bar. A <c>portrait</c> /
    /// <c>landscape</c> class on the root restacks it. It only displays state and raises events; the
    /// <see cref="GameController"/> decides what they mean.
    /// </summary>
    public sealed class HudView
    {
        /// <summary>Extra inset beyond the OS safe area, for rounded display corners (panel points).</summary>
        private const float CornerMargin = 14f;
        private const float NoteSeconds = 3f;
        private const float FloatSeconds = 1.2f;

        private readonly VisualElement _root;
        private readonly VisualElement _top;
        private readonly VisualElement _pillRow;
        private readonly Dictionary<string, (VisualElement Pill, Label Value)> _pills = new Dictionary<string, (VisualElement, Label)>();
        private readonly Label _hearthBadge;
        private readonly Label _hearthLine;
        private readonly VisualElement _lordFace;
        private readonly Label _seasonLine;
        private readonly Label _comfort;
        private readonly VisualElement _tensionStrip;
        private readonly VisualElement _tensionCover;
        private readonly Label _tensionNote;
        private readonly VisualElement _waveStrip;
        private readonly Label _waveLabel;
        private readonly VisualElement _hearthFill;
        private readonly Label _hearthValue;
        private readonly VisualElement _prophetFill;
        private readonly Label _prophetValue;
        private readonly VisualElement _sideLeft;
        private readonly VisualElement _sideRight;
        private readonly Label _lockedNote;
        private readonly VisualElement _settings;
        private readonly VisualElement _howTo;
        private readonly Label _probe;
        private readonly Label _toast;
        private readonly VisualElement _daily;
        private readonly Label _dailyHeading;
        private readonly VisualElement _dailyLines;
        private readonly Button _thermos;
        private readonly Label _dailyFooter;
        private string _dailySignature;
        private readonly VisualElement _card;
        private readonly Label _cardTitle;
        private readonly Label _cardSummary;
        private readonly Label _cardFlavour;
        private readonly Label _cardStats;
        private readonly VisualElement _cardStars;
        private readonly Button _cardNext;
        private readonly VisualElement _stamp;
        private readonly VisualElement _bar;
        private readonly VisualElement _duskBar;
        private readonly VisualElement _nightBar;
        private readonly ScrollView _drawer;
        private readonly Button _paintDone;
        private readonly Button _repairAll;
        private readonly VisualElement _ghostActions;
        private readonly Button _ghostConfirm;
        private readonly Button _ghostRotate;
        private readonly Label _ghostProblem;
        private Button _move;
        private readonly Label[] _bubbles = new Label[3];
        private readonly Func<Vector2?>[] _bubbleAnchor = new Func<Vector2?>[3];
        private readonly float[] _bubbleUntil = new float[3];
        private readonly Dictionary<BuildingType, Button> _cards = new Dictionary<BuildingType, Button>();
        private string _cardsSignature;
        private readonly VisualElement _buildingCard;
        private readonly Label _buildingTitle;
        private readonly Label _buildingOutput;
        private readonly Label _buildingReady;
        private readonly Label _buildingRemaining;
        private readonly Button _upgrade;
        private readonly Button _finishNow;
        private readonly Button _demolish;
        private Button _repair;
        private readonly Button _sluice;
        private readonly Button _voice;
        private readonly Button _stones;
        private readonly VisualElement _relicRow;
        private readonly Button _laser;
        private readonly Button _torch;
        private readonly Button _sunFire;
        private readonly Button _speed;
        private readonly Label _nightStatus;
        private readonly Dictionary<Button, Label> _skillText = new Dictionary<Button, Label>();
        private readonly Label[] _floaters = new Label[12];
        private readonly float[] _floatStart = new float[12];
        private readonly Vector2[] _floatAt = new Vector2[12];
        private int _nextFloater;
        private readonly Button _errand;
        private readonly Label _errandHeading;
        private readonly Label _errandText;
        private readonly Label _errandReward;
        private readonly Label _errandClaim;
        private VisualElement _hinted;
        private Label _cloud;
        private Button _drawerToggle;
        private VisualElement _drawerChevron;
        private VisualElement _buildTabs;
        private string _buildTab = VillagePresenter.Work;
        private readonly Dictionary<BuildingType, string> _cardCategory = new Dictionary<BuildingType, string>();
        private Button _errandFold;
        private bool _drawerCollapsed;
        private bool _errandFolded;
        private readonly HashSet<string> _openFeatures = new HashSet<string>();
        private readonly Dictionary<string, Button> _featureButtons = new Dictionary<string, Button>();
        private VisualElement _devTools;
        private bool _devMode;
        private Label _accountNote;
        private Button _soundToggle;
        private Button _musicToggle;
        private readonly VisualElement _coachArrow;
        private readonly Label _coachLine;
        private CoachStep _coach = CoachStep.None;
        private Vector2? _coachPoint;
        private bool _coachSnoozed; // the player tapped the line away: quiet until the step changes
        private bool _coachScroll; // the coach's card should scroll into view once it has a layout
        private float _hintUntil;
        private float _toastUntil;
        private float _noteUntil;
        private Vector4 _padding = new Vector4(float.NaN, 0f, 0f, 0f);

        public HudView(VisualElement root)
        {
            _root = root;
            _root.AddToClassList("hud");
            _root.pickingMode = PickingMode.Ignore;

            // ---------- top: frame + strip ----------
            _top = Container("top");
            var frame = Container("frame");
            frame.name = "frame";
            var pills = Container("pills");
            _pillRow = pills;
            foreach (var (key, icon) in new[] { ("timber", "timber"), ("scrap", "scrap"), ("peat", "peat"), ("devotion", "devotion"), ("pennies", "penny"), ("people", "cult"), ("comfort", "comfort") })
            {
                var pill = Surface(key == "pennies" || key == "people" ? "pill-note" : "pill");
                pill.name = $"pill-{key}";
                pill.Add(Icon(icon));
                var value = Text(string.Empty, "pill-value");
                pill.Add(value);
                pill.Add(Text("demo", "demo-tag"));
                var capLine = Container("cap-line"); // the store's fill, a thin line under the number (F4)
                capLine.pickingMode = PickingMode.Ignore;
                var capFill = Container("cap-fill");
                capFill.pickingMode = PickingMode.Ignore;
                capLine.Add(capFill);
                pill.Add(capLine);
                var pillKey = key;
                pill.AddManipulator(new Clickable(() => PillClicked?.Invoke(pillKey)));
                _pills[key] = (pill, value);
                pills.Add(pill);
            }

            // One row (2026-10-16, "a lot going on"): the Prophet's face, the stores beside it, the settings at the end.
            var identity = Container("identity");
            var portrait = Surface("avatar"); // not "portrait": that class is the root's orientation flag
            portrait.name = "portrait";
            portrait.RegisterCallback<ClickEvent>(_ => FeatureClicked?.Invoke("prophet")); // his sheet: level and perks (spec 2026-10-20)
            _portrait = portrait;
            var clip = Container("avatar-clip"); // round mask for the face only, so the badge is not clipped
            clip.Add(Icon("portrait_prophet", "portrait-face"));
            portrait.Add(clip);
            _hearthBadge = Text("1", "hearth-badge");
            _hearthBadge.name = "hearth-badge";
            portrait.Add(_hearthBadge);
            identity.Add(portrait);
            _seasonLine = Text(string.Empty, "season-line");
            _seasonLine.name = "season-line";
            _seasonLine.pickingMode = PickingMode.Ignore;
            portrait.Add(_seasonLine);
            var label = Surface("id-label");
            label.Add(Container("tape"));
            _hearthLine = Text("Hearth Lv 1", "id-title");
            _comfort = Text(string.Empty, "id-line");
            _comfort.name = "comfort";
            label.Add(_hearthLine);
            label.Add(_comfort);
            label.AddToClassList("hidden"); // Hearth level is on the badge, Comfort has its own pill
            identity.Add(label);
            identity.Add(pills);
            var howTo = ButtonWith("?", () => ShowHowTo(!Visible(_howTo)), "round", "howto-button");
            howTo.name = "howto-button";
            identity.Add(howTo);
            var settingsButton = IconButton("settings", () => ToggleSettings(), "round");
            settingsButton.name = "settings-button";
            identity.Add(settingsButton);
            frame.Add(identity);
            _top.Add(frame);

            _tensionStrip = Container("strip");
            _tensionStrip.name = "tension-strip";
            // A real meter (spec 2026-11-17 fun first, D5): an empty track the lord's tape fills, his face at its edge.
            var tape = Container("tension-track");
            _tensionCover = Surface("hazard-fill");
            _tensionCover.name = "tension-fill";
            tape.Add(_tensionCover);
            _lordFace = Icon("portrait_lord", "lord-face");
            _lordFace.pickingMode = PickingMode.Ignore;
            tape.Add(_lordFace);
            _tensionStrip.Add(tape);
            _tensionNote = Text(string.Empty, "marker-note");
            _tensionStrip.Add(_tensionNote);
            // Who's coming tonight (spec 2026-11-17 fun first, B5): the scouting lines live on a card at Dusk, not in the fight.
            _rosterLink = Text("Who's coming ›", "roster-link");
            _rosterLink.name = "roster-link";
            _rosterLink.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation(); // the link is the roster; the rest of the strip is the lord's hall
                RosterClicked?.Invoke();
            });
            _tensionStrip.Add(_rosterLink);
            _tensionStrip.RegisterCallback<ClickEvent>(_ => StripClicked?.Invoke()); // the lord's hall (spec 2026-11-17 fun first, D2)
            _top.Add(_tensionStrip);

            _waveStrip = Container("strip");
            _waveStrip.name = "wave-strip";
            _waveLabel = Text(string.Empty, "wave-label");
            _waveLabel.name = "wave-label";
            _waveRow = Container("wave-row");
            _waveRow.Add(_waveLabel);
            _waveStrip.Add(_waveRow);
            (_hearthFill, _hearthValue) = Meter(_waveStrip, "Hearth", "hearth-fill");
            (_prophetFill, _prophetValue) = Meter(_waveStrip, "Prophet", "prophet-fill");
            _objectiveLine = Text(string.Empty, "objective-line");
            _objectiveLine.name = "objective-line";
            _waveStrip.Add(_objectiveLine);
            _top.Add(_waveStrip);

            // ---------- middle: side columns + centre stack ----------
            var middle = Container("middle");
            _sideLeft = Container("side");
            _sideLeft.name = "side-left";
            _sideRight = Container("side");
            _sideRight.name = "side-right";
            // Eight entry points, not twelve (spec 2026-11-17 fun first, D): Lands and Study at the left, the timed ones at the right.
            _sideLeft.Add(Shortcut(LockedFeatures.Get("map")));
            _sideLeft.Add(Shortcut(LockedFeatures.Get("research")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("pilgrims")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("festival")));
            var centre = Container("centre");
            _toast = Text(string.Empty, "toast", "hidden");
            _toast.name = "toast";
            _toast.pickingMode = PickingMode.Ignore;
            _lockedNote = Text(string.Empty, "locked-note", "hidden");
            _lockedNote.name = "locked-note";
            _settings = BuildSettings(out _probe);
            _buildingCard = BuildBuildingCard(out _buildingTitle, out _buildingOutput, out _buildingReady, out _buildingRemaining, out _upgrade, out _finishNow, out _demolish);
            _card = BuildCard(out _cardTitle, out _cardSummary, out _cardFlavour, out _cardStats, out _stamp, out _cardStars, out _cardNext);
            _paintDone = ButtonWith("Done", () => PaintDoneClicked?.Invoke(), "plain", "rotate-button");
            _paintDone.name = "paint-done";
            Show(_paintDone, false);
            _repairAll = ButtonWith(string.Empty, () => RepairAllClicked?.Invoke(), "plain", "repair-all-button");
            _repairAll.name = "repair-all";
            Show(_repairAll, false);

            // ✓ ✗ ↻ beside the ghost of a building being placed or moved.
            _ghostActions = Container("ghost-actions");
            _ghostActions.name = "ghost-actions";
            _ghostConfirm = ButtonWith("✓", () => GhostConfirmClicked?.Invoke(), "ghost-button", "ghost-confirm");
            _ghostConfirm.name = "ghost-confirm";
            var ghostCancel = ButtonWith("×", () => GhostCancelClicked?.Invoke(), "ghost-button", "ghost-cancel");
            ghostCancel.name = "ghost-cancel";
            _ghostRotate = ButtonWith("Turn", () => GhostRotateClicked?.Invoke(), "ghost-button", "ghost-turn");
            _ghostRotate.name = "ghost-rotate";
            var buttons = Container("ghost-row");
            buttons.Add(_ghostConfirm);
            buttons.Add(ghostCancel);
            buttons.Add(_ghostRotate);
            _ghostProblem = Text(string.Empty, "ghost-problem");
            _ghostProblem.pickingMode = PickingMode.Ignore;
            _ghostActions.Add(buttons);
            _ghostActions.Add(_ghostProblem);
            Show(_ghostActions, false);
            // The Prophet's errand: a sticky note under the frame (spec 2026-10-13 first-hour arc).
            _errand = new Button(() => ErrandClicked?.Invoke()) { name = "errand-note" };
            _errand.AddToClassList("errand-note");
            _errand.AddToClassList("note-yellow");
            _errandHeading = Text(string.Empty, "errand-heading");
            _errandText = Text(string.Empty, "errand-text");
            _errandReward = Text(string.Empty, "errand-reward");
            _errandClaim = Text("Claim ✓", "errand-claim");
            _errandFold = new Button(() => ErrandFoldClicked?.Invoke()) { name = "errand-fold", text = "–" };
            _errandFold.AddToClassList("errand-fold");
            _errandFold.RegisterCallback<ClickEvent>(e => e.StopPropagation()); // folding is not a tap on the note
            foreach (var part in new VisualElement[] { _errandHeading, _errandText, _errandReward, _errandClaim })
            {
                part.pickingMode = PickingMode.Ignore;
                _errand.Add(part);
            }

            _errand.Add(_errandFold);
            Show(_errand, false);
            centre.Add(_errand);

            // The daily list takes the note's place once the starter errands are done (spec 2026-10-15).
            _daily = new VisualElement { name = "daily-note" };
            _daily.AddToClassList("errand-note");
            _daily.AddToClassList("daily-note");
            _daily.AddToClassList("note-yellow");
            _dailyHeading = Text(string.Empty, "errand-heading");
            _dailyHeading.RegisterCallback<ClickEvent>(_ => ErrandFoldClicked?.Invoke()); // the heading folds it, like the errand
            _dailyLines = new VisualElement { name = "daily-lines" };
            _thermos = ButtonWith("Open the Thermos", () => ThermosClicked?.Invoke(), "daily-thermos");
            _thermos.name = "daily-thermos";
            _dailyFooter = Text(string.Empty, "daily-footer");
            _daily.Add(_dailyHeading);
            _daily.Add(_dailyLines);
            _daily.Add(_thermos);
            _daily.Add(_dailyFooter);
            Show(_daily, false);
            centre.Add(_daily);
            centre.Add(_toast);
            centre.Add(_lockedNote);
            middle.Add(_sideLeft);
            middle.Add(centre);
            middle.Add(_sideRight);

            // ---------- bottom bar ----------
            _bar = Panel("bar");
            _duskBar = Container("dusk-bar");
            _duskBar.name = "dusk-bar";
            _buildTabs = Container("build-tabs");
            _buildTabs.name = "build-tabs";
            _buildTabs.pickingMode = PickingMode.Position;
            foreach (var category in VillagePresenter.Categories)
            {
                var name = category;
                var tab = new Button(() => ShowBuildTab(name)) { name = $"tab-{category}", text = category };
                tab.AddToClassList("build-tab");
                _buildTabs.Add(tab);
            }

            _drawer = new ScrollView(ScrollViewMode.Horizontal) { name = "build-drawer" };
            _drawer.AddToClassList("drawer");
            _drawer.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _drawer.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _drawerToggle = new Button(() => DrawerToggleClicked?.Invoke()) { name = "drawer-toggle", text = string.Empty };
            _drawerToggle.AddToClassList("drawer-toggle");
            // A drawn chevron: the Fell fonts have no ▾ / ▴ (the phone showed a box).
            _drawerChevron = new VisualElement { pickingMode = PickingMode.Ignore, userData = false };
            _drawerChevron.style.width = 14;
            _drawerChevron.style.height = 10;
            _drawerChevron.style.marginRight = 6;
            _drawerChevron.generateVisualContent += context =>
            {
                var up = context.visualElement.userData is bool b && b;
                var w = context.visualElement.resolvedStyle.width;
                var h = context.visualElement.resolvedStyle.height;
                var p = context.painter2D;
                p.strokeColor = context.visualElement.parent?.resolvedStyle.color ?? Color.white; // the button's own text colour
                p.lineWidth = 2.2f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(1f, up ? h - 2f : 2f));
                p.LineTo(new Vector2(w / 2f, up ? 2f : h - 2f));
                p.LineTo(new Vector2(w - 1f, up ? h - 2f : 2f));
                p.Stroke();
            };
            _drawerToggle.style.flexDirection = FlexDirection.Row;
            _drawerToggle.style.alignItems = Align.Center;
            _drawerToggle.Add(_drawerChevron);
            _drawerToggle.Add(new Label("Build") { pickingMode = PickingMode.Ignore });
            _bar.Add(_drawerToggle);
            _duskBar.Add(_buildTabs);
            _duskBar.Add(_drawer);
            _duskBar.Add(_paintDone); // floats just above the cards while painting
            _duskBar.Add(_repairAll);
            var actions = Container("actions");
            actions.Add(Shortcut(LockedFeatures.Get("shop"), "bar-shortcut"));
            var start = ButtonWith("Light the Lamps", () => StartNightClicked?.Invoke(), "primary");
            start.name = "start-night";
            actions.Add(start);
            actions.Add(Shortcut(LockedFeatures.Get("cult"), "bar-shortcut"));
            _duskBar.Add(actions);

            _nightBar = Container("night-bar");
            _nightBar.name = "night-bar";
            var skills = Container("skills");
            skills.AddToClassList("skill-arc"); // round notes in an arc, bottom right (spec 2026-11-17 fun first, B6)
            _sluice = SkillButton("sluice", "note-blue", () => SluiceClicked?.Invoke());
            _voice = SkillButton("voice", "note-pink", () => VoiceClicked?.Invoke());
            _stones = SkillButton("stones", "note-yellow", () => StonesClicked?.Invoke());
            _speed = SkillButton("speed", "note-green", () => SpeedClicked?.Invoke());
            _speed.AddToClassList("speed");
            _speed.Remove(_skillRing[_speed]); // a setting, not a skill: no cooldown ring
            _skillRing.Remove(_speed);
            _relicRow = Container("relics");
            _relicRow.name = "relics";
            _laser = SkillButton("laser", "note-pink", () => LaserClicked?.Invoke());
            _torch = SkillButton("torch", "note-yellow", () => TorchClicked?.Invoke());
            _sunFire = SkillButton("magnifier", "note-blue", () => SunFireClicked?.Invoke());
            foreach (var relic in new[] { _laser, _torch, _sunFire })
            {
                relic.AddToClassList("relic"); // a small round chip with a ring timer (B6)
                _relicRow.Add(relic);
            }

            _sluice.AddToClassList("arc-high");
            _voice.AddToClassList("arc-left");
            _stones.AddToClassList("arc-corner"); // the aimed one, biggest, under the thumb
            skills.Add(_sluice);
            skills.Add(_voice);
            skills.Add(_stones);
            _waveRow.Add(_speed); // the speed is a setting, not a skill: up with the wave (spec 2026-11-17 fun first, B6)
            _nightStatus = Text(string.Empty, "hint");
            var side = Container("night-side"); // the hint over the relic chips, left of the arc
            side.Add(_nightStatus);
            side.Add(_relicRow);
            _nightBar.Add(side);
            _nightBar.Add(skills);
            _gapRow = BuildGapRow();
            _bar.Add(_gapRow);
            _bar.Add(_duskBar);
            _bar.Add(_nightBar);

            _root.Add(_top);
            _root.Add(middle);
            _root.Add(_bar);
            _root.Add(_card);      // overlays, added last so they draw over the bar
            _root.Add(_settings);
            _howTo = BuildHowTo();
            _root.Add(_howTo);
            _roster = Panel("sheet");
            _roster.AddToClassList("roster");
            _roster.name = "roster";
            Show(_roster, false);
            _root.Add(_roster);
            _root.Add(_buildingCard);
            Show(_buildingCard, false);
            // The tutorial coach (GDD 05): a hi-vis arrow at the next tap and the Prophet's line beside it.
            _coachArrow = new VisualElement { name = "coach-arrow", pickingMode = PickingMode.Ignore };
            _coachArrow.AddToClassList("coach-arrow");
            _coachArrow.generateVisualContent += DrawCoachArrow;
            _coachLine = Text(string.Empty, "coach-line", "note-yellow");
            _coachLine.name = "coach-line";
            // Tap the note: it steps aside until the next step. Never for good: a new player taps notes (the errand
            // note beside it is one), and switching the coach off by accident left the playtest with nothing to follow.
            _coachLine.RegisterCallback<ClickEvent>(_ => _coachSnoozed = true);
            _root.Add(_coachArrow);
            _root.Add(_coachLine);
            Show(_coachArrow, false);
            Show(_coachLine, false);
            for (var i = 0; i < _floaters.Length; i++)
            {
                _floaters[i] = Text(string.Empty, "floater", "hidden");
                _floaters[i].pickingMode = PickingMode.Ignore;
                _floatStart[i] = float.NegativeInfinity;
                _root.Add(_floaters[i]);
            }

            for (var i = 0; i < _edgeMarkers.Length; i++)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("edge-marker");
                marker.generateVisualContent += DrawEdgeChevron;
                var count = Text(string.Empty, "edge-count");
                count.pickingMode = PickingMode.Ignore;
                marker.Add(count);
                Show(marker, false);
                _edgeMarkers[i] = marker;
                _root.Add(marker);
            }

            for (var i = 0; i < _hitMarkers.Length; i++)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.AddToClassList("edge-marker");
                marker.AddToClassList("edge-hit");
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.AddToClassList("edge-hit-glow"); // along the edge, behind the arrow
                marker.Add(glow);
                var arrow = new VisualElement { pickingMode = PickingMode.Ignore };
                arrow.AddToClassList("edge-hit-arrow");
                arrow.generateVisualContent += DrawEdgeChevron;
                var bang = Text("!", "edge-count");
                bang.pickingMode = PickingMode.Ignore;
                arrow.Add(bang);
                marker.Add(arrow);
                Show(marker, false);
                _hitMarkers[i] = marker;
                _root.Add(marker);
            }

            _root.Add(_ghostActions); // above the board, beside the ghost
            for (var i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i] = Text(string.Empty, "bubble", "hidden");
                _bubbles[i].pickingMode = PickingMode.Ignore;
                _root.Add(_bubbles[i]);
            }
            SetPortrait(false);
            Show(_settings, false);
        }

        /// <summary>A build card was picked (null: the selected card was tapped again to cancel).</summary>
        public event Action<BuildingType?> BuildCardSelected;
        public event Action UpgradeClicked;
        public event Action DemolishClicked;
        public event Action FinishNowClicked;

        /// <summary>Spend a Towel: half an hour off the construction.</summary>
        public event Action HurryClicked;
        public event Action CancelBuildClicked;
        private Button _cancelBuild;

        /// <summary>A Guardhouse's duty switched: man the towers ⇄ patrol.</summary>
        public event Action DutyClicked;
        private Button _duty;

        /// <summary>The notable button on a card: ask one to work here, or send them home.</summary>
        public event Action NotableClicked;
        private Button _notable;

        /// <summary>Pack this tower up / pack every tower up (spec 2026-10-20 river).</summary>
        public event Action PackUpClicked;
        public event Action PackUpAllClicked;
        private Button _packUp;
        private Button _packAll;
        private VisualElement _packRow;

        private Button _hurry;
        public event Action RepairClicked;

        /// <summary>The building card's crew − / +: the crew the player now wants there.</summary>
        public event Action<int> CrewChanged;

        private VisualElement _crewRow;
        private Label _crewLine;
        private Button _crewLess;
        private Button _crewMore;
        private int _crewWanted;
        public event Action BuildingCardClosed;
        public event Action StartNightClicked;
        public event Action SluiceClicked;

        /// <summary>A boon note was tapped in a gap (its index in the offer).</summary>
        public event Action<int> BoonClicked;

        /// <summary>"Call now" in a gap: the next wave comes at once, for more salvage.</summary>
        public event Action CallWaveClicked;

        private readonly Label _objectiveLine;
        private readonly VisualElement _gapRow;
        private Label _gapNote;
        private readonly Button[] _boonButtons = new Button[3];
        private Button _callWave;
        private VisualElement _callRing;

        /// <summary>
        /// Between waves (spec 2026-11-17 fun first, C2–C3): three sticky notes, pick one or ignore them, and a round "Call
        /// now" with the gap's ring timer. Above the skills, never over the fight.
        /// </summary>
        private VisualElement BuildGapRow()
        {
            var row = Container("gap-row");
            row.name = "gap-row";
            _gapNote = Text(string.Empty, "gap-note");
            row.Add(_gapNote);
            var notes = Container("gap-notes");
            var colours = new[] { "note-yellow", "note-pink", "note-blue" };
            for (var i = 0; i < _boonButtons.Length; i++)
            {
                var index = i;
                var boon = new Button(() => BoonClicked?.Invoke(index)) { name = $"boon-{i}" };
                boon.AddToClassList("btn");
                boon.AddToClassList("boon");
                boon.AddToClassList(colours[i]);
                boon.Add(Text(string.Empty, "boon-title"));
                boon.Add(Text(string.Empty, "boon-text"));
                _boonButtons[i] = boon;
                notes.Add(boon);
            }

            _callWave = new Button(() => CallWaveClicked?.Invoke()) { name = "call-wave" };
            _callWave.AddToClassList("btn");
            _callWave.AddToClassList("call-wave");
            _callWave.AddToClassList("note-green");
            _callRing = new VisualElement { pickingMode = PickingMode.Ignore, userData = 0f };
            _callRing.AddToClassList("skill-ring");
            _callRing.generateVisualContent += DrawCooldownRing;
            _callWave.Add(_callRing);
            _callWave.Add(Text("Call now", "call-title"));
            _callWave.Add(Text("+10% Scrap", "call-text"));
            notes.Add(_callWave);
            row.Add(notes);
            Show(row, false);
            return row;
        }

        private void UpdateGap(NightGap gap)
        {
            Show(_gapRow, gap != null);
            if (gap == null)
            {
                return;
            }

            _gapNote.text = gap.Picked != null
                ? $"{gap.Picked} for wave {gap.NextWave}. It comes in {gap.SecondsLeft}s."
                : $"Wave {gap.NextWave} in {gap.SecondsLeft}s. Pick a boon, or call it now.";
            for (var i = 0; i < _boonButtons.Length; i++)
            {
                var on = i < gap.Boons.Length;
                Show(_boonButtons[i], on);
                if (on)
                {
                    ((Label)_boonButtons[i][0]).text = gap.Boons[i].Title;
                    ((Label)_boonButtons[i][1]).text = gap.Boons[i].Text;
                }
            }

            // The ring fills gold as the gap runs out and the wave draws near.
            var left = Mathf.Clamp01(gap.Left);
            if (!(_callRing.userData is float was) || Mathf.Abs(was - left) > 0.004f)
            {
                _callRing.userData = left;
                _callRing.MarkDirtyRepaint();
            }
        }
        public event Action VoiceClicked;
        public event Action StonesClicked;
        public event Action SpeedClicked;
        public event Action RecenterClicked;

        /// <summary>Ends a paint tool (roads, fences, walls).</summary>
        public event Action PaintDoneClicked;

        public event Action GhostConfirmClicked;
        public event Action GhostCancelClicked;
        public event Action GhostRotateClicked;
        public event Action MoveClicked;
        public event Action RepairAllClicked;
        public event Action BackToDuskClicked;

        /// <summary>The Dawn card's second button: straight on to the next Pilgrims' stage.</summary>
        public event Action NextStageClicked;
        public event Action ResetTipsClicked;
        public event Action ReplayIntroClicked;
        public event Action DrawerToggleClicked;
        public event Action ErrandFoldClicked;
        public event Action<string> FeatureClicked;
        public event Action DevFillStoresClicked;
        public event Action DevEndShieldClicked;

        /// <summary>Settings → Sign in with Google.</summary>
        public event Action GoogleSignInClicked;
        private Button _google;
        private Label _googleSignedIn;

        /// <summary>Once this phone is signed in with Google, Settings says so instead of offering the button again.</summary>
        public void SetGoogleSignedIn(bool signedIn)
        {
            Show(_google, GoogleAuthConfig.Enabled && !signedIn);
            Show(_googleSignedIn, GoogleAuthConfig.Enabled && signedIn);
        }
        public event Action SoundToggled;
        public event Action MusicToggled;
        public event Action LaserClicked;
        public event Action TorchClicked;
        public event Action SunFireClicked;
        public event Action DevRaiseTensionClicked;
        public event Action DevSkipWarningClicked;
        public event Action ErrandClicked;

        /// <summary>A resource pill was tapped ("timber", "scrap", "peat", "devotion", "people").</summary>
        public event Action<string> PillClicked;

        public void SetPortrait(bool portrait)
        {
            _root.EnableInClassList("portrait", portrait);
            _root.EnableInClassList("landscape", !portrait);
        }

        public void ShowPhase(GamePhase phase)
        {
            // At Night the notes and tips sit at the bottom, above the skills, never over the fight (spec 2026-11-17 fun first, B5).
            _root.EnableInClassList("night-phase", phase == GamePhase.Night);
            _root.EnableInClassList("dawn-phase", phase == GamePhase.Dawn); // the card has the screen: no barks over it
            Show(_tensionStrip, phase == GamePhase.Dusk);
            if (phase != GamePhase.Dusk)
            {
                Show(_accountNote, false);
            }
            Show(_waveStrip, phase == GamePhase.Night);
            Show(_pillRow, phase != GamePhase.Night); // the stores don't matter mid-fight; the board gets the room (spec 2026-10-16 one thing at a time)
            Show(_sideLeft, phase == GamePhase.Dusk);
            Show(_sideRight, phase == GamePhase.Dusk);
            Show(_duskBar, phase == GamePhase.Dusk);
            Show(_drawerToggle, phase == GamePhase.Dusk); // nothing to build in the dark
            Show(_nightBar, phase == GamePhase.Night);
            Show(_bar, phase != GamePhase.Dawn);
            Show(_card, false); // at Dawn it waits for its own content (ShowDawn, ShowAwayReport): a stale card's Back to Dusk was live (playtest 2026-11-18)
            Show(_prophetFill.parent.parent, phase == GamePhase.Night);
            if (phase != GamePhase.Dusk)
            {
                Show(_settings, false);
                Show(_roster, false);
                _noteUntil = 0f;
            }
        }

        public void UpdateFrame(in FrameState frame)
        {
            SetPill("timber", frame.Timber);
            SetPill("scrap", frame.Scrap);
            SetPill("peat", frame.Peat);
            SetPill("devotion", frame.Devotion);
            SetPill("pennies", frame.Pennies);
            SetPill("people", frame.People);
            _pills["peat"].Pill.EnableInClassList("cold", frame.Cold);
            _pills["people"].Pill.EnableInClassList("cold", frame.Cold);
            _hearthBadge.text = frame.HearthLevel.ToString();
            _hearthLine.text = frame.Season == null ? $"Hearth Lv {frame.HearthLevel}" : $"Hearth Lv {frame.HearthLevel} · {frame.Season}";
            // Placeholders stay out of sight until they are real (Comfort, pennies).
            _comfort.text = $"Comfort {frame.Comfort.Value}";
            Show(_comfort, !frame.Comfort.IsDemo);
            _pills["comfort"].Value.text = frame.Comfort.Value.ToString();
            Show(_pills["comfort"].Pill, !frame.Comfort.IsDemo);
            Show(_pills["pennies"].Pill, !frame.Pennies.IsDemo);
            var tension = Mathf.Clamp01(frame.Tension);
            _tensionCover.style.width = Length.Percent(tension * 100f);
            // His face rides the tape's edge (USS pulls it back by half), kept on screen: the strip runs past the screen's sides.
            _lordFace.style.left = Length.Percent(Mathf.Lerp(8f, 94f, tension));
            // What matters leads ("Shielded · 11h · next: the levy"); the season sits under the portrait (D5).
            _tensionNote.text = frame.TensionIsDemo ? $"{frame.TensionNote} (demo)" : frame.TensionNote;
            _seasonLine.text = frame.Season ?? string.Empty;
            Show(_seasonLine, frame.Season != null);
        }

        public void UpdateDusk(int night, string probe)
        {
            _probe.text = string.IsNullOrEmpty(probe) ? $"Night {night}" : $"Night {night} · {probe}";
        }

        /// <summary>Fills the build drawer; rebuilds the cards only when their content changes (called often).</summary>
        public void SetBuildCards(IReadOnlyList<BuildCardModel> cards, BuildingType? selected)
        {
            var signature = string.Join("|", cards.Select(c => $"{c.Type}:{c.Locked}:{c.Affordable}:{c.Cost}:{c.LockText}"));
            if (signature != _cardsSignature)
            {
                _cardsSignature = signature;
                _drawer.Clear();
                _cards.Clear();
                foreach (var card in cards)
                {
                    var model = card;
                    var button = new Button(() => PickCard(model)) { name = $"card-{card.Type}" };
                    button.AddToClassList("btn");
                    button.AddToClassList("build-card");
                    button.AddToClassList(NoteFor(card.Category)); // a colour per category (F4)
                    button.AddToClassList(_cards.Count % 2 == 0 ? "tilt-left" : "tilt-right");
                    button.EnableInClassList("locked", card.Locked);
                    button.EnableInClassList("unaffordable", !card.Affordable);
                    button.Add(Icon(card.Icon, "card-icon"));
                    button.Add(Text(card.Name, "card-name"));
                    button.Add(Text(card.Cost, "card-cost"));
                    button.Add(Text(card.Time, "card-time"));
                    button.Add(Text(card.Size, "card-size"));
                    button.Add(Text(card.LockText, "card-lock"));
                    foreach (var child in button.Children())
                    {
                        child.pickingMode = PickingMode.Ignore;
                    }

                    _cards[card.Type] = button;
                    _cardCategory[card.Type] = card.Category;
                    _drawer.Add(button);
                }

                ShowBuildTab(_buildTab);
            }

            foreach (var pair in _cards)
            {
                pair.Value.EnableInClassList("selected", pair.Key == selected);
            }
        }

        /// <summary>A build card's note colour: what it's for, at a glance (spec 2026-11-17 fun first, F4). Pure.</summary>
        public static string NoteFor(string category) => category switch
        {
            VillagePresenter.Homes => "note-pink",
            VillagePresenter.Defence => "note-blue",
            VillagePresenter.Work => "note-yellow",
            _ => "note-green",
        };

        /// <summary>Shows one tab of the build drawer: only its cards, scrolled back to the start.</summary>
        public void ShowBuildTab(string category)
        {
            _buildTab = category;
            foreach (var (type, button) in _cards)
            {
                Show(button, !_cardCategory.TryGetValue(type, out var c) || c == category);
            }

            foreach (var tab in _buildTabs.Children())
            {
                tab.EnableInClassList("build-tab-open", tab.name == $"tab-{category}");
            }

            _drawer.scrollOffset = Vector2.zero;
            _coachScroll = _coach.Target == CoachTarget.BuildCard; // back to the start, then to the coach's card if it's here
        }

        /// <summary>Done shows while a paint tool (roads, fences, walls) is on.</summary>
        public void ShowPaintDone(bool visible) => Show(_paintDone, visible);

        /// <summary>"Repair all (N) · cost" above the cards, or hidden (null).</summary>
        public void ShowRepairAll(string label)
        {
            Show(_repairAll, label != null);
            _repairAll.text = label ?? string.Empty;
        }

        /// <summary>✓ ✗ ↻ beside the ghost (panel coordinates of its right edge), or hidden (null).</summary>
        /// <param name="ghost">The ghost's box on screen (panel coordinates), or null to hide the buttons.</param>
        /// <param name="warning">Allowed, but worth knowing (it would stand unconnected): said without blocking ✓.</param>
        /// <param name="avoid">What the buttons must not cover (the Hearth, panel coordinates), or null.</param>
        public void ShowGhostActions(Rect? ghost, string problem, bool canRotate, string warning = null, Rect? avoid = null)
        {
            // A sheet on screen (settings, how to play) has the finger: the ghost's buttons wait behind it.
            var sheetOpen = Visible(_settings) || Visible(_howTo) || Visible(_roster);
            Show(_ghostActions, ghost != null && !sheetOpen);
            if (ghost == null || sheetOpen)
            {
                return;
            }

            // Under the ghost, centred, never on it: a finger dragging the ghost must find the ghost (owner 2026-10-16).
            // Over it instead when there's no room below (the drawer), and kept inside the screen's sides.
            var box = ghost.Value;
            var width = _ghostActions.resolvedStyle.width > 0f ? _ghostActions.resolvedStyle.width : 180f;
            var height = _ghostActions.resolvedStyle.height > 0f ? _ghostActions.resolvedStyle.height : 60f;
            var panelWidth = _root.resolvedStyle.width > 0f ? _root.resolvedStyle.width : 400f;
            var floor = Visible(_drawer) ? _drawer.worldBound.yMin - 50f : (_root.resolvedStyle.height > 0f ? _root.resolvedStyle.height : 800f) - 120f;
            var at = GhostActionsAt(box, new Vector2(width, height), panelWidth, floor, avoid);
            _ghostActions.style.left = at.x;
            _ghostActions.style.top = at.y;
            _ghostConfirm.SetEnabled(problem == null);
            Show(_ghostRotate, canRotate);
            _ghostProblem.text = problem ?? warning ?? string.Empty;
            Show(_ghostProblem, problem != null || warning != null);
        }

        /// <summary>A speech bubble that follows its speaker (panel coordinates from <paramref name="anchor"/>, null once gone).</summary>
        public void Bubble(string text, Func<Vector2?> anchor, float now)
        {
            var slot = 0;
            for (var i = 1; i < _bubbles.Length; i++)
            {
                if (_bubbleUntil[i] < _bubbleUntil[slot])
                {
                    slot = i;
                }
            }

            _bubbles[slot].text = text;
            _bubbleAnchor[slot] = anchor;
            _bubbleUntil[slot] = now + BubbleDirector.BubbleSeconds;
        }

        /// <summary>Shows the parchment card for a building, or hides it (null).</summary>
        public void ShowBuildingCard(BuildingCardModel card)
        {
            Show(_buildingCard, card != null);
            if (card == null)
            {
                return;
            }

            _buildingTitle.text = card.Title;
            _buildingOutput.text = card.Output;
            _buildingReady.text = card.Ready;
            _buildingRemaining.text = card.Remaining;
            Show(_buildingReady, !string.IsNullOrEmpty(card.Ready));
            Show(_buildingRemaining, card.IsUpgrading);
            _upgrade.text = card.UpgradeLabel;
            _upgrade.SetEnabled(card.CanUpgrade);
            Show(_finishNow, card.IsUpgrading && _devMode);
            Show(_hurry, card.HurryLabel != null);
            if (card.HurryLabel != null)
            {
                _hurry.text = card.HurryLabel;
                _hurry.SetEnabled(card.CanHurry);
            }
            Show(_cancelBuild, card.CancelLabel != null);
            _cancelBuild.text = card.CancelLabel ?? string.Empty;
            Show(_duty, card.DutyLabel != null);
            Show(_notable, card.NotableLabel != null);
            _notable.text = card.NotableLabel ?? string.Empty;
            Show(_packRow, card.CanPackUp);
            _duty.text = card.DutyLabel ?? string.Empty;
            Show(_demolish, card.CanDemolish);
            _demolish.text = card.DemolishLabel;
            Show(_move, card.CanMove);
            Show(_repair, card.IsWrecked);
            _repair.text = card.RepairLabel;
            _repair.SetEnabled(card.CanRepair);
            Show(_crewRow, card.ShowCrew);
            _crewLine.text = card.CrewLine ?? string.Empty;
            _crewWanted = card.CrewWanted;
            _crewLess.SetEnabled(card.CanCrewLess);
            _crewMore.SetEnabled(card.CanCrewMore);
        }

        private void PickCard(BuildCardModel card)
        {
            if (card.Locked)
            {
                Announce(card.LockText == "Max built" ? $"No room for another {card.Name} until the Hearth grows." : $"{card.Name} unlocks at {card.LockText}.", Time.time, prophet: true);
                return;
            }

            var selected = _cards.TryGetValue(card.Type, out var button) && button.ClassListContains("selected");
            BuildCardSelected?.Invoke(selected ? (BuildingType?)null : card.Type);
        }

        /// <summary>
        /// The wave line at Night, with a tally once a raider has been driven off (spec 2026-11-17 fun first, B7:
        /// driving one off earns no Scrap, salvage comes at Dawn, so it shows what it really is).
        /// </summary>
        public static string WaveText(int wave, int waveCount, int alive, int defeated)
        {
            var text = $"Wave {Mathf.Max(1, wave)}/{waveCount} · {alive} on the road";
            return defeated > 0 ? $"{text} · {defeated} driven off" : text;
        }

        public void UpdateNight(in NightHudState state)
        {
            _waveLabel.text = state.Gap != null
                ? NightRulesText.GapLine(state.Gap.NextWave, state.WaveCount, state.Gap.SecondsLeft, state.Defeated)
                : WaveText(state.Wave, state.WaveCount, state.Alive, state.Defeated);
            UpdateGap(state.Gap);
            _objectiveLine.text = state.Objective ?? string.Empty;
            Show(_objectiveLine, state.Objective != null);
            SetMeter(_hearthValue, _hearthFill, state.Hearth, state.HearthMax);
            SetMeter(_prophetValue, _prophetFill, state.Prophet, state.ProphetMax);
            SetSkill(_sluice, state.Sluice, state.SluiceReady, state.SluiceCooldown);
            SetSkill(_voice, state.Voice, state.VoiceReady, state.VoiceCooldown);
            SetSkill(_stones, state.Stones, state.StonesReady, state.StonesCooldown);
            _stones.EnableInClassList("aiming", state.Aiming);
            SetRelic(_laser, state.Laser);
            SetRelic(_torch, state.Torch);
            SetRelic(_sunFire, state.SunFire);
            Show(_relicRow, state.Laser.Shown || state.Torch.Shown || state.SunFire.Shown);
            SetSkill(_speed, $"{state.Speed}×", true);
            _nightStatus.text = state.Status;
            Show(_nightStatus, !string.IsNullOrEmpty(state.Status));
        }

        public void SetStartLabel(string text)
        {
            var start = _root.Q<Button>("start-night");
            if (start != null && start.text != text)
            {
                start.text = text;
            }
        }

        /// <summary>Before the first-Night errand, Light the Lamps is a small, muted note rather than the big button.</summary>
        public void SetStartWaiting(bool waiting) => _root.Q<Button>("start-night")?.EnableInClassList("waiting", waiting);

        /// <summary>The current errand (null hides the note: no errands left, or a Night is on).</summary>
        public event Action<int> DailyClaimClicked;
        public event Action<int> DailySwapClicked;
        public event Action ThermosClicked;

        /// <summary>Today's list (null: hidden): a line per errand with Claim, or Swap for 2 Towels; the Thermos; the footer.</summary>
        public void ShowDaily(DailyNote note)
        {
            Show(_daily, note != null);
            if (note == null)
            {
                return;
            }

            _dailyHeading.text = note.Heading + (_errandFolded ? " ▸" : string.Empty);
            Show(_dailyLines, !_errandFolded);
            Show(_dailyFooter, !_errandFolded);
            Show(_thermos, note.ThermosReady);
            _dailyFooter.text = note.Footer;
            var signature = _errandFolded + "#" + string.Join("|", note.Lines.Select(l => $"{l.Text}{l.Progress}{l.CanClaim}{l.Claimed}{l.CanSwap}"));
            if (signature == _dailySignature)
            {
                return;
            }

            _dailySignature = signature;
            _dailyLines.Clear();
            foreach (var line in note.Lines)
            {
                var slot = line.Slot;
                var row = Container("daily-line");
                row.pickingMode = PickingMode.Position;
                row.EnableInClassList("daily-claimed", line.Claimed);
                row.Add(Text(line.Text + (string.IsNullOrEmpty(line.Progress) ? string.Empty : $" ({line.Progress})"), "daily-text"));
                var under = Container("daily-under");
                under.pickingMode = PickingMode.Position;
                under.Add(Text(line.Claimed ? "Paid." : line.Reward, "errand-reward"));
                if (line.CanClaim)
                {
                    var claim = ButtonWith("Claim", () => DailyClaimClicked?.Invoke(slot), "daily-button");
                    claim.name = $"daily-claim-{slot}";
                    under.Add(claim);
                }
                else if (line.CanSwap)
                {
                    var swap = ButtonWith($"Swap · {DailyBook.RerollCost} Towels", () => DailySwapClicked?.Invoke(slot), "daily-button", "daily-swap");
                    swap.name = $"daily-swap-{slot}";
                    under.Add(swap);
                }

                row.Add(under);
                _dailyLines.Add(row);
            }
        }

        public void ShowErrand(ErrandNote note)
        {
            Show(_errand, note != null);
            if (note == null)
            {
                return;
            }

            _errandHeading.text = note.Progress == null ? note.Heading : $"{note.Heading} · {note.Progress}";
            // Folded, the note is one line: what to do (tap it to do it), or that it's done.
            _errandText.text = !_errandFolded ? note.Text
                : note.Done ? "Done! Tap to claim your reward."
                : note.Progress == null ? note.Text : $"{note.Text} ({note.Progress})";
            _errandReward.text = note.Reward;
            Show(_errandReward, !_errandFolded && !string.IsNullOrEmpty(note.Reward));
            Show(_errandClaim, note.Done && !_errandFolded);
            _errand.EnableInClassList("done", note.Done);
        }

        /// <summary>Makes a build card (scrolled into view) or Light the Lamps wiggle for a moment: "this one".</summary>
        public void Hint(BuildingType? card, float now)
        {
            _hinted?.RemoveFromClassList("hinted");
            _hinted = card != null ? (_cards.TryGetValue(card.Value, out var button) ? button : null) : _root.Q<Button>("start-night");
            if (_hinted == null)
            {
                return;
            }

            if (card != null)
            {
                if (_cardCategory.TryGetValue(card.Value, out var category) && category != _buildTab)
                {
                    ShowBuildTab(category); // the coach points at a card: its tab opens
                }

                _drawer.ScrollTo(_hinted);
            }

            _hinted.AddToClassList("hinted");
            _hintUntil = now + 1.6f;
        }

        /// <summary>
        /// The tutorial coach's current step; <paramref name="buildingPoint"/> is the panel point over the building
        /// when the step points at one. Placed and bounced every <see cref="Tick"/>.
        /// </summary>
        public void ShowCoach(CoachStep step, Vector2? buildingPoint)
        {
            step ??= CoachStep.None;
            var changed = step.Target != _coach.Target || step.Type != _coach.Type || step.Line != _coach.Line;
            _coach = step;
            _coachPoint = buildingPoint;
            if (changed)
            {
                _coachSnoozed = false;
                _coachLine.text = step.Line ?? string.Empty; // no ✕: the fonts lack it (a box on the phone); tapping the note still hides it
                if (step.Target == CoachTarget.BuildCard && step.Type != null && _cards.TryGetValue(step.Type.Value, out var card))
                {
                    if (_cardCategory.TryGetValue(step.Type.Value, out var category) && category != _buildTab)
                    {
                        ShowBuildTab(category); // the next card is on another tab: open it, or the coach has nothing to point at
                    }

                    _coachScroll = true;
                }
            }
        }

        /// <summary>The coach's arrow and line, as the HUD places them now (tests).</summary>
        public (bool Arrow, bool Line, Rect ArrowBounds, string Text) CoachShown =>
            (_coachArrow.resolvedStyle.display == DisplayStyle.Flex, _coachLine.resolvedStyle.display == DisplayStyle.Flex, _coachArrow.worldBound, _coachLine.text);

        private Rect? CoachTargetBounds()
        {
            switch (_coach.Target)
            {
                case CoachTarget.BuildCard when _drawerCollapsed:
                    return Visible(_drawerToggle) ? _drawerToggle.worldBound : (Rect?)null; // open the drawer first
                case CoachTarget.BuildCard when _coach.Type != null && _cardCategory.TryGetValue(_coach.Type.Value, out var category)
                                                && category != _buildTab && _buildTabs.Q<Button>($"tab-{category}") is { } tab && Visible(tab):
                    return tab.worldBound; // the player went to another tab: point back at the card's
                case CoachTarget.BuildCard when _coach.Type != null && _cards.TryGetValue(_coach.Type.Value, out var card) && Visible(card):
                    var bounds = card.worldBound;
                    return OffTheDrawer(card) ? (Rect?)null : bounds;
                case CoachTarget.GhostConfirm when Visible(_ghostConfirm):
                    return _ghostConfirm.worldBound;
                case CoachTarget.StartNight when _root.Q<Button>("start-night") is { } start && Visible(start) && start.enabledSelf:
                    return start.worldBound;
                case CoachTarget.Feature when _coach.Feature != null && _featureButtons.TryGetValue(_coach.Feature, out var feature) && Visible(feature):
                    return feature.worldBound;
                case CoachTarget.ErrandNote when Visible(_errand):
                    return _errand.worldBound;
                case CoachTarget.Building when _coachPoint != null:
                    return new Rect(_coachPoint.Value.x - 1f, _coachPoint.Value.y - 1f, 2f, 2f);
                default:
                    return null;
            }
        }

        /// <summary>True when a card's middle is scrolled past the drawer's edge (a drawer not laid out yet clips nothing).</summary>
        private bool OffTheDrawer(VisualElement card)
        {
            var edge = _drawer.contentViewport.worldBound;
            var x = card.worldBound.center.x;
            return edge.width > 0f && (x < edge.xMin || x > edge.xMax);
        }

        private static bool Visible(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return element.worldBound.width > 0f;
        }

        private void TickCoach(float now)
        {
            // A sheet (settings, a building card, the Dawn card) has the player's attention: the coach waits behind it.
            var sheetOpen = Visible(_settings) || Visible(_buildingCard) || Visible(_card) || Visible(_howTo) || _coachSnoozed;
            if (_coachScroll && _coach.Type != null && _cards.TryGetValue(_coach.Type.Value, out var coachCard) && Visible(coachCard))
            {
                _coachScroll = false;
                if (OffTheDrawer(coachCard))
                {
                    _drawer.ScrollTo(coachCard); // off the drawer's edge, the arrow would point at nothing
                }
            }

            var target = _coach.HasArrow && !sheetOpen ? CoachTargetBounds() : null;
            var hasLine = !sheetOpen && !string.IsNullOrEmpty(_coach.Line) && (target != null || !_coach.HasArrow);
            Show(_coachArrow, target != null);
            Show(_coachLine, hasLine);
            // One voice: while the coach points somewhere else, the errand note (saying the same thing) waits.
            var hush = hasLine && _coach.Target != CoachTarget.ErrandNote;
            _errand.EnableInClassList("hushed", hush);
            _daily.EnableInClassList("hushed", hush);
            if (!hasLine && target == null)
            {
                return;
            }

            var width = _root.worldBound.width;
            const float arrowW = 34f, arrowH = 40f;
            var bounce = Mathf.Abs(Mathf.Sin(now * 5f)) * 8f;
            float lineY;
            var lineX = width / 2f;
            if (target != null)
            {
                var r = target.Value;
                var below = r.center.y < _root.worldBound.height * 0.4f; // near the top: point up from underneath
                var tipX = r.center.x;
                // A card: point into it, past the tab row above, so the arrow can't read as "tap the Work tab".
                var inset = _coach.Target == CoachTarget.BuildCard ? r.height * 0.3f : -4f;
                var tipY = below ? r.yMax - inset + bounce : r.yMin + inset - bounce;
                var local = _root.WorldToLocal(new Vector2(tipX, tipY));
                _coachArrow.style.left = local.x - arrowW / 2f;
                _coachArrow.style.top = below ? local.y : local.y - arrowH;
                _coachArrow.style.rotate = new Rotate(Angle.Degrees(below ? 180f : 0f));
                lineX = local.x;
                lineY = below ? local.y + arrowH + 6f : local.y - arrowH - 8f;
                _coachLine.EnableInClassList("coach-line-above", !below);
            }
            else
            {
                var note = Visible(_errand) ? _root.WorldToLocal(new Vector2(0f, _errand.worldBound.yMax)).y : 120f;
                lineY = note + 8f;
                if (Visible(_toast) && !_toast.ClassListContains("hidden"))
                {
                    lineY = Mathf.Max(lineY, _root.WorldToLocal(new Vector2(0f, _toast.worldBound.yMax)).y + 8f); // under the toast, not on it
                }

                _coachLine.EnableInClassList("coach-line-above", false);
            }

            if (!hasLine)
            {
                return; // an arrow alone (at the note, which says it already)
            }

            var lineW = Mathf.Max(_coachLine.resolvedStyle.width, 60f);
            var lineH = _coachLine.resolvedStyle.height;
            _coachLine.style.left = Mathf.Clamp(lineX - lineW / 2f, 12f, Mathf.Max(12f, width - lineW - 12f));
            _coachLine.style.top = _coachLine.ClassListContains("coach-line-above") ? lineY - (float.IsNaN(lineH) ? 0f : lineH) : lineY;
        }

        private readonly VisualElement[] _edgeMarkers = new VisualElement[4];

        /// <summary>
        /// Raiders out of sight (spec 2026-11-17 fun first, B4): a red chevron on the board's edge, pointing their way,
        /// with how many. Positions are in viewport coordinates (0..1, bottom-left origin).
        /// </summary>
        public void ShowEdgeMarkers(IReadOnlyList<EdgeMarker> markers)
        {
            var size = _root.worldBound;
            for (var i = 0; i < _edgeMarkers.Length; i++)
            {
                var on = markers != null && i < markers.Count && !float.IsNaN(size.width);
                Show(_edgeMarkers[i], on);
                if (!on)
                {
                    continue;
                }

                var m = markers[i];
                var marker = _edgeMarkers[i];
                marker.style.left = m.At.x * size.width - 22f;
                marker.style.top = (1f - m.At.y) * size.height - 22f;
                marker.style.rotate = new Rotate(Angle.Degrees(m.AngleDegrees));
                var label = (Label)marker[0];
                label.text = m.Count.ToString();
                label.style.rotate = new Rotate(Angle.Degrees(-m.AngleDegrees)); // the number stays upright
            }
        }

        private readonly VisualElement[] _hitMarkers = new VisualElement[4];

        /// <summary>
        /// Ours hit out of sight (spec 2026-11-17 fun first, B4): a red glow along that edge and a "!" arrow pointing
        /// at it, beating at <paramref name="pulse"/> (0..1). Positions as for <see cref="ShowEdgeMarkers"/>.
        /// </summary>
        public void ShowHitMarkers(IReadOnlyList<EdgeMarker> markers, float pulse)
        {
            var size = _root.worldBound;
            for (var i = 0; i < _hitMarkers.Length; i++)
            {
                var on = markers != null && i < markers.Count && pulse > 0f && !float.IsNaN(size.width);
                Show(_hitMarkers[i], on);
                if (!on)
                {
                    continue;
                }

                var m = markers[i];
                var marker = _hitMarkers[i];
                marker.style.left = m.At.x * size.width - 22f;
                marker.style.top = (1f - m.At.y) * size.height - 22f;
                marker.style.rotate = new Rotate(Angle.Degrees(m.AngleDegrees));
                marker.style.opacity = pulse;
                var scale = 1f + 0.18f * pulse;
                marker[1].style.scale = new Scale(new Vector3(scale, scale, 1f));
                marker[1][0].style.rotate = new Rotate(Angle.Degrees(-m.AngleDegrees)); // the "!" stays upright
            }
        }

        private static void DrawEdgeChevron(MeshGenerationContext context)
        {
            const float s = 44f;
            var p = context.painter2D;
            p.fillColor = new Color(0.78f, 0.16f, 0.12f, 0.92f);
            p.strokeColor = new Color(0.17f, 0.08f, 0.05f);
            p.lineWidth = 3f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(s * 0.5f, 2f)); // the point, towards the raiders
            p.LineTo(new Vector2(s - 3f, s * 0.62f));
            p.LineTo(new Vector2(s * 0.5f, s * 0.48f));
            p.LineTo(new Vector2(3f, s * 0.62f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private static void DrawCoachArrow(MeshGenerationContext context)
        {
            const float w = 34f, h = 40f;
            var p = context.painter2D;
            p.fillColor = new Color(0.91f, 1f, 0.23f);
            p.strokeColor = new Color(0.17f, 0.13f, 0.08f);
            p.lineWidth = 3f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(w * 0.32f, 2f));
            p.LineTo(new Vector2(w * 0.68f, 2f));
            p.LineTo(new Vector2(w * 0.68f, h * 0.5f));
            p.LineTo(new Vector2(w - 2f, h * 0.5f));
            p.LineTo(new Vector2(w * 0.5f, h - 2f));
            p.LineTo(new Vector2(2f, h * 0.5f));
            p.LineTo(new Vector2(w * 0.32f, h * 0.5f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        /// <summary>The account line in Settings: how Google sign-in is going.</summary>
        public void ShowAccountNote(string text)
        {
            _accountNote.text = text;
            Show(_accountNote, !string.IsNullOrEmpty(text));
        }

        /// <summary>Opens (or locks and hides) a frame shortcut. True when that changed it.</summary>
        public bool SetFeatureOpen(string id, bool open)
        {
            if ((_openFeatures.Contains(id)) == open || !_featureButtons.TryGetValue(id, out var button))
            {
                return false;
            }

            Show(button.parent, open);

            if (open)
            {
                _openFeatures.Add(id);
            }
            else
            {
                _openFeatures.Remove(id);
            }

            button.EnableInClassList("locked", !open);
            var padlock = button.Q(className: "padlock");
            if (padlock != null)
            {
                padlock.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            }

            return true;
        }

        private VisualElement _portrait;

        /// <summary>A red dot on the Prophet's portrait: a perk is waiting to be chosen.</summary>
        public void SetProphetBadge(bool on)
        {
            var badge = _portrait?.Q(className: "feature-badge");
            if (badge == null && on && _portrait != null)
            {
                badge = new VisualElement { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("feature-badge");
                _portrait.Add(badge);
            }

            if (badge != null)
            {
                badge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>A red dot on a frame button: something wants you there (the lord's demand on Tithes).</summary>
        public void SetFeatureBadge(string id, bool on)
        {
            if (!_featureButtons.TryGetValue(id, out var button))
            {
                return;
            }

            var badge = button.Q(className: "feature-badge");
            if (badge == null && on)
            {
                badge = new VisualElement { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("feature-badge");
                button.Add(badge);
            }

            if (badge != null)
            {
                badge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>The root the map overlay hangs on.</summary>
        public VisualElement Root => _root;

        /// <summary>Folds the build drawer away (the board gets the room) or brings it back.</summary>
        /// <summary>The world map has the screen: the frame, the side buttons and the bar step aside (the toast stays).</summary>
        public void SetMapOpen(bool open)
        {
            // The village's own floating notes and bubbles point at the village, which isn't on screen.
            foreach (var element in new[] { _top, _bar, _sideLeft, _sideRight, _errand, _coachArrow, _coachLine, _buildingCard }.Concat(_floaters).Concat(_bubbles))
            {
                // Closing clears the inline style rather than forcing Visible: that would beat USS rules like .hushed.
                element.style.visibility = open ? new StyleEnum<Visibility>(Visibility.Hidden) : new StyleEnum<Visibility>(StyleKeyword.Null);
            }
        }

        public void SetDrawerCollapsed(bool collapsed)
        {
            _drawerCollapsed = collapsed;
            _drawer.EnableInClassList("collapsed", collapsed); // slides (USS transition) instead of snapping the bar
            Show(_buildTabs, !collapsed);
            _drawerChevron.userData = collapsed;
            _drawerChevron.MarkDirtyRepaint();
            _framePendingFromHud = true;
        }

        /// <summary>Set when the HUD's height changed (the board re-frames); read and cleared by the controller.</summary>
        public bool TakeFrameRequest()
        {
            var pending = _framePendingFromHud;
            _framePendingFromHud = false;
            return pending;
        }

        private bool _framePendingFromHud;

        public bool DrawerCollapsed => _drawerCollapsed;

        /// <summary>Folds the errand note to its heading (tap it to unfold) or opens it fully.</summary>
        public void SetErrandFolded(bool folded)
        {
            _errandFolded = folded;
            _errand.EnableInClassList("folded", folded);
            Show(_errandHeading, !folded);
            _errandFold.text = folded ? "+" : "–";
            if (folded)
            {
                Show(_errandReward, false);
                Show(_errandClaim, false);
            }
        }

        public bool ErrandFolded => _errandFolded;

        /// <summary>The sound and music switches in Settings.</summary>
        public void SetAudio(bool sound, bool music)
        {
            _soundToggle.text = sound ? "Sound: on" : "Sound: off";
            _musicToggle.text = music ? "Music: on" : "Music: off";
        }

        /// <summary>Shows the development tools (Settings row, Finish now) on a device allowed to use them.</summary>
        public void SetDevTools(bool on)
        {
            _devMode = on;
            Show(_devTools, on);
        }

        /// <summary>The cloud-save line in Settings.</summary>
        public void SetCloudStatus(string text)
        {
            if (_cloud != null && _cloud.text != text)
            {
                _cloud.text = text;
            }
        }

        /// <summary>The Dawn card for a Night the village played itself while the player was away.</summary>
        public void ShowAwayReport(NightReport report)
        {
            var won = report.HearthHealth > 0;
            SetMeter(_hearthValue, _hearthFill, report.HearthHealth, report.HearthMaxHealth);
            _cardTitle.text = won ? "While you were away… the Hearth held" : "While you were away… the Hearth went cold";
            _cardSummary.text = $"Stars {report.Stars}/3 · Hearth {report.HearthHealth}/{report.HearthMaxHealth} · {report.Wrecked.Count} wrecked · +{report.Salvage} Scrap";
            _cardFlavour.text = won
                ? "The towers did the work. The cult slept through it and calls that faith."
                : $"{Ruler.Cap(Ruler.Short)}'s men came, saw nobody in charge, and helped themselves.";
            SetStars(report.Stars);
            _cardStats.text = _devMode ? $"Played out by the village · hash {report.StateHashHex}" : "Played out by the village";
            Show(_stamp, _devMode); // the server's check is for us, not the player (spec 2026-11-17 fun first, A2)
            _stamp.EnableInClassList("verified", true);
            _stamp.EnableInClassList("mismatch", false);
            Show(_cardNext, false);
            Show(_card, true);
        }

        /// <summary>Lights one gold star per star earned (spec 2026-11-17 fun first, A2).</summary>
        private void SetStars(int stars)
        {
            for (var i = 0; i < _cardStars.childCount; i++)
            {
                _cardStars[i].EnableInClassList("on", i < stars);
            }
        }

        public void ShowDawn(DawnCardModel model, BattleResult result, BattleResult serverReplay, int commands)
        {
            var match = serverReplay == result;
            SetMeter(_hearthValue, _hearthFill, result.HearthHealth, result.HearthMaxHealth);
            _waveLabel.text = "Dawn";
            _cardTitle.text = model.Title;
            SetStars(model.Stars);
            _cardSummary.text = model.Summary;
            _cardStats.text = string.Join("\n", model.Earned);
            Show(_cardStats, model.Earned.Count > 0);
            _cardFlavour.text = model.Flavour;
            Debug.Log($"[CoC] Dawn: {commands} commands · client hash {result.StateHashHex} · server replay {serverReplay.StateHashHex} · {(match ? "verified" : "MISMATCH")}");
            // The server's replay check is for us, not the player: the stamp shows in development builds only (spec 2026-11-17 fun first, A2).
            Show(_stamp, _devMode);
            _stamp.EnableInClassList("verified", match);
            _stamp.EnableInClassList("mismatch", !match);
            _cardNext.text = model.Next ?? string.Empty;
            Show(_cardNext, model.Next != null);
            Show(_card, true);
        }

        /// <summary>The world speaks on a parchment ribbon; the Prophet (<paramref name="prophet"/>) scribbles on a sticky note.</summary>
        public void Announce(string text, float now, float seconds = 3.5f, bool prophet = false)
        {
            _toast.text = text;
            _toast.EnableInClassList("prophet", prophet);
            _toastUntil = now + seconds;
        }

        /// <summary>A marker-pen note that rises and fades above a raider (panel coordinates).</summary>
        public void Float(string text, Vector2 panelPosition, float now)
        {
            var i = _nextFloater;
            _nextFloater = (_nextFloater + 1) % _floaters.Length;
            _floaters[i].text = text;
            _floatStart[i] = now;
            _floatAt[i] = panelPosition;
        }

        public void ShowLockedNote(string id, float now)
        {
            _lockedNote.text = LockedFeatures.Get(id).Note;
            _noteUntil = now + NoteSeconds;
            Show(_settings, false);
        }

        /// <summary>Per frame: hides expired notes and keeps the root padded to the safe area.</summary>
        public void Tick(float now)
        {
            TickCoach(now);
            _toast.EnableInClassList("hidden", now >= _toastUntil || string.IsNullOrEmpty(_toast.text));
            _lockedNote.EnableInClassList("hidden", now >= _noteUntil);
            if (_hinted != null)
            {
                var live = now < _hintUntil;
                _hinted.style.rotate = live ? new Rotate(Angle.Degrees(Mathf.Sin(now * 22f) * 5f)) : StyleKeyword.Null;
                if (!live)
                {
                    _hinted.RemoveFromClassList("hinted");
                    _hinted = null;
                }
            }

            for (var i = 0; i < _floaters.Length; i++)
            {
                var age = now - _floatStart[i];
                var live = age >= 0f && age < FloatSeconds;
                _floaters[i].EnableInClassList("hidden", !live);
                if (live)
                {
                    var t = age / FloatSeconds;
                    _floaters[i].style.left = _floatAt[i].x - 40f;
                    _floaters[i].style.top = _floatAt[i].y - 36f * t;
                    _floaters[i].style.opacity = 1f - t * t;
                }
            }

            for (var i = 0; i < _bubbles.Length; i++)
            {
                var at = now < _bubbleUntil[i] ? _bubbleAnchor[i]?.Invoke() : null;
                _bubbles[i].EnableInClassList("hidden", at == null);
                if (at != null)
                {
                    // Bubbles stay below the frame and the health bars: speech never covers the numbers.
                    var floor = (_top.layout.yMax > 0f ? _top.layout.yMax : 0f) + 4f;
                    var bubbleHeight = _bubbles[i].layout.height > 1f ? _bubbles[i].layout.height : 40f;
                    var ceiling = (_bar.resolvedStyle.display == DisplayStyle.None ? _root.layout.height : _bar.worldBound.yMin) - bubbleHeight - 4f;
                    if (!_toast.ClassListContains("hidden") && _toast.worldBound.height > 0f)
                    {
                        // nor the announcement: below it when it's at the top, above it when it's at the bottom (at Night)
                        if (_toast.worldBound.center.y < _root.layout.height / 2f)
                        {
                            floor = Mathf.Max(floor, _toast.worldBound.yMax + 4f);
                        }
                        else
                        {
                            ceiling = Mathf.Min(ceiling, _toast.worldBound.yMin - bubbleHeight - 4f);
                        }
                    }

                    var bubbleWidth = _bubbles[i].layout.width > 1f ? _bubbles[i].layout.width : 180f;
                    _bubbles[i].style.left = Mathf.Clamp(at.Value.x - bubbleWidth / 2f, 4f, Mathf.Max(4f, _root.layout.width - bubbleWidth - 4f));
                    _bubbles[i].style.top = Mathf.Clamp(at.Value.y - 64f, floor, Mathf.Max(floor, ceiling));
                }
            }

            ApplySafeArea();
        }

        /// <summary>True when a screen position (pixels, bottom-left origin) is over a panel or button.</summary>
        public bool IsOverUi(Vector2 screen)
        {
            var panel = _root.panel;
            if (panel == null)
            {
                return false;
            }

            var local = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(local) != null;
        }

        /// <summary>
        /// The part of the screen between the top (frame + strip) and the bottom bar, in viewport coordinates (0..1,
        /// bottom-left origin). The camera fits the board in here. Null until UI Toolkit has done a layout pass.
        /// </summary>
        public Rect? ClearViewport()
        {
            var size = _root.worldBound;
            if (float.IsNaN(size.height) || size.height < 1f || float.IsNaN(_top.worldBound.yMax))
            {
                return null;
            }

            var top = _top.worldBound.yMax / size.height;
            var bottom = _bar.resolvedStyle.display == DisplayStyle.None ? 1f - _padding.w / size.height : _bar.worldBound.yMin / size.height;
            var left = float.IsNaN(_padding.x) ? 0f : _padding.x / size.width;
            var right = float.IsNaN(_padding.x) ? 0f : _padding.z / size.width;
            const float gap = 0.015f;
            return Rect.MinMaxRect(left + gap, 1f - bottom + gap, 1f - right - gap, 1f - top - gap);
        }

        /// <summary>"How to play" (2026-10-16: a first-time player "would have no idea how to play"): four steps on a card.</summary>
        public void ShowHowTo(bool open)
        {
            Show(_howTo, open);
            if (open)
            {
                Show(_settings, false);
            }
        }

        public bool HowToShown => Visible(_howTo);

        /// <summary>
        /// Where the ghost's ✓ ✗ Turn go (top-left, panel coordinates): under the ghost, else over it, else beside it, kept
        /// inside the sides and above <paramref name="floor"/>; the first spot that doesn't cover <paramref name="avoid"/>
        /// (the Hearth: twice covered in the playtests of 2026-11-17 and 18). Pure.
        /// </summary>
        public static Vector2 GhostActionsAt(Rect ghost, Vector2 size, float panelWidth, float floor, Rect? avoid)
        {
            Vector2 Fit(float left, float top) => new Vector2(
                Mathf.Clamp(left, CornerMargin, Mathf.Max(CornerMargin, panelWidth - size.x - CornerMargin)),
                Mathf.Clamp(top, CornerMargin, Mathf.Max(CornerMargin, floor - size.y)));
            var below = Fit(ghost.center.x - size.x / 2f, ghost.yMax + 10f);
            var above = Fit(ghost.center.x - size.x / 2f, ghost.yMin - 10f - size.y);
            var right = Fit(ghost.xMax + 10f, ghost.center.y - size.y / 2f);
            var left = Fit(ghost.xMin - 10f - size.x, ghost.center.y - size.y / 2f);
            var roomBelow = ghost.yMax + 10f + size.y <= floor;
            var candidates = roomBelow ? new[] { below, above, right, left } : new[] { above, below, right, left };
            // Never on the ghost itself either: a finger dragging it must find it (owner 2026-10-16).
            bool Clear(Vector2 at) => avoid == null || !new Rect(at, size).Overlaps(avoid.Value);
            foreach (var at in candidates)
            {
                if (Clear(at) && !new Rect(at, size).Overlaps(ghost))
                {
                    return at;
                }
            }

            return candidates[0];
        }

        /// <summary>"Who's coming ›" on the Tension strip was tapped.</summary>
        public event Action RosterClicked;

        /// <summary>The Tension strip itself was tapped: the lord's hall.</summary>
        public event Action StripClicked;

        /// <summary>The lord has written (a demand waits): the strip says so, as the Tithes button's dot once did.</summary>
        public void SetStripAlert(bool on) => _tensionStrip.EnableInClassList("alert", on);

        private readonly Label _rosterLink;
        private readonly VisualElement _roster;

        public bool RosterShown => Visible(_roster);

        /// <summary>The link on the strip: louder while the card has a raider the player hasn't read about.</summary>
        public void SetRosterLink(bool visible, bool hasNew)
        {
            Show(_rosterLink, visible);
            _rosterLink.text = hasNew ? "New raiders! ›" : "Who's coming ›";
            _rosterLink.EnableInClassList("new", hasNew);
        }

        /// <summary>The roster card (spec 2026-11-17 fun first, B5): who may come tonight, with the scout's line for each. Null closes it.</summary>
        /// <param name="objective">The coming Night's third-star objective, shown under the title (spec 2026-11-17 fun first, C1).</param>
        public void ShowRoster(string title, IReadOnlyList<RaiderRow> rows, string objective = null)
        {
            _roster.Clear();
            if (rows == null)
            {
                Show(_roster, false);
                return;
            }

            _roster.Add(Text(title, "sheet-title"));
            if (objective != null)
            {
                _roster.Add(Text($"Three stars: keep the Hearth above 80%, with {objective}.", "sheet-line", "small", "wrap", "roster-objective"));
            }

            foreach (var row in rows)
            {
                var line = Container("roster-row");
                var head = Container("roster-head");
                head.Add(Text(row.Leads ? $"{row.Name} · leads the last wave" : row.Name, "roster-name"));
                if (row.IsNew)
                {
                    head.Add(Text("New", "roster-new"));
                }

                line.Add(head);
                line.Add(Text(row.Line, "sheet-line", "small", "wrap"));
                _roster.Add(line);
            }

            var close = ButtonWith("Let them come", () => Show(_roster, false), "plain");
            close.name = "roster-close";
            _roster.Add(close);
            Show(_roster, true);
            Show(_settings, false);
            Show(_howTo, false);
        }

        private VisualElement BuildHowTo()
        {
            var sheet = Panel("sheet");
            sheet.AddToClassList("howto");
            sheet.name = "howto";
            sheet.Add(Text("How to play", "sheet-title"));
            sheet.Add(Text("You're a time traveller stuck in the year 950. Build the comfiest village in history, and keep the local lord from flattening it.", "sheet-line", "small", "wrap"));
            var steps = new (string Title, string Text)[]
            {
                ("Build", "Tap Build at the bottom, pick a building and tap the ground. Woodcutters make Timber; peat cuts make Peat, which keeps your villagers warm."),
                ("Grow", "Tap a building to collect what it made or to upgrade it. Upgrade the Hearth, the fire in the middle, to unlock more."),
                ("Spread the faith", "Send preachers from the Mission House. Places you win send you offerings, but the lord gets cross: that's the yellow-and-black tape at the top."),
                ("Survive the night", "When the tape fills, his men come. Build towers beside the road, and tap the ground to walk the Prophet: raiders chase him into your towers."),
            };
            for (var i = 0; i < steps.Length; i++)
            {
                var row = Container("howto-step");
                row.Add(Text((i + 1).ToString(), "howto-number"));
                var words = Container("howto-words");
                words.Add(Text(steps[i].Title, "howto-title"));
                words.Add(Text(steps[i].Text, "sheet-line", "wrap", "howto-text"));
                row.Add(words);
                sheet.Add(row);
            }

            sheet.Add(Text("Stuck? The yellow note under the top bar always says what to do next. Tap it and it shows you where.", "sheet-line", "wrap", "howto-tip"));
            var close = ButtonWith("Got it", () => Show(_howTo, false), "plain");
            close.name = "howto-close";
            sheet.Add(close);
            Show(sheet, false);
            return sheet;
        }

        private void ToggleSettings()
        {
            Show(_settings, _settings.style.display == DisplayStyle.None);
            _noteUntil = 0f;
        }

        private VisualElement BuildSettings(out Label probe)
        {
            var sheet = Panel("sheet");
            sheet.name = "settings-sheet";
            sheet.Add(Text("Settings", "sheet-title"));
            var recenter = ButtonWith("Recenter camera", () => RecenterClicked?.Invoke(), "plain");
            recenter.name = "recenter";
            sheet.Add(recenter);
            var howTo = ButtonWith("How to play", () => ShowHowTo(true), "plain");
            howTo.name = "settings-howto";
            sheet.Add(howTo);
            _cloud = Text("Cloud save: on this device only", "sheet-line", "small");
            _cloud.name = "cloud-status";
            sheet.Add(_cloud);
            // Sign in with Google (ADR 0007, 2026-10-20): the village follows the account, no code to write down.
            _google = ButtonWith("Sign in with Google", () => GoogleSignInClicked?.Invoke(), "plain", "wrap");
            _google.name = "google-sign-in";
            Show(_google, GoogleAuthConfig.Enabled);
            sheet.Add(_google);
            _googleSignedIn = Text("Signed in with Google ✓", "sheet-line", "small");
            _googleSignedIn.name = "google-signed-in";
            Show(_googleSignedIn, false);
            sheet.Add(_googleSignedIn);
            _accountNote = Text(string.Empty, "sheet-line", "small", "account-note");
            _accountNote.name = "account-note";
            Show(_accountNote, false);
            sheet.Add(_accountNote);
            _soundToggle = ButtonWith("Sound: on", () => SoundToggled?.Invoke(), "plain");
            _soundToggle.name = "sound-toggle";
            _musicToggle = ButtonWith("Music: on", () => MusicToggled?.Invoke(), "plain");
            _musicToggle.name = "music-toggle";
            var audioRow = Container("sheet-row");
            audioRow.Add(_soundToggle);
            audioRow.Add(_musicToggle);
            sheet.Add(audioRow);
            var intro = ButtonWith("Replay intro", () =>
            {
                Show(_settings, false);
                ReplayIntroClicked?.Invoke();
            }, "plain");
            intro.name = "replay-intro";
            var tips = ButtonWith("Tips again", () => ResetTipsClicked?.Invoke(), "plain");
            tips.name = "reset-tips";
            var replayRow = Container("sheet-row");
            replayRow.Add(tips);
            replayRow.Add(intro);
            sheet.Add(replayRow);
            // Development tools: hidden unless the game says this device may use them (SetDevTools).
            _devTools = Container("sheet-row");
            var raise = ButtonWith("Tension +50", () => DevRaiseTensionClicked?.Invoke(), "plain");
            raise.name = "dev-raise-tension";
            var skip = ButtonWith("Skip warning", () => DevSkipWarningClicked?.Invoke(), "plain");
            skip.name = "dev-skip-warning";
            var fill = ButtonWith("Fill stores", () => DevFillStoresClicked?.Invoke(), "plain");
            fill.name = "dev-fill-stores";
            _devTools.Add(raise);
            _devTools.Add(skip);
            _devTools.Add(fill);
            var shield = ButtonWith("Lift shield", () => DevEndShieldClicked?.Invoke(), "plain");
            shield.name = "dev-end-shield";
            _devTools.Add(shield);
            Show(_devTools, false);
            sheet.Add(_devTools);
            sheet.Add(Text($"Build {Application.version}", "sheet-line", "small"));
            probe = Text(string.Empty, "sheet-line", "small");
            probe.style.display = Debug.isDebugBuild ? DisplayStyle.Flex : DisplayStyle.None;
            sheet.Add(probe);
            sheet.Add(ButtonWith("Close", () => Show(_settings, false), "plain"));
            return sheet;
        }

        private VisualElement BuildBuildingCard(out Label title, out Label output, out Label ready, out Label remaining, out Button upgrade, out Button finishNow, out Button demolish)
        {
            var sheet = Panel("sheet");
            sheet.name = "building-card";
            title = Text(string.Empty, "sheet-title");
            title.name = "building-title";
            output = Text(string.Empty, "sheet-line");
            output.name = "building-output";
            ready = Text(string.Empty, "sheet-line");
            ready.name = "building-ready";
            remaining = Text(string.Empty, "sheet-line");
            remaining.name = "building-remaining";
            upgrade = ButtonWith(string.Empty, () => UpgradeClicked?.Invoke(), "plain", "wrap");
            upgrade.name = "upgrade";
            finishNow = ButtonWith("Finish now (dev)", () => FinishNowClicked?.Invoke(), "plain");
            finishNow.name = "finish-now";
            _hurry = ButtonWith("Hurry", () => HurryClicked?.Invoke(), "plain");
            _hurry.name = "hurry";
            _cancelBuild = ButtonWith("Cancel", () => CancelBuildClicked?.Invoke(), "plain", "wrap");
            _cancelBuild.name = "cancel-build";
            _duty = ButtonWith(string.Empty, () => DutyClicked?.Invoke(), "plain", "wrap");
            _duty.name = "duty";
            _notable = ButtonWith(string.Empty, () => NotableClicked?.Invoke(), "plain", "wrap");
            _notable.name = "notable";
            _packUp = ButtonWith("Pack up", () => PackUpClicked?.Invoke(), "plain");
            _packUp.name = "pack-up";
            _packAll = ButtonWith("Pack up all", () => PackUpAllClicked?.Invoke(), "plain");
            _packAll.name = "pack-all";
            _packRow = new VisualElement { name = "pack-row" };
            _packRow.style.flexDirection = FlexDirection.Row;
            foreach (var b in new[] { _packUp, _packAll })
            {
                b.style.flexGrow = 1;
                b.style.flexBasis = 0;
                _packRow.Add(b);
            }
            demolish = ButtonWith("Demolish", () => DemolishClicked?.Invoke(), "plain");
            demolish.name = "demolish";
            _move = ButtonWith("Move", () => MoveClicked?.Invoke(), "plain");
            _move.name = "move";
            _repair = ButtonWith(string.Empty, () => RepairClicked?.Invoke(), "plain");
            _repair.name = "repair";
            _crewRow = new VisualElement { name = "crew-row" };
            _crewRow.style.flexDirection = FlexDirection.Row;
            _crewRow.style.alignItems = Align.Center;
            _crewLine = Text(string.Empty, "sheet-line");
            _crewLine.name = "crew-line";
            _crewLine.style.flexGrow = 1;
            _crewLess = ButtonWith("−", () => CrewChanged?.Invoke(_crewWanted - 1), "plain");
            _crewLess.name = "crew-less";
            _crewMore = ButtonWith("+", () => CrewChanged?.Invoke(_crewWanted + 1), "plain");
            _crewMore.name = "crew-more";
            foreach (var b in new[] { _crewLess, _crewMore })
            {
                b.style.minWidth = 44;
                b.style.fontSize = 20;
            }

            _crewRow.Add(_crewLine);
            _crewRow.Add(_crewLess);
            _crewRow.Add(_crewMore);
            var close = ButtonWith("Close", () =>
            {
                Show(_buildingCard, false);
                BuildingCardClosed?.Invoke();
            }, "plain");
            close.name = "card-close";
            foreach (var element in new VisualElement[] { title, output, ready, _crewRow, _duty, _notable, remaining, _repair, upgrade, _hurry, _cancelBuild, finishNow, _move, _packRow, demolish, close })
            {
                sheet.Add(element);
            }

            return sheet;
        }

        private VisualElement BuildCard(out Label title, out Label summary, out Label flavour, out Label stats, out VisualElement stamp, out VisualElement stars,
            out Button next)
        {
            var card = Panel("card");
            card.name = "card";
            title = Text(string.Empty, "card-title");
            stars = Container("card-stars");
            for (var i = 0; i < 3; i++)
            {
                var star = Container("card-star");
                star.EnableInClassList("middle", i == 1); // the middle one stands taller
                stars.Add(star);
            }

            summary = Text(string.Empty, "card-line");
            flavour = Text(string.Empty, "card-line");
            stats = Text(string.Empty, "card-line", "card-earned");
            stamp = Container("stamp");
            stamp.name = "stamp";
            card.Add(title);
            card.Add(stars);
            card.Add(summary);
            card.Add(stats);
            card.Add(flavour);
            card.Add(stamp);
            card.Add(ButtonWith("Back to Dusk", () => BackToDuskClicked?.Invoke(), "primary"));
            next = ButtonWith(string.Empty, () => NextStageClicked?.Invoke(), "plain");
            next.name = "card-next";
            card.Add(next);
            return card;
        }

        private VisualElement Shortcut(LockedFeature feature, string extraClass = null)
        {
            var holder = Container("shortcut");
            if (extraClass != null)
            {
                holder.AddToClassList(extraClass);
            }

            var id = feature.Id;
            var button = IconButton(feature.Icon, () =>
            {
                if (_openFeatures.Contains(id))
                {
                    FeatureClicked?.Invoke(id);
                }
                else
                {
                    ShowLockedNote(id, Time.time);
                }
            }, "round", "locked");
            button.name = $"locked-{feature.Id}"; // the name stays: tests and captures find shortcuts by it
            button.Add(Container("padlock"));
            _featureButtons[id] = button;
            holder.Add(button);
            holder.Add(Text(feature.Label, "shortcut-label"));
            Show(holder, false); // nothing locked on show: a shortcut appears when it opens (spec 2026-10-16 one thing at a time)
            return holder;
        }

        private Button SkillButton(string icon, string note, Action clicked)
        {
            var button = new Button(clicked) { name = icon };
            button.AddToClassList("btn");
            button.AddToClassList("skill");
            button.AddToClassList(note);
            var ring = new VisualElement { pickingMode = PickingMode.Ignore, userData = 0f };
            ring.AddToClassList("skill-ring"); // fills round as the skill cools down (spec 2026-11-17 fun first, B6)
            ring.generateVisualContent += DrawCooldownRing;
            button.Add(ring);
            _skillRing[button] = ring;
            button.Add(Icon(icon, "skill-icon"));
            var text = Text(string.Empty, "skill-text");
            text.pickingMode = PickingMode.Ignore;
            button.Add(text);
            _skillText[button] = text;
            return button;
        }

        private void SetRelic(Button button, RelicButtonState state)
        {
            Show(button, state.Shown);
            if (state.Shown)
            {
                SetSkill(button, state.Text, state.Ready || state.Aiming, state.Cooldown);
                button.EnableInClassList("aiming", state.Aiming);
            }
        }

        private readonly VisualElement _waveRow;

        private readonly Dictionary<Button, VisualElement> _skillRing = new Dictionary<Button, VisualElement>();

        /// <summary>
        /// The radial cooldown (B6): a dark track round the note, and the part already cooled in gold, filling clockwise
        /// from the top. Ready: one full gold ring. <c>userData</c> holds how much is left (0..1).
        /// </summary>
        private static void DrawCooldownRing(MeshGenerationContext context)
        {
            var ring = context.visualElement;
            var left = ring.userData is float f ? Mathf.Clamp01(f) : 0f;
            var rect = ring.contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - 3f;
            if (radius <= 2f)
            {
                return;
            }

            var centre = rect.center;
            var p = context.painter2D;
            p.lineWidth = 4f;
            p.lineCap = LineCap.Round;
            if (left > 0f)
            {
                p.strokeColor = new Color(0.12f, 0.08f, 0.05f, 0.7f);
                p.BeginPath();
                p.Arc(centre, radius, 0f, 360f);
                p.Stroke();
            }

            var done = 1f - left;
            if (done <= 0.001f)
            {
                return;
            }

            p.strokeColor = new Color(1f, 0.83f, 0.3f);
            p.BeginPath();
            p.Arc(centre, radius, -90f, -90f + 360f * done);
            p.Stroke();
        }
        private readonly Dictionary<Button, float> _readyPopAt = new Dictionary<Button, float>();

        /// <summary>Per frame, so no queries: the label was remembered when the button was built.</summary>
        private void SetSkill(Button button, string text, bool ready, float cooldown = 0f)
        {
            _skillText[button].text = text;
            if (ready && !button.enabledSelf)
            {
                _readyPopAt[button] = Time.unscaledTime; // ready again: a little pop says so
            }

            button.SetEnabled(ready);
            if (_skillRing.TryGetValue(button, out var ring))
            {
                var left = ready ? 0f : Mathf.Clamp01(cooldown);
                if (!(ring.userData is float was) || Mathf.Abs(was - left) > 0.004f)
                {
                    ring.userData = left;
                    ring.MarkDirtyRepaint();
                }
            }

            var popping = _readyPopAt.TryGetValue(button, out var at) && Time.unscaledTime - at < 0.35f;
            button.style.scale = popping ? new Scale(Vector3.one * (1f + 0.14f * Mathf.Sin((Time.unscaledTime - at) / 0.35f * Mathf.PI))) : StyleKeyword.Null;
        }

        private void SetPill(string key, Reading reading)
        {
            var (pill, value) = _pills[key];
            // A compact number; the cap is the thin line under it, red when the store is full (spec 2026-11-17 fun first, F4).
            value.text = PillText(reading.Value);
            pill.EnableInClassList("demo", reading.IsDemo);
            pill.EnableInClassList("full", reading.Cap > 0 && reading.Value >= reading.Cap);
            pill.EnableInClassList("capped", reading.Cap > 0);
            var fill = pill.Q(className: "cap-fill");
            if (fill != null)
            {
                fill.style.width = Length.Percent(reading.Cap > 0 ? Mathf.Clamp01(reading.Value / (float)reading.Cap) * 100f : 0f);
            }
        }

        /// <summary>A pill's number: "850", "1.2k", "12k". Pure.</summary>
        public static string PillText(int value) => value < 1000 ? value.ToString() : value < 10000 ? $"{value / 1000f:0.#}k".Replace(".0k", "k") : $"{value / 1000}k";

        private static string Short(int cap) => cap < 1000 ? cap.ToString() : cap % 1000 == 0 ? $"{cap / 1000}k" : $"{cap / 1000f:0.#}k";

        private static void SetMeter(Label value, VisualElement fill, int current, int max)
        {
            var fraction = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            value.text = Mathf.Max(0, current).ToString();
            fill.style.width = Length.Percent(fraction * 100f);
        }

        private static (VisualElement Fill, Label Value) Meter(VisualElement parent, string name, string fillName)
        {
            var row = Container("meter");
            row.Add(Text(name, "meter-label"));
            var track = Surface("meter-track");
            var fill = Container("meter-fill");
            fill.name = fillName;
            track.Add(fill);
            row.Add(track);
            var value = Text(string.Empty, "meter-value");
            row.Add(value);
            parent.Add(row);
            return (fill, value);
        }

        /// <summary>UI Toolkit ignores Screen.safeArea, so pad the root ourselves (cutouts, rounded corners, gesture bar).</summary>
        private void ApplySafeArea()
        {
            var panel = _root.panel;
            if (panel == null)
            {
                return;
            }

            var area = Screen.safeArea;
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMin, Screen.height - area.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMax, Screen.height - area.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            var padding = new Vector4(
                topLeft.x + CornerMargin,
                topLeft.y + CornerMargin * 0.6f,
                full.x - bottomRight.x + CornerMargin,
                full.y - bottomRight.y + CornerMargin * 0.6f);
            if (padding == _padding)
            {
                return;
            }

            _padding = padding;
            _root.style.paddingLeft = padding.x;
            _root.style.paddingTop = padding.y;
            _root.style.paddingRight = padding.z;
            _root.style.paddingBottom = padding.w;
        }

        private static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private static VisualElement Container(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        /// <summary>A drawn piece of the frame (pill, portrait, tape): blocks world taps, unlike plain layout containers.</summary>
        private static VisualElement Surface(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        /// <summary>A panel blocks world taps (picking on), unlike plain layout containers.</summary>
        private static VisualElement Panel(string className)
        {
            var element = new VisualElement();
            element.AddToClassList("panel");
            element.AddToClassList(className);
            return element;
        }

        private static VisualElement Icon(string icon, string className = "icon")
        {
            var element = Container(className);
            element.AddToClassList("icon");
            element.AddToClassList($"icon-{icon}");
            return element;
        }

        private static Label Text(string text, params string[] classNames)
        {
            var label = new Label(text);
            foreach (var className in classNames)
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private static Button IconButton(string icon, Action clicked, params string[] classNames)
        {
            var button = ButtonWith(string.Empty, clicked, classNames);
            button.Add(Icon(icon));
            return button;
        }

        private static Button ButtonWith(string text, Action clicked, params string[] classNames)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("btn");
            foreach (var className in classNames)
            {
                button.AddToClassList(className);
            }

            return button;
        }
    }
}
