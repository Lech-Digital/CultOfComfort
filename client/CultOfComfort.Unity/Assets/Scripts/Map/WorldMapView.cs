using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The screen over the 3D world map (spec 2026-10-15 real map): the lord's temper at the top, missionaries and
    /// offerings at the bottom, and between them nothing but the land (<see cref="WorldStage"/>), with a name plate
    /// floating over every place: its name and a ring of who holds its faith. A card slides up for the tapped place.
    /// Draws <see cref="WorldMapModel"/>; raises events for the controller. Fingers on the land go to the controller,
    /// which moves the camera: the plates never catch them.
    /// </summary>
    public sealed class WorldMapView
    {
        private static readonly Color Faith = new Color(0.91f, 1f, 0.23f);
        private static readonly Color Loyalty = new Color(0.55f, 0.18f, 0.14f);

        private readonly VisualElement _root;
        private readonly VisualElement _labels;
        private readonly Label _tension;
        private readonly VisualElement _help;
        private readonly VisualElement _helpLines;
        private readonly Label _titheTitle;
        private readonly VisualElement _tensionFill;
        private readonly Label _missionaries;
        private readonly Label _offerings;
        private readonly Button _collect;
        private readonly VisualElement _card;
        private readonly Label _cardTitle;
        private readonly Label _cardKind;
        private readonly VisualElement _cardLines;
        private readonly VisualElement _cardOptions;
        private readonly Label _cardNote;
        private readonly VisualElement _tithe;
        private readonly VisualElement _titheOptions;
        private readonly Label _titheNote;
        private readonly Label _title;
        private readonly Label _status;
        private readonly VisualElement _demandRow;
        private readonly Label _demandLine;
        private readonly Button _demandButton;
        private readonly VisualElement _cardDemand;
        private readonly Label _cardDemandLine;
        private readonly Button _cardDemandButton;
        private readonly VisualElement _cardPreachers;
        private int _demandId = -1;
        private readonly VisualElement _cardTithe;
        private readonly Label _cardTemper;
        private readonly VisualElement _cardTemperFill;
        private readonly Label _cardTemperHelp;
        private readonly VisualElement _cardTitheOptions;
        private readonly Label _cardTitheNote;
        private readonly Dictionary<int, VisualElement> _plates = new Dictionary<int, VisualElement>();
        private readonly Dictionary<int, VisualElement> _rings = new Dictionary<int, VisualElement>();
        private WorldMapModel _model;
        private int _selected;

        public event Action Closed;
        public event Action<int> NodeTapped;
        public event Action<int, int> PreachClicked;
        public event Action CollectClicked;
        public event Action<ResourceBag> TitheClicked;

        /// <summary>Zoom: +1 shows more of the world, -1 less.</summary>
        public event Action<int> Zoomed;

        public event Action Recentred;

        /// <summary>The card was closed (the controller lifts the ring off the place).</summary>
        public event Action CardClosed;

        /// <summary>"Pay a tithe": the controller flies to the lord's seat and opens its card.</summary>
        public event Action TitheRequested;

        /// <summary>The missionaries button: the controller opens the Mission House.</summary>
        public event Action MissionariesClicked;

        /// <summary>Deliver what the lord demanded (the demand's id).</summary>
        public event Action<int> DemandDelivered;

        /// <summary>A preacher was picked on a place's card (the controller redraws the card for them).</summary>
        public event Action<int> PreacherPicked;

        /// <summary>The minimap was tapped or dragged: the land point under the finger.</summary>
        public event Action<Vector2> MinimapPicked;

        /// <summary>The big Home button: back to your village.</summary>
        public event Action HomeClicked;

        private readonly VisualElement _minimap;
        private Texture2D _minimapTexture;
        private float _miniBound = 1f;
        private Vector2 _miniFocus;
        private float _miniHalf;
        private Vector2? _miniHome;
        private readonly List<(Vector2 At, Color Colour)> _miniMarks = new List<(Vector2, Color)>();

        public WorldMapView(VisualElement parent)
        {
            _root = new VisualElement { name = "world-map", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("world-map-3d");
            parent.Add(_root);

            _labels = new VisualElement { name = "map-labels", pickingMode = PickingMode.Ignore };
            _labels.AddToClassList("map-layer");
            _root.Add(_labels);

            var top = new VisualElement { name = "map-top" };
            top.AddToClassList("panel");
            top.AddToClassList("map-top");
            var header = new VisualElement();
            header.AddToClassList("map-header");
            _title = new Label("The Marchlands");
            _title.AddToClassList("map-title");
            var zoom = new VisualElement();
            zoom.AddToClassList("map-zoom");
            var home = UiGlyphs.Home("map-home", () => Recentred?.Invoke());
            home.AddToClassList("map-close");
            zoom.Add(home);
            foreach (var (text, name, act) in new (string, string, Action)[]
                     {
                         ("−", "map-zoom-out", () => Zoomed?.Invoke(+1)), ("+", "map-zoom-in", () => Zoomed?.Invoke(-1)),
                     })
            {
                var b = new Button(act) { name = name, text = text };
                b.AddToClassList("map-close");
                zoom.Add(b);
            }

            var help = new Button(ShowHelp) { name = "map-help", text = "?" };
            help.AddToClassList("map-close");
            var close = UiGlyphs.Close("map-close", () => Closed?.Invoke());
            close.AddToClassList("map-close");
            header.Add(_title);
            header.Add(zoom);
            header.Add(help);
            header.Add(close);
            top.Add(header);

            var bar = new VisualElement();
            bar.AddToClassList("map-tension-bar");
            _tensionFill = new VisualElement();
            _tensionFill.AddToClassList("map-tension-fill");
            bar.Add(_tensionFill);
            _tension = new Label { name = "map-tension" };
            _tension.AddToClassList("map-line");
            var titheButton = new Button(() => TitheRequested?.Invoke()) { name = "map-tithe", text = "Pay a tithe" };
            titheButton.AddToClassList("plain");
            titheButton.AddToClassList("map-small-button");
            _tension.style.flexGrow = 1;
            _tension.style.flexShrink = 1;
            var temperRow = new VisualElement();
            temperRow.AddToClassList("map-row");
            temperRow.Add(_tension);
            temperRow.Add(titheButton);
            top.Add(temperRow);
            top.Add(bar);
            _demandRow = new VisualElement { name = "map-demand" };
            _demandRow.AddToClassList("map-row");
            _demandRow.AddToClassList("map-demand");
            _demandLine = new Label { name = "map-demand-line" };
            _demandLine.AddToClassList("map-line");
            _demandLine.AddToClassList("map-demand-line");
            _demandLine.style.flexGrow = 1;
            _demandLine.style.flexShrink = 1;
            _demandButton = new Button(() => DemandDelivered?.Invoke(_demandId)) { name = "map-deliver", text = "Deliver" };
            _demandButton.AddToClassList("plain");
            _demandButton.AddToClassList("map-small-button");
            _demandRow.Add(_demandLine);
            _demandRow.Add(_demandButton);
            _demandRow.style.display = DisplayStyle.None;
            top.Add(_demandRow);
            _root.Add(top);

            _status = new Label { name = "map-status", pickingMode = PickingMode.Ignore };
            _status.AddToClassList("map-status");
            _root.Add(_status);

            // The minimap, top right under the temper: the whole world small, tap or drag it to travel.
            var miniRow = new VisualElement { pickingMode = PickingMode.Ignore };
            miniRow.AddToClassList("map-mini-row");
            _minimap = new VisualElement { name = "map-minimap" };
            _minimap.AddToClassList("map-minimap");
            _minimap.generateVisualContent += DrawMinimap;
            _minimap.RegisterCallback<PointerDownEvent>(e =>
            {
                _minimap.CapturePointer(e.pointerId);
                PickMinimap(e.localPosition);
                e.StopPropagation();
            });
            _minimap.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_minimap.HasPointerCapture(e.pointerId))
                {
                    PickMinimap(e.localPosition);
                }
            });
            _minimap.RegisterCallback<PointerUpEvent>(e => _minimap.ReleasePointer(e.pointerId));
            miniRow.Add(_minimap);
            _root.Add(miniRow);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            _root.Add(spacer);

            var homeRow = new VisualElement { pickingMode = PickingMode.Ignore };
            homeRow.AddToClassList("map-home-row");
            var homeButton = new Button(() => HomeClicked?.Invoke()) { name = "map-go-home" };
            homeButton.AddToClassList("map-go-home");
            var homeIcon = UiGlyphs.Home("map-go-home-icon", () => HomeClicked?.Invoke());
            homeIcon.pickingMode = PickingMode.Ignore;
            homeIcon.AddToClassList("map-go-home-icon");
            var homeText = new Label("Home") { pickingMode = PickingMode.Ignore };
            homeText.AddToClassList("map-go-home-text");
            homeButton.Add(homeIcon);
            homeButton.Add(homeText);
            homeRow.Add(homeButton);
            _root.Add(homeRow);

            var bottom = new VisualElement { name = "map-bottom" };
            bottom.AddToClassList("panel");
            bottom.AddToClassList("map-bottom");
            _missionaries = new Label { name = "map-missionaries", pickingMode = PickingMode.Ignore };
            _missionaries.AddToClassList("map-line");
            var missionariesButton = new Button(() => MissionariesClicked?.Invoke()) { name = "map-missionaries-button" };
            missionariesButton.AddToClassList("plain");
            missionariesButton.AddToClassList("map-chip");
            missionariesButton.Add(_missionaries);
            var offeringsRow = new VisualElement();
            offeringsRow.AddToClassList("map-row");
            _offerings = new Label { name = "map-offerings" };
            _offerings.AddToClassList("map-line");
            _offerings.style.flexGrow = 1;
            _offerings.style.flexShrink = 1;
            _collect = new Button(() => CollectClicked?.Invoke()) { name = "map-collect", text = "Collect" };
            _collect.AddToClassList("plain");
            _collect.AddToClassList("map-small-button");
            offeringsRow.Add(_offerings);
            offeringsRow.Add(_collect);
            bottom.Add(missionariesButton);
            bottom.Add(offeringsRow);
            _root.Add(bottom);

            _card = Sheet("map-card", out _cardTitle, () => CardClosed?.Invoke());
            _cardKind = new Label();
            _cardKind.AddToClassList("small");
            _cardLines = new VisualElement();
            _cardOptions = new VisualElement();
            _cardNote = new Label();
            _cardNote.AddToClassList("small");
            _cardNote.AddToClassList("map-note");
            // The card scrolls inside a sheet that leaves the top of the map in view (the place stays visible).
            var cardBody = new ScrollView(ScrollViewMode.Vertical) { name = "map-card-body" };
            cardBody.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            cardBody.style.flexShrink = 1;
            _card.style.maxHeight = Length.Percent(58);
            _card.Insert(1, cardBody);
            cardBody.Add(_cardKind);
            cardBody.Add(_cardLines);
            cardBody.Add(_cardOptions);
            cardBody.Add(_cardNote);
            _cardTithe = new VisualElement { name = "map-card-tithe" };
            _cardTemper = new Label();
            _cardTemper.AddToClassList("sheet-line");
            _cardTemper.AddToClassList("wrap");
            var cardBar = new VisualElement();
            cardBar.AddToClassList("map-tension-bar");
            _cardTemperFill = new VisualElement();
            _cardTemperFill.AddToClassList("map-tension-fill");
            cardBar.Add(_cardTemperFill);
            _cardTemperHelp = new Label();
            _cardTemperHelp.AddToClassList("small");
            _cardTemperHelp.AddToClassList("map-note");
            var titheHeading = new Label("Pay him a tithe");
            titheHeading.AddToClassList("sheet-heading");
            _cardTitheNote = new Label();
            _cardTitheNote.AddToClassList("small");
            _cardTitheNote.AddToClassList("map-note");
            _cardTitheOptions = new VisualElement();
            _cardTithe.Add(_cardTemper);
            _cardTithe.Add(cardBar);
            _cardTithe.Add(_cardTemperHelp);
            _cardTithe.Add(titheHeading);
            _cardTithe.Add(_cardTitheNote);
            _cardTithe.Add(_cardTitheOptions);
            _card.Q<ScrollView>("map-card-body").Insert(1, _cardTithe);
            _cardDemand = new VisualElement { name = "map-card-demand" };
            _cardDemand.AddToClassList("map-card-demand");
            _cardDemandLine = new Label();
            _cardDemandLine.AddToClassList("sheet-line");
            _cardDemandLine.AddToClassList("wrap");
            _cardDemandButton = new Button(() => DemandDelivered?.Invoke(_demandId)) { name = "card-deliver", text = "Deliver what he asks" };
            _cardDemandButton.AddToClassList("plain");
            _cardDemand.Add(_cardDemandLine);
            _cardDemand.Add(_cardDemandButton);
            _cardTithe.Insert(0, _cardDemand);
            _cardPreachers = new VisualElement { name = "map-card-preachers" };
            _cardPreachers.AddToClassList("map-card-preachers");
            var body = _card.Q<ScrollView>("map-card-body");
            body.Insert(body.IndexOf(_cardOptions), _cardPreachers);

            _tithe = Sheet("map-tithe-sheet", out _titheTitle, null);
            _titheTitle.text = "Pay Lord Wulfric a tithe";
            _titheOptions = new VisualElement();
            _titheNote = new Label();
            _titheNote.AddToClassList("small");
            _titheNote.AddToClassList("map-note");
            _tithe.Insert(1, _titheNote);
            _tithe.Insert(2, _titheOptions);

            _help = Sheet("map-help-sheet", out var helpTitle, null);
            helpTitle.text = "How the lands work";
            _helpLines = new VisualElement();
            _help.Insert(1, _helpLines);

            Show(false);
        }

        public bool IsOpen => _root.resolvedStyle.display == DisplayStyle.Flex;

        /// <summary>True when a screen point (pixels, y up) is on the map's own panels, sheets or buttons, not the land.</summary>
        public bool IsOverPanel(Vector2 screen)
        {
            var panel = _root.panel;
            if (panel == null || !IsOpen)
            {
                return false;
            }

            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
            for (var e = picked; e != null; e = e.parent)
            {
                if (e == _root)
                {
                    return true;
                }
            }

            return false;
        }

        public void Show(bool open)
        {
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                CloseCard();
                ShowTithe(false);
                _help.style.display = DisplayStyle.None;
            }
        }

        /// <summary>The "how the lands work" sheet (shown once by the controller, and by the ? button).</summary>
        public void ShowHelp()
        {
            CloseCard();
            ShowTithe(false);
            _help.style.display = DisplayStyle.Flex;
        }

        public void CloseCard()
        {
            if (_card.style.display != DisplayStyle.None)
            {
                _card.style.display = DisplayStyle.None;
                CardClosed?.Invoke();
            }
        }

        public void ShowTitheSheet() => ShowTithe(true);

        /// <summary>A line over the land while it waits for the server ("Finding your way…"), or null.</summary>
        public void SetStatus(string text)
        {
            _status.text = text ?? string.Empty;
            _status.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement Sheet(string name, out Label title, Action closed)
        {
            var sheet = new VisualElement { name = name };
            sheet.AddToClassList("panel");
            sheet.AddToClassList("map-sheet");
            title = new Label();
            title.AddToClassList("sheet-title");
            sheet.Add(title);
            var close = new Button(() =>
            {
                sheet.style.display = DisplayStyle.None;
                closed?.Invoke();
            }) { text = "Close" };
            close.AddToClassList("plain");
            sheet.Add(close);
            sheet.style.display = DisplayStyle.None;
            _root.Add(sheet);
            return sheet;
        }

        private void ShowTithe(bool open)
        {
            _tithe.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                CloseCard();
                _help.style.display = DisplayStyle.None;
            }
        }

        // ---------------- model → screen ----------------

        public void Render(WorldMapModel model)
        {
            _model = model;
            _title.text = model.Title;
            BuildPlates();

            foreach (var node in model.Nodes)
            {
                var plate = _plates[node.Id];
                plate.EnableInClassList("map-plate-far", !node.Reachable && !node.Home && !node.Camp);
                plate.EnableInClassList("map-plate-patron", node.Patron);
                _rings[node.Id].Q<Label>(className: "map-plate-share").text = node.Home || node.Camp ? string.Empty : $"{node.Influence}%";
                _rings[node.Id].userData = new RingShares(node.Home ? 1f : node.Influence / 100f, node.RivalShare / 100f,
                    node.RivalColour >= 0 ? SharedWorldPresenter.CultColours[node.RivalColour % SharedWorldPresenter.CultColours.Length] : Color.clear);
                _rings[node.Id].MarkDirtyRepaint();
            }

            _tension.text = model.TensionLine;
            _titheTitle.text = $"Pay {model.Lord} a tithe";
            _helpLines.Clear();
            foreach (var line in model.Help)
            {
                var l = new Label("• " + line);
                l.AddToClassList("sheet-line");
                l.AddToClassList("wrap");
                _helpLines.Add(l);
            }

            _tensionFill.style.width = Length.Percent(model.TensionFraction * 100f);
            _missionaries.text = string.IsNullOrEmpty(model.MissionaryChip) ? model.MissionaryLine : model.MissionaryChip;
            _demandRow.style.display = model.DemandLine != null ? DisplayStyle.Flex : DisplayStyle.None;
            _demandLine.text = model.DemandLine ?? string.Empty;
            _demandButton.SetEnabled(model.DemandAffordable);
            _demandId = model.DemandId;
            _offerings.text = model.OfferingsLine;
            _collect.SetEnabled(model.CanCollect);
            _titheOptions.Clear();
            foreach (var option in model.Tithes)
            {
                var amount = option.Amount;
                var b = new Button(() => TitheClicked?.Invoke(amount)) { text = option.Label };
                b.AddToClassList("plain");
                b.AddToClassList("wrap");
                b.SetEnabled(option.Enabled);
                _titheOptions.Add(b);
            }

            _titheNote.text = model.TitheNote;
        }

        /// <summary>
        /// Moves every plate over its place (<paramref name="screenOf"/>: 0–1 across and down the view; null when off screen). Far out
        /// (<paramref name="distance"/>), small places drop their names so the land doesn't drown in labels.
        /// </summary>
        public void PlaceLabels(Func<int, Vector2?> screenOf, float distance)
        {
            var panel = _root.panel;
            if (panel == null || _model == null)
            {
                return;
            }

            ApplySafeArea(panel);
            var far = distance > FarDistance;
            var size = new Vector2(_root.layout.width, _root.layout.height);
            var centre = size / 2f;
            var shown = new List<(MapNodeModel Node, Vector2 At)>();
            foreach (var node in _model.Nodes)
            {
                var at = screenOf(node.Id);
                if (at == null)
                {
                    _plates[node.Id].style.display = DisplayStyle.None;
                    continue;
                }

                shown.Add((node, new Vector2(at.Value.x * size.x, at.Value.y * size.y)));
            }

            // Most important first; a plate that would overlap one already up waits until there's room (zoom in).
            var taken = new List<Rect>();
            foreach (var (node, local) in shown.OrderBy(p => Priority(p.Node)).ThenBy(p => (p.At - centre).sqrMagnitude))
            {
                var plate = _plates[node.Id];
                var width = plate.layout.width > 1f ? plate.layout.width : 34f + node.Name.Length * 6f;
                var height = plate.layout.height > 1f ? plate.layout.height : 26f;
                var rect = new Rect(local.x - width / 2f - 3f, local.y - height - 2f, width + 6f, height + 4f);
                // Far out the map reads like a realm: only seats, camps, your village and your holdings keep a plate.
                if (node.Id != _selected && !node.Home && ((far && Priority(node) > 3) || taken.Any(r => r.Overlaps(rect))))
                {
                    plate.style.display = DisplayStyle.None;
                    continue;
                }

                taken.Add(rect);
                plate.style.display = DisplayStyle.Flex;
                plate.style.left = local.x;
                plate.style.top = local.y;
                plate.EnableInClassList("map-plate-selected", node.Id == _selected);
            }
        }

        private int Priority(MapNodeModel node) =>
            node.Home || node.Id == _selected ? 0 : node.Seat || node.Camp ? 1 : node.Patron ? 2 : node.Influence > 0 ? 3 : 4;

        /// <summary>Past this camera distance the map shows the realm, not the hamlets.</summary>
        public const float FarDistance = 30f;

        /// <summary>The panels keep clear of the camera cutout and the gesture bar (UI Toolkit ignores the safe area).</summary>
        private void ApplySafeArea(IPanel panel)
        {
            var area = Screen.safeArea;
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMin, Screen.height - area.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(area.xMax, Screen.height - area.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            var padding = new Vector4(topLeft.x, topLeft.y + 4f, full.x - bottomRight.x, full.y - bottomRight.y + 4f);
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

        private Vector4 _padding = new Vector4(-1f, -1f, -1f, -1f);

        public void ShowCard(NodeCardModel card)
        {
            ShowTithe(false);
            _help.style.display = DisplayStyle.None;
            _cardTithe.style.display = card.Seat ? DisplayStyle.Flex : DisplayStyle.None;
            _cardPreachers.Clear();
            foreach (var p in card.Preachers)
            {
                var id = p.Id;
                var chip = new Button(() => PreacherPicked?.Invoke(id)) { text = p.Label, name = $"pick-{id}" };
                chip.AddToClassList("map-preacher-chip");
                chip.EnableInClassList("map-preacher-chip-on", p.Selected);
                chip.SetEnabled(p.Enabled);
                _cardPreachers.Add(chip);
            }

            _cardPreachers.style.display = card.Preachers.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (card.Seat)
            {
                _cardDemand.style.display = card.DemandLine != null ? DisplayStyle.Flex : DisplayStyle.None;
                _cardDemandLine.text = card.DemandLine ?? string.Empty;
                _cardDemandButton.SetEnabled(card.DemandAffordable);
                _demandId = card.DemandId >= 0 ? card.DemandId : _demandId;
                _cardTemper.text = card.Temper;
                _cardTemperFill.style.width = Length.Percent(card.TemperFraction * 100f);
                _cardTemperHelp.text = card.TemperHelp ?? string.Empty;
                _cardTitheNote.text = card.TitheNote ?? string.Empty;
                _cardTitheOptions.Clear();
                foreach (var option in card.Tithes)
                {
                    var amount = option.Amount;
                    var b = new Button(() => TitheClicked?.Invoke(amount)) { text = option.Label };
                    b.AddToClassList("plain");
                    b.AddToClassList("wrap");
                    b.SetEnabled(option.Enabled);
                    _cardTitheOptions.Add(b);
                }
            }

            _cardTitle.text = card.Title;
            _cardKind.text = card.Kind;
            _cardLines.Clear();
            foreach (var line in card.Lines)
            {
                var l = new Label(line);
                l.AddToClassList("sheet-line");
                l.AddToClassList("wrap");
                _cardLines.Add(l);
            }

            _cardOptions.Clear();
            foreach (var option in card.Options)
            {
                var hours = option.Hours;
                var b = new Button(() => PreachClicked?.Invoke(_selected, hours)) { text = option.Label, name = $"preach-{hours}" };
                b.AddToClassList("plain");
                b.SetEnabled(option.Enabled);
                _cardOptions.Add(b);
            }

            _cardNote.text = card.Note ?? string.Empty;
            _card.style.display = DisplayStyle.Flex;
        }

        public bool HasPlate(int id) => _plates.ContainsKey(id);

        /// <summary>Gives the minimap its picture of the world (±<paramref name="bound"/> land units round the centre).</summary>
        public void SetMinimap(Texture2D texture, float bound)
        {
            if (texture == _minimapTexture)
            {
                return;
            }

            _minimapTexture = texture;
            _miniBound = bound;
            _minimap.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            _minimap.MarkDirtyRepaint();
        }

        /// <summary>Where the camera looks (a box), your village (blue) and the seats and camps (dots).</summary>
        public void UpdateMinimap(Vector2 focus, float halfWidth, Vector2? home, IEnumerable<(Vector2 At, Color Colour)> marks)
        {
            if (Vector2.Distance(focus, _miniFocus) < 0.05f && Mathf.Abs(halfWidth - _miniHalf) < 0.05f && home == _miniHome)
            {
                return;
            }

            _miniFocus = focus;
            _miniHalf = halfWidth;
            _miniHome = home;
            _miniMarks.Clear();
            _miniMarks.AddRange(marks);
            _minimap.MarkDirtyRepaint();
        }

        private Vector2 MiniToLocal(Vector2 land, float size) => new Vector2((land.x / _miniBound * 0.5f + 0.5f) * size, (0.5f - land.y / _miniBound * 0.5f) * size);

        private void PickMinimap(Vector2 local)
        {
            var size = _minimap.resolvedStyle.width;
            if (size <= 1f)
            {
                return;
            }

            MinimapPicked?.Invoke(new Vector2((local.x / size - 0.5f) * 2f * _miniBound, (0.5f - local.y / size) * 2f * _miniBound));
        }

        private void DrawMinimap(MeshGenerationContext context)
        {
            var size = _minimap.resolvedStyle.width;
            if (size <= 1f || _minimapTexture == null)
            {
                return;
            }

            var p = context.painter2D;
            foreach (var (at, colour) in _miniMarks)
            {
                p.fillColor = colour;
                p.BeginPath();
                p.Arc(MiniToLocal(at, size), 1.7f, 0f, 360f);
                p.Fill();
            }

            if (_miniHome != null)
            {
                p.fillColor = new Color(0.27f, 0.38f, 0.55f);
                p.strokeColor = new Color(0.95f, 0.92f, 0.84f);
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.Arc(MiniToLocal(_miniHome.Value, size), 4.5f, 0f, 360f);
                p.Fill();
                p.Stroke();
            }

            var centre = MiniToLocal(_miniFocus, size);
            var half = Mathf.Max(3f, _miniHalf / _miniBound * 0.5f * size);
            p.strokeColor = new Color(1f, 0.97f, 0.85f);
            p.lineWidth = 1.8f;
            p.BeginPath();
            p.MoveTo(centre + new Vector2(-half, -half * 1.5f));
            p.LineTo(centre + new Vector2(half, -half * 1.5f));
            p.LineTo(centre + new Vector2(half, half * 0.9f));
            p.LineTo(centre + new Vector2(-half, half * 0.9f));
            p.ClosePath();
            p.Stroke();
        }

        /// <summary>A place was tapped on the land (the controller picked it): same as tapping its plate.</summary>
        public void Tap(int id)
        {
            _selected = id;
            NodeTapped?.Invoke(id);
        }

        /// <summary>
        /// Keeps a plate per place in the window: new places get one, places gone lose theirs, the rest are kept (the
        /// window moves with the camera, and rebuilding a hundred plates each time stuttered on the phone).
        /// </summary>
        private void BuildPlates()
        {
            var wanted = new HashSet<int>(_model.Nodes.Select(n => n.Id));
            foreach (var gone in _plates.Keys.Where(k => !wanted.Contains(k)).ToList())
            {
                _plates[gone].RemoveFromHierarchy();
                _plates.Remove(gone);
                _rings.Remove(gone);
            }

            foreach (var node in _model.Nodes)
            {
                var id = node.Id;
                if (_plates.TryGetValue(id, out var existing))
                {
                    // Camps are numbered by their order in the window: the same id can be another camp now.
                    var label = existing.Q<Label>(className: "map-plate-name");
                    if (label.text == node.Name)
                    {
                        continue;
                    }

                    existing.RemoveFromHierarchy();
                    _plates.Remove(id);
                    _rings.Remove(id);
                }

                var plate = new Button(() => Tap(id)) { name = $"node-{id}", pickingMode = PickingMode.Ignore };
                plate.AddToClassList("map-plate");
                plate.EnableInClassList("map-plate-home", node.Home);
                plate.EnableInClassList("map-plate-camp", node.Camp);
                plate.EnableInClassList("map-plate-seat", node.Seat);
                var ring = new VisualElement { pickingMode = PickingMode.Ignore, userData = default(RingShares) };
                ring.AddToClassList("map-plate-ring");
                ring.generateVisualContent += DrawRing;
                var share = new Label { pickingMode = PickingMode.Ignore };
                share.AddToClassList("map-plate-share");
                ring.Add(share);
                var name = new Label(node.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("map-plate-name");
                if (node.Camp && node.RivalColour >= 0)
                {
                    name.style.borderBottomColor = SharedWorldPresenter.CultColours[node.RivalColour % SharedWorldPresenter.CultColours.Length];
                }

                var banner = new VisualElement { pickingMode = PickingMode.Ignore };
                banner.AddToClassList("map-banner");
                if (!node.Home && !node.Camp)
                {
                    plate.Add(ring);
                }

                plate.Add(name);
                plate.Add(banner);
                plate.style.display = DisplayStyle.None;
                _labels.Add(plate);
                _plates[id] = plate;
                _rings[id] = ring;
            }
        }

        /// <summary>What a node's ring shows: your share (yellow), the strongest rival's (their colour), the Authority's (the rest).</summary>
        private readonly struct RingShares
        {
            public RingShares(float mine, float rival, Color rivalColour)
            {
                Mine = mine;
                Rival = rival;
                RivalColour = rivalColour;
            }

            public float Mine { get; }

            public float Rival { get; }

            public Color RivalColour { get; }
        }

        private static void DrawRing(MeshGenerationContext context)
        {
            var ring = context.visualElement;
            var shares = ring.userData is RingShares s ? s : default;
            var share = shares.Mine;
            var r = ring.resolvedStyle.width / 2f;
            var centre = new Vector2(r, r);
            var p = context.painter2D;
            p.lineWidth = 4f;
            p.strokeColor = new Color(Loyalty.r, Loyalty.g, Loyalty.b, 0.7f);
            p.BeginPath();
            p.Arc(centre, r - 2f, 0f, 360f);
            p.Stroke();
            if (share > 0f)
            {
                p.strokeColor = Faith;
                p.BeginPath();
                p.Arc(centre, r - 2f, -90f, -90f + 360f * share);
                p.Stroke();
            }

            if (shares.Rival > 0f && share < 1f)
            {
                var from = -90f + 360f * share;
                p.strokeColor = shares.RivalColour;
                p.BeginPath();
                p.Arc(centre, r - 2f, from, from + 360f * Mathf.Min(shares.Rival, 1f - share));
                p.Stroke();
            }
        }
    }
}
