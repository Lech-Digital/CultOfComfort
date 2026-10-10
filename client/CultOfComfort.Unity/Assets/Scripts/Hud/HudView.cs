using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>What the Night bar shows. Built by the controller each frame from the live battle.</summary>
    public readonly struct NightHudState
    {
        public NightHudState(int night, int hearth, int hearthMax, string sluice, bool sluiceReady, string voice, bool voiceReady, int speed, string status)
        {
            Night = night;
            Hearth = hearth;
            HearthMax = hearthMax;
            Sluice = sluice;
            SluiceReady = sluiceReady;
            Voice = voice;
            VoiceReady = voiceReady;
            Speed = speed;
            Status = status;
        }

        public int Night { get; }
        public int Hearth { get; }
        public int HearthMax { get; }
        public string Sluice { get; }
        public bool SluiceReady { get; }
        public string Voice { get; }
        public bool VoiceReady { get; }
        public int Speed { get; }
        public string Status { get; }
    }

    /// <summary>
    /// The HUD, built in code with UI Toolkit and styled by Resources/UI/Hud.uss. Layout is flexbox: a status panel
    /// on top, a bar at the bottom, and a <c>portrait</c> / <c>landscape</c> class on the root that restacks the bar.
    /// It only displays state and raises events; the <see cref="GameController"/> decides what they mean.
    /// </summary>
    public sealed class HudView
    {
        /// <summary>Extra inset beyond the OS safe area, for rounded display corners (panel points).</summary>
        private const float CornerMargin = 14f;

        private readonly VisualElement _root;
        private readonly VisualElement _top;
        private readonly Label _phaseLine;
        private readonly Label _probeLine;
        private readonly VisualElement _hearth;
        private readonly Label _hearthLabel;
        private readonly VisualElement _hearthFill;
        private readonly Label _toast;
        private readonly VisualElement _card;
        private readonly Label _cardTitle;
        private readonly Label _cardSummary;
        private readonly Label _cardFlavour;
        private readonly Label _cardStats;
        private readonly Label _cardVerdict;
        private readonly VisualElement _bar;
        private readonly VisualElement _duskBar;
        private readonly VisualElement _nightBar;
        private readonly Dictionary<TowerType, Button> _towerButtons = new Dictionary<TowerType, Button>();
        private readonly Button _sluice;
        private readonly Button _voice;
        private readonly Button _speed;
        private readonly Label _nightStatus;
        private float _toastUntil;
        private Vector4 _padding = new Vector4(float.NaN, 0f, 0f, 0f);

        public HudView(VisualElement root, IReadOnlyDictionary<TowerType, (string Name, int Cost)> towers)
        {
            _root = root;
            _root.AddToClassList("hud");
            _root.pickingMode = PickingMode.Ignore;

            // Top: status panel + recenter.
            _top = Container("top-row");
            var status = Panel("status");
            status.Add(Text("Cult of Comfort", "title"));
            _phaseLine = Text(string.Empty, "line");
            _probeLine = Text(string.Empty, "line", "small");
            _hearth = Container("hearth");
            _hearthLabel = Text(string.Empty, "hearth-label");
            var track = Container("track");
            _hearthFill = Container("fill");
            track.Add(_hearthFill);
            _hearth.Add(_hearthLabel);
            _hearth.Add(track);
            status.Add(_phaseLine);
            status.Add(_probeLine);
            status.Add(_hearth);
            _top.Add(status);
            _top.Add(Container("spacer"));
            _top.Add(ButtonWith("Recenter", () => RecenterClicked?.Invoke(), "chip"));

            // Middle: toast and the Dawn card.
            var middle = Container("middle");
            var toastRow = Container("toast-row");
            _toast = Text(string.Empty, "toast", "hidden");
            _toast.pickingMode = PickingMode.Ignore;
            toastRow.Add(_toast);
            _card = Panel("card");
            _cardTitle = Text(string.Empty, "card-title");
            _cardSummary = Text(string.Empty, "line");
            _cardFlavour = Text(string.Empty, "line");
            _cardStats = Text(string.Empty, "line", "small");
            _cardVerdict = Text(string.Empty, "line");
            _card.Add(_cardTitle);
            _card.Add(_cardSummary);
            _card.Add(_cardFlavour);
            _card.Add(_cardStats);
            _card.Add(_cardVerdict);
            _card.Add(ButtonWith("Back to Dusk", () => BackToDuskClicked?.Invoke(), "primary", "first"));
            middle.Add(_card);
            middle.Add(toastRow);

            // Bottom: Dusk build bar or Night skill bar.
            _bar = Panel("bar");
            _duskBar = Container("bar-content");
            var picker = Container("group");
            foreach (var tower in towers)
            {
                var button = new Button(() => TowerSelected?.Invoke(tower.Key)) { name = $"tower-{tower.Key}" };
                button.AddToClassList("btn");
                button.AddToClassList("tower");
                var towerName = Text(tower.Value.Name, "tower-name");
                var towerCost = Text($"{tower.Value.Cost} Scrap", "tower-cost");
                towerName.pickingMode = towerCost.pickingMode = PickingMode.Ignore;
                button.Add(towerName);
                button.Add(towerCost);
                if (_towerButtons.Count == 0)
                {
                    button.AddToClassList("first");
                }

                _towerButtons[tower.Key] = button;
                picker.Add(button);
            }

            _duskBar.Add(picker);
            _duskBar.Add(Text("Tap empty ground to build, tap a tower to demolish. Drag to look around, pinch to zoom.", "hint"));
            var start = ButtonWith("Light the Lamps", () => StartNightClicked?.Invoke(), "primary");
            start.name = "start-night";
            _duskBar.Add(start);

            _nightBar = Container("bar-content");
            var skills = Container("group");
            _sluice = ButtonWith(string.Empty, () => SluiceClicked?.Invoke(), "first");
            _voice = ButtonWith(string.Empty, () => VoiceClicked?.Invoke());
            _speed = ButtonWith(string.Empty, () => SpeedClicked?.Invoke(), "speed");
            _sluice.name = "sluice";
            _voice.name = "voice";
            skills.Add(_sluice);
            skills.Add(_voice);
            skills.Add(_speed);
            _nightStatus = Text(string.Empty, "hint");
            _nightBar.Add(skills);
            _nightBar.Add(_nightStatus);
            _bar.Add(_duskBar);
            _bar.Add(_nightBar);

            _root.Add(_top);
            _root.Add(middle);
            _root.Add(_bar);
            SetPortrait(false);
        }

        public event Action<TowerType> TowerSelected;
        public event Action StartNightClicked;
        public event Action SluiceClicked;
        public event Action VoiceClicked;
        public event Action SpeedClicked;
        public event Action RecenterClicked;
        public event Action BackToDuskClicked;

        public void SetPortrait(bool portrait)
        {
            _root.EnableInClassList("portrait", portrait);
            _root.EnableInClassList("landscape", !portrait);
        }

        public void ShowPhase(GamePhase phase)
        {
            Show(_duskBar, phase == GamePhase.Dusk);
            Show(_nightBar, phase == GamePhase.Night);
            Show(_bar, phase != GamePhase.Dawn);
            Show(_card, phase == GamePhase.Dawn);
            Show(_probeLine, phase == GamePhase.Dusk);
            Show(_hearth, phase != GamePhase.Dusk);
        }

        public void UpdateDusk(int night, int scrap, TowerType? selected, IReadOnlyDictionary<TowerType, int> costs, string probe)
        {
            _phaseLine.text = $"Dusk · Night {night} · Scrap {scrap}";
            _probeLine.text = probe;
            foreach (var pair in _towerButtons)
            {
                pair.Value.EnableInClassList("selected", pair.Key == selected);
                pair.Value.EnableInClassList("unaffordable", scrap < costs[pair.Key]);
            }
        }

        public void UpdateNight(in NightHudState state)
        {
            _phaseLine.text = $"Night {state.Night}";
            SetHearth(state.Hearth, state.HearthMax);
            _sluice.text = state.Sluice;
            _sluice.SetEnabled(state.SluiceReady);
            _voice.text = state.Voice;
            _voice.SetEnabled(state.VoiceReady);
            _speed.text = $"{state.Speed}×";
            _nightStatus.text = state.Status;
        }

        public void ShowDawn(BattleResult result, BattleResult serverReplay, int commands)
        {
            var won = result.Outcome == BattleOutcome.Won;
            var match = serverReplay == result;
            _phaseLine.text = "Dawn";
            SetHearth(result.HearthHealth, result.HearthMaxHealth);
            _cardTitle.text = won ? "The Hearth stands" : "The Hearth went cold";
            _cardSummary.text = $"Stars {result.Stars}/3 · Hearth {result.HearthHealth}/{result.HearthMaxHealth}";
            _cardFlavour.text = won
                ? "The attackers left damp, confused and noticeably cleaner."
                : "Wulfric's men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.";
            _cardStats.text = $"{result.EnemiesDefeated} defeated · {result.EnemiesLeaked} reached the Hearth · {commands} commands logged\n" +
                              $"Client hash {result.StateHashHex} · server replay {serverReplay.StateHashHex}";
            _cardVerdict.text = match ? "Server replay: VERIFIED" : "Server replay: MISMATCH";
            _cardVerdict.EnableInClassList("verified", match);
            _cardVerdict.EnableInClassList("mismatch", !match);
        }

        public void Announce(string text, float now, float seconds = 3.5f)
        {
            _toast.text = text;
            _toastUntil = now + seconds;
        }

        /// <summary>Per frame: hides an expired toast and keeps the root padded to the safe area.</summary>
        public void Tick(float now)
        {
            _toast.EnableInClassList("hidden", now >= _toastUntil || string.IsNullOrEmpty(_toast.text));
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
        /// The part of the screen between the status panel and the bottom bar, in viewport coordinates (0..1,
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

        private void SetHearth(int hearth, int max)
        {
            var fraction = max > 0 ? Mathf.Clamp01(hearth / (float)max) : 0f;
            _hearthLabel.text = $"Hearth {hearth}";
            _hearthFill.style.width = Length.Percent(fraction * 100f);
            _hearthFill.style.backgroundColor = Color.Lerp(Palette.Invalid, Palette.Ember, fraction);
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

        /// <summary>A panel blocks world taps (picking on), unlike plain layout containers.</summary>
        private static VisualElement Panel(string className)
        {
            var element = new VisualElement();
            element.AddToClassList("panel");
            element.AddToClassList(className);
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
