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
        public RelicButtonState(bool shown, string text, bool ready, bool aiming = false)
        {
            Shown = shown;
            Text = text;
            Ready = ready;
            Aiming = aiming;
        }

        public bool Shown { get; }
        public string Text { get; }
        public bool Ready { get; }
        public bool Aiming { get; }
    }

    /// <summary>What the Night bar shows. Built by the controller each frame from the live battle.</summary>
    public readonly struct NightHudState
    {
        public NightHudState(
            int night, int hearth, int hearthMax, int prophet, int prophetMax, string sluice, bool sluiceReady, string voice, bool voiceReady,
            string stones, bool stonesReady, bool aiming, int speed, int wave, int waveCount, int alive, string status,
            RelicButtonState laser = default, RelicButtonState torch = default, RelicButtonState sunFire = default)
        {
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
        private readonly Dictionary<string, (VisualElement Pill, Label Value)> _pills = new Dictionary<string, (VisualElement, Label)>();
        private readonly Label _hearthBadge;
        private readonly Label _hearthLine;
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
        private readonly Label _probe;
        private readonly Label _toast;
        private readonly VisualElement _card;
        private readonly Label _cardTitle;
        private readonly Label _cardSummary;
        private readonly Label _cardFlavour;
        private readonly Label _cardStats;
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
        private readonly VisualElement _coachArrow;
        private readonly Label _coachLine;
        private CoachStep _coach = CoachStep.None;
        private Vector2? _coachPoint;
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
            foreach (var (key, icon) in new[] { ("timber", "timber"), ("scrap", "scrap"), ("peat", "peat"), ("devotion", "devotion"), ("pennies", "penny"), ("people", "cult") })
            {
                var pill = Surface(key == "pennies" || key == "people" ? "pill-note" : "pill");
                pill.name = $"pill-{key}";
                pill.Add(Icon(icon));
                var value = Text(string.Empty, "pill-value");
                pill.Add(value);
                pill.Add(Text("demo", "demo-tag"));
                var pillKey = key;
                pill.AddManipulator(new Clickable(() => PillClicked?.Invoke(pillKey)));
                _pills[key] = (pill, value);
                pills.Add(pill);
            }

            frame.Add(pills);
            var identity = Container("identity");
            var portrait = Surface("avatar"); // not "portrait": that class is the root's orientation flag
            portrait.name = "portrait";
            var clip = Container("avatar-clip"); // round mask for the face only, so the badge is not clipped
            clip.Add(Icon("portrait_prophet", "portrait-face"));
            portrait.Add(clip);
            _hearthBadge = Text("1", "hearth-badge");
            _hearthBadge.name = "hearth-badge";
            portrait.Add(_hearthBadge);
            identity.Add(portrait);
            var label = Surface("id-label");
            label.Add(Container("tape"));
            _hearthLine = Text("Hearth Lv 1", "id-title");
            _comfort = Text(string.Empty, "id-line");
            _comfort.name = "comfort";
            label.Add(_hearthLine);
            label.Add(_comfort);
            identity.Add(label);
            identity.Add(Container("spacer"));
            var settingsButton = IconButton("settings", () => ToggleSettings(), "round");
            settingsButton.name = "settings-button";
            identity.Add(settingsButton);
            frame.Add(identity);
            _top.Add(frame);

            _tensionStrip = Container("strip");
            _tensionStrip.name = "tension-strip";
            var tape = Surface("hazard-track");
            _tensionCover = Container("hazard-cover");
            tape.Add(_tensionCover);
            _tensionStrip.Add(tape);
            _tensionNote = Text(string.Empty, "marker-note");
            _tensionStrip.Add(_tensionNote);
            _top.Add(_tensionStrip);

            _waveStrip = Container("strip");
            _waveStrip.name = "wave-strip";
            _waveLabel = Text(string.Empty, "wave-label");
            _waveLabel.name = "wave-label";
            _waveStrip.Add(_waveLabel);
            (_hearthFill, _hearthValue) = Meter(_waveStrip, "Hearth", "hearth-fill");
            (_prophetFill, _prophetValue) = Meter(_waveStrip, "Prophet", "prophet-fill");
            _top.Add(_waveStrip);

            // ---------- middle: side columns + centre stack ----------
            var middle = Container("middle");
            _sideLeft = Container("side");
            _sideLeft.name = "side-left";
            _sideRight = Container("side");
            _sideRight.name = "side-right";
            _sideLeft.Add(Shortcut(LockedFeatures.Get("map")));
            _sideLeft.Add(Shortcut(LockedFeatures.Get("research")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("missionaries")));
            _sideRight.Add(Shortcut(LockedFeatures.Get("tithes")));
            var centre = Container("centre");
            _toast = Text(string.Empty, "toast", "hidden");
            _toast.name = "toast";
            _toast.pickingMode = PickingMode.Ignore;
            _lockedNote = Text(string.Empty, "locked-note", "hidden");
            _lockedNote.name = "locked-note";
            _settings = BuildSettings(out _probe);
            _buildingCard = BuildBuildingCard(out _buildingTitle, out _buildingOutput, out _buildingReady, out _buildingRemaining, out _upgrade, out _finishNow, out _demolish);
            _card = BuildCard(out _cardTitle, out _cardSummary, out _cardFlavour, out _cardStats, out _stamp);
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
            foreach (var part in new VisualElement[] { _errandHeading, _errandText, _errandReward, _errandClaim })
            {
                part.pickingMode = PickingMode.Ignore;
                _errand.Add(part);
            }

            Show(_errand, false);
            centre.Add(_errand);
            centre.Add(_toast);
            centre.Add(_lockedNote);
            middle.Add(_sideLeft);
            middle.Add(centre);
            middle.Add(_sideRight);

            // ---------- bottom bar ----------
            _bar = Panel("bar");
            _duskBar = Container("dusk-bar");
            _duskBar.name = "dusk-bar";
            _drawer = new ScrollView(ScrollViewMode.Horizontal) { name = "build-drawer" };
            _drawer.AddToClassList("drawer");
            _drawer.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _drawer.verticalScrollerVisibility = ScrollerVisibility.Hidden;
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
            _sluice = SkillButton("sluice", "note-blue", () => SluiceClicked?.Invoke());
            _voice = SkillButton("voice", "note-pink", () => VoiceClicked?.Invoke());
            _stones = SkillButton("stones", "note-yellow", () => StonesClicked?.Invoke());
            _speed = SkillButton("speed", "note-green", () => SpeedClicked?.Invoke());
            _speed.AddToClassList("speed");
            _relicRow = Container("relics");
            _relicRow.name = "relics";
            _laser = SkillButton("laser", "note-pink", () => LaserClicked?.Invoke());
            _torch = SkillButton("torch", "note-yellow", () => TorchClicked?.Invoke());
            _sunFire = SkillButton("magnifier", "note-blue", () => SunFireClicked?.Invoke());
            foreach (var relic in new[] { _laser, _torch, _sunFire })
            {
                relic.AddToClassList("relic");
                _relicRow.Add(relic);
            }

            _nightBar.Add(_relicRow);
            skills.Add(_sluice);
            skills.Add(_voice);
            skills.Add(_stones);
            skills.Add(_speed);
            _nightStatus = Text(string.Empty, "hint");
            _nightBar.Add(skills);
            _nightBar.Add(_nightStatus);
            _bar.Add(_duskBar);
            _bar.Add(_nightBar);

            _root.Add(_top);
            _root.Add(middle);
            _root.Add(_bar);
            _root.Add(_card);      // overlays, added last so they draw over the bar
            _root.Add(_settings);
            _root.Add(_buildingCard);
            Show(_buildingCard, false);
            // The tutorial coach (GDD 05): a hi-vis arrow at the next tap and the Prophet's line beside it.
            _coachArrow = new VisualElement { name = "coach-arrow", pickingMode = PickingMode.Ignore };
            _coachArrow.AddToClassList("coach-arrow");
            _coachArrow.generateVisualContent += DrawCoachArrow;
            _coachLine = Text(string.Empty, "coach-line", "note-yellow");
            _coachLine.name = "coach-line";
            _coachLine.pickingMode = PickingMode.Ignore;
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
        public event Action RepairClicked;
        public event Action BuildingCardClosed;
        public event Action StartNightClicked;
        public event Action SluiceClicked;
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
        public event Action ResetTipsClicked;
        public event Action ReplayIntroClicked;
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
            Show(_tensionStrip, phase == GamePhase.Dusk);
            Show(_waveStrip, phase == GamePhase.Night);
            Show(_sideLeft, phase == GamePhase.Dusk);
            Show(_sideRight, phase == GamePhase.Dusk);
            Show(_duskBar, phase == GamePhase.Dusk);
            Show(_nightBar, phase == GamePhase.Night);
            Show(_bar, phase != GamePhase.Dawn);
            Show(_card, phase == GamePhase.Dawn);
            Show(_prophetFill.parent.parent, phase == GamePhase.Night);
            if (phase != GamePhase.Dusk)
            {
                Show(_settings, false);
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
            _hearthLine.text = $"Hearth Lv {frame.HearthLevel}";
            // Placeholders stay out of sight until they are real (Comfort, pennies).
            _comfort.text = $"Comfort {frame.Comfort.Value}";
            Show(_comfort, !frame.Comfort.IsDemo);
            Show(_pills["pennies"].Pill, !frame.Pennies.IsDemo);
            _tensionCover.style.width = Length.Percent((1f - Mathf.Clamp01(frame.Tension)) * 100f);
            _tensionNote.text = frame.TensionIsDemo ? $"TENSION — {frame.TensionNote} (demo)" : $"TENSION — {frame.TensionNote}";
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
                var notes = new[] { "note-yellow", "note-pink", "note-green", "note-blue" };
                foreach (var card in cards)
                {
                    var model = card;
                    var button = new Button(() => PickCard(model)) { name = $"card-{card.Type}" };
                    button.AddToClassList("btn");
                    button.AddToClassList("build-card");
                    button.AddToClassList(notes[_cards.Count % notes.Length]);
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
                    _drawer.Add(button);
                }
            }

            foreach (var pair in _cards)
            {
                pair.Value.EnableInClassList("selected", pair.Key == selected);
            }
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
        public void ShowGhostActions(Vector2? at, string problem, bool canRotate)
        {
            Show(_ghostActions, at != null);
            if (at == null)
            {
                return;
            }

            // Beside the ghost; flipped to its left when the row would run off the right edge.
            var width = _ghostActions.resolvedStyle.width > 0f ? _ghostActions.resolvedStyle.width : 180f;
            var panelWidth = _root.resolvedStyle.width > 0f ? _root.resolvedStyle.width : float.MaxValue;
            var left = at.Value.x + 8f + width > panelWidth - CornerMargin ? at.Value.x - 8f - width : at.Value.x + 8f;
            _ghostActions.style.left = Mathf.Max(CornerMargin, left);
            _ghostActions.style.top = at.Value.y - 30f;
            _ghostConfirm.SetEnabled(problem == null);
            Show(_ghostRotate, canRotate);
            _ghostProblem.text = problem ?? string.Empty;
            Show(_ghostProblem, problem != null);
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
            Show(_finishNow, card.IsUpgrading && Debug.isDebugBuild);
            Show(_demolish, card.CanDemolish);
            _demolish.text = card.DemolishLabel;
            Show(_move, card.CanMove);
            Show(_repair, card.IsWrecked);
            _repair.text = card.RepairLabel;
            _repair.SetEnabled(card.CanRepair);
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

        public void UpdateNight(in NightHudState state)
        {
            _waveLabel.text = $"Wave {Mathf.Max(1, state.Wave)}/{state.WaveCount} · {state.Alive} on the road";
            SetMeter(_hearthValue, _hearthFill, state.Hearth, state.HearthMax);
            SetMeter(_prophetValue, _prophetFill, state.Prophet, state.ProphetMax);
            SetSkill(_sluice, state.Sluice, state.SluiceReady);
            SetSkill(_voice, state.Voice, state.VoiceReady);
            SetSkill(_stones, state.Stones, state.StonesReady);
            _stones.EnableInClassList("aiming", state.Aiming);
            SetRelic(_laser, state.Laser);
            SetRelic(_torch, state.Torch);
            SetRelic(_sunFire, state.SunFire);
            Show(_relicRow, state.Laser.Shown || state.Torch.Shown || state.SunFire.Shown);
            SetSkill(_speed, $"{state.Speed}×", true);
            _nightStatus.text = state.Status;
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
        public void ShowErrand(ErrandNote note)
        {
            Show(_errand, note != null);
            if (note == null)
            {
                return;
            }

            _errandHeading.text = note.Progress == null ? note.Heading : $"{note.Heading} · {note.Progress}";
            _errandText.text = note.Text;
            _errandReward.text = note.Reward;
            Show(_errandReward, !string.IsNullOrEmpty(note.Reward));
            Show(_errandClaim, note.Done);
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
                _coachLine.text = step.Line ?? string.Empty;
                if (step.Target == CoachTarget.BuildCard && step.Type != null && _cards.TryGetValue(step.Type.Value, out var card))
                {
                    _drawer.ScrollTo(card);
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
                case CoachTarget.BuildCard when _coach.Type != null && _cards.TryGetValue(_coach.Type.Value, out var card) && Visible(card):
                    var drawer = _drawer.worldBound;
                    var bounds = card.worldBound;
                    return bounds.xMax < drawer.xMin || bounds.xMin > drawer.xMax ? (Rect?)null : bounds;
                case CoachTarget.GhostConfirm when Visible(_ghostConfirm):
                    return _ghostConfirm.worldBound;
                case CoachTarget.ErrandNote when Visible(_errand):
                    return _errand.worldBound;
                case CoachTarget.Building when _coachPoint != null:
                    return new Rect(_coachPoint.Value.x - 1f, _coachPoint.Value.y - 1f, 2f, 2f);
                default:
                    return null;
            }
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
            var target = _coach.HasArrow ? CoachTargetBounds() : null;
            var hasLine = !string.IsNullOrEmpty(_coach.Line) && (target != null || !_coach.HasArrow);
            Show(_coachArrow, target != null);
            Show(_coachLine, hasLine);
            if (!hasLine)
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
                var tipY = below ? r.yMax + 4f + bounce : r.yMin - 4f - bounce;
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
                _coachLine.EnableInClassList("coach-line-above", false);
            }

            var lineW = Mathf.Max(_coachLine.resolvedStyle.width, 60f);
            var lineH = _coachLine.resolvedStyle.height;
            _coachLine.style.left = Mathf.Clamp(lineX - lineW / 2f, 12f, Mathf.Max(12f, width - lineW - 12f));
            _coachLine.style.top = _coachLine.ClassListContains("coach-line-above") ? lineY - (float.IsNaN(lineH) ? 0f : lineH) : lineY;
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
                : "Wulfric's men came, saw nobody in charge, and helped themselves.";
            _cardStats.text = $"Played out by the village · hash {report.StateHashHex}";
            _stamp.EnableInClassList("verified", true);
            _stamp.EnableInClassList("mismatch", false);
        }

        public void ShowDawn(BattleResult result, BattleResult serverReplay, int commands)
        {
            var won = result.Outcome == BattleOutcome.Won;
            var match = serverReplay == result;
            SetMeter(_hearthValue, _hearthFill, result.HearthHealth, result.HearthMaxHealth);
            _waveLabel.text = "Dawn";
            _cardTitle.text = won ? "The Hearth stands" : "The Hearth went cold";
            _cardSummary.text = $"Stars {result.Stars}/3 · Hearth {result.HearthHealth}/{result.HearthMaxHealth}";
            _cardFlavour.text = won
                ? "The attackers left damp, confused and noticeably cleaner."
                : "Wulfric's men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.";
            _cardStats.text = $"{result.EnemiesDefeated} defeated · {result.EnemiesLeaked} reached the Hearth · {commands} commands logged\n" +
                              $"Client hash {result.StateHashHex} · server replay {serverReplay.StateHashHex}";
            _stamp.EnableInClassList("verified", match);
            _stamp.EnableInClassList("mismatch", !match);
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
                    _bubbles[i].style.left = at.Value.x - 70f;
                    _bubbles[i].style.top = at.Value.y - 64f;
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
            _cloud = Text("Cloud save: on this device only", "sheet-line");
            _cloud.name = "cloud-status";
            sheet.Add(_cloud);
            sheet.Add(Text("Sound: there is no sound yet.", "sheet-line"));
            var intro = ButtonWith("Watch the intro again", () =>
            {
                Show(_settings, false);
                ReplayIntroClicked?.Invoke();
            }, "plain");
            intro.name = "replay-intro";
            sheet.Add(intro);
            var tips = ButtonWith("Show tips again", () => ResetTipsClicked?.Invoke(), "plain");
            tips.name = "reset-tips";
            sheet.Add(tips);
            if (Debug.isDebugBuild)
            {
                var raise = ButtonWith("Raise Tension +50 (dev)", () => DevRaiseTensionClicked?.Invoke(), "plain");
                raise.name = "dev-raise-tension";
                var skip = ButtonWith("Skip the raid warning (dev)", () => DevSkipWarningClicked?.Invoke(), "plain");
                skip.name = "dev-skip-warning";
                sheet.Add(raise);
                sheet.Add(skip);
            }
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
            upgrade = ButtonWith(string.Empty, () => UpgradeClicked?.Invoke(), "plain");
            upgrade.name = "upgrade";
            finishNow = ButtonWith("Finish now (dev)", () => FinishNowClicked?.Invoke(), "plain");
            finishNow.name = "finish-now";
            demolish = ButtonWith("Demolish", () => DemolishClicked?.Invoke(), "plain");
            demolish.name = "demolish";
            _move = ButtonWith("Move", () => MoveClicked?.Invoke(), "plain");
            _move.name = "move";
            _repair = ButtonWith(string.Empty, () => RepairClicked?.Invoke(), "plain");
            _repair.name = "repair";
            var close = ButtonWith("Close", () =>
            {
                Show(_buildingCard, false);
                BuildingCardClosed?.Invoke();
            }, "plain");
            close.name = "card-close";
            foreach (var element in new VisualElement[] { title, output, ready, remaining, _repair, upgrade, finishNow, _move, demolish, close })
            {
                sheet.Add(element);
            }

            return sheet;
        }

        private VisualElement BuildCard(out Label title, out Label summary, out Label flavour, out Label stats, out VisualElement stamp)
        {
            var card = Panel("card");
            card.name = "card";
            title = Text(string.Empty, "card-title");
            summary = Text(string.Empty, "card-line");
            flavour = Text(string.Empty, "card-line");
            stats = Text(string.Empty, "card-line", "small");
            stamp = Container("stamp");
            stamp.name = "stamp";
            card.Add(title);
            card.Add(summary);
            card.Add(flavour);
            card.Add(stats);
            card.Add(stamp);
            card.Add(ButtonWith("Back to Dusk", () => BackToDuskClicked?.Invoke(), "primary"));
            return card;
        }

        private VisualElement Shortcut(LockedFeature feature, string extraClass = null)
        {
            var holder = Container("shortcut");
            if (extraClass != null)
            {
                holder.AddToClassList(extraClass);
            }

            var button = IconButton(feature.Icon, () => ShowLockedNote(feature.Id, Time.time), "round", "locked");
            button.name = $"locked-{feature.Id}";
            button.Add(Container("padlock"));
            holder.Add(button);
            holder.Add(Text(feature.Label, "shortcut-label"));
            return holder;
        }

        private Button SkillButton(string icon, string note, Action clicked)
        {
            var button = new Button(clicked) { name = icon };
            button.AddToClassList("btn");
            button.AddToClassList("skill");
            button.AddToClassList(note);
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
                SetSkill(button, state.Text, state.Ready || state.Aiming);
                button.EnableInClassList("aiming", state.Aiming);
            }
        }

        /// <summary>Per frame, so no queries: the label was remembered when the button was built.</summary>
        private void SetSkill(Button button, string text, bool ready)
        {
            _skillText[button].text = text;
            button.SetEnabled(ready);
        }

        private void SetPill(string key, Reading reading)
        {
            var (pill, value) = _pills[key];
            value.text = reading.Cap > 0 ? $"{reading.Value}/{Short(reading.Cap)}" : reading.Value.ToString();
            pill.EnableInClassList("demo", reading.IsDemo);
        }

        private static string Short(int cap) => cap >= 1000 && cap % 1000 == 0 ? $"{cap / 1000}k" : cap.ToString();

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
