using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The Marchlands on parchment (spec 2026-10-14 world map): hex places joined by roads, a ring of your influence on
    /// each, banners where you're Patron, missionaries walking the roads; a card for the tapped place; offerings and the
    /// tithe at the bottom. Draws <see cref="WorldMapModel"/>; raises events for the controller.
    /// </summary>
    public sealed class WorldMapView
    {
        private const float NodeSize = 52f;
        private static readonly Color Ink = new Color(0.23f, 0.17f, 0.1f);
        private static readonly Color Faith = new Color(0.91f, 1f, 0.23f);
        private static readonly Color Loyalty = new Color(0.55f, 0.18f, 0.14f);

        private readonly VisualElement _root;
        private readonly VisualElement _map;
        private readonly VisualElement _nodes;
        private readonly VisualElement _walkers;
        private readonly Label _tension;
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
        private readonly Dictionary<int, Button> _nodeButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, VisualElement> _rings = new Dictionary<int, VisualElement>();
        private WorldMapModel _model;

        public event Action Closed;
        public event Action<int> NodeTapped;
        public event Action<int, int> PreachClicked;
        public event Action CollectClicked;
        public event Action<ResourceBag> TitheClicked;

        /// <summary>The shared world was dragged by this fraction of the map (x, y in 0–1).</summary>
        public event Action<Vector2> Panned;

        /// <summary>Zoom: +1 shows more of the world, -1 less.</summary>
        public event Action<int> Zoomed;

        public event Action Recentred;

        private readonly Label _title;
        private readonly VisualElement _zoom;
        private Vector2? _dragFrom;
        private Vector2 _dragBy;
        private bool _dragging;
        private bool _swallowClick;

        public WorldMapView(VisualElement parent)
        {
            _root = new VisualElement { name = "world-map" };
            _root.AddToClassList("world-map");
            _root.AddToClassList("panel");
            parent.Add(_root);

            var header = new VisualElement();
            header.AddToClassList("map-header");
            _title = new Label("The Marchlands");
            _title.AddToClassList("map-title");
            var close = new Button(() => Closed?.Invoke()) { name = "map-close", text = "✕" };
            close.AddToClassList("map-close");
            _zoom = new VisualElement();
            _zoom.AddToClassList("map-zoom");
            foreach (var (text, name, act) in new (string, string, Action)[]
                     {
                         ("⌂", "map-home", () => Recentred?.Invoke()), ("−", "map-zoom-out", () => Zoomed?.Invoke(+1)), ("+", "map-zoom-in", () => Zoomed?.Invoke(-1)),
                     })
            {
                var b = new Button(act) { name = name, text = text };
                b.AddToClassList("map-close");
                _zoom.Add(b);
            }

            header.Add(_title);
            header.Add(_zoom);
            header.Add(close);
            _root.Add(header);

            var tensionRow = new VisualElement();
            tensionRow.AddToClassList("map-tension");
            var bar = new VisualElement();
            bar.AddToClassList("map-tension-bar");
            _tensionFill = new VisualElement();
            _tensionFill.AddToClassList("map-tension-fill");
            bar.Add(_tensionFill);
            _tension = new Label { name = "map-tension" };
            _tension.AddToClassList("map-line");
            var titheButton = new Button(() => ShowTithe(true)) { name = "map-tithe", text = "Pay a tithe" };
            titheButton.AddToClassList("plain");
            titheButton.AddToClassList("map-small-button");
            tensionRow.Add(_tension);
            tensionRow.Add(bar);
            tensionRow.Add(titheButton);
            _root.Add(tensionRow);

            _map = new VisualElement { name = "map-area" };
            _map.AddToClassList("map-area");
            _map.generateVisualContent += DrawRoads;
            _nodes = new VisualElement { pickingMode = PickingMode.Ignore };
            _nodes.AddToClassList("map-layer");
            _walkers = new VisualElement { pickingMode = PickingMode.Ignore };
            _walkers.AddToClassList("map-layer");
            _map.Add(_nodes);
            _map.Add(_walkers);
            _map.RegisterCallback<GeometryChangedEvent>(_ => Place());
            _map.RegisterCallback<PointerDownEvent>(OnDragStart, TrickleDown.TrickleDown);
            _map.RegisterCallback<PointerMoveEvent>(OnDragMove, TrickleDown.TrickleDown);
            _map.RegisterCallback<PointerUpEvent>(OnDragEnd, TrickleDown.TrickleDown);
            _root.Add(_map);

            var footer = new VisualElement();
            footer.AddToClassList("map-footer");
            _missionaries = new Label { name = "map-missionaries" };
            _missionaries.AddToClassList("map-line");
            var offeringsRow = new VisualElement();
            offeringsRow.AddToClassList("map-row");
            _offerings = new Label { name = "map-offerings" };
            _offerings.AddToClassList("map-line");
            _offerings.style.flexGrow = 1;
            _collect = new Button(() => CollectClicked?.Invoke()) { name = "map-collect", text = "Collect" };
            _collect.AddToClassList("plain");
            _collect.AddToClassList("map-small-button");
            offeringsRow.Add(_offerings);
            offeringsRow.Add(_collect);
            footer.Add(_missionaries);
            footer.Add(offeringsRow);
            _root.Add(footer);

            _card = Sheet("map-card", out _cardTitle);
            _cardKind = new Label();
            _cardKind.AddToClassList("small");
            _cardLines = new VisualElement();
            _cardOptions = new VisualElement();
            _cardNote = new Label();
            _cardNote.AddToClassList("small");
            _cardNote.AddToClassList("map-note");
            _card.Insert(1, _cardKind);
            _card.Insert(2, _cardLines);
            _card.Insert(3, _cardOptions);
            _card.Insert(4, _cardNote);

            _tithe = Sheet("map-tithe-sheet", out var titheTitle);
            titheTitle.text = "Pay Lord Wulfric a tithe";
            _titheOptions = new VisualElement();
            _titheNote = new Label();
            _titheNote.AddToClassList("small");
            _titheNote.AddToClassList("map-note");
            _tithe.Insert(1, _titheOptions);
            _tithe.Insert(2, _titheNote);

            Show(false);
        }

        public bool IsOpen => _root.resolvedStyle.display == DisplayStyle.Flex;

        public void Show(bool open)
        {
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                CloseCard();
                ShowTithe(false);
            }
        }

        public void CloseCard() => _card.style.display = DisplayStyle.None;

        public void ShowTitheSheet() => ShowTithe(true);

        private VisualElement Sheet(string name, out Label title)
        {
            var sheet = new VisualElement { name = name };
            sheet.AddToClassList("panel");
            sheet.AddToClassList("map-sheet");
            title = new Label();
            title.AddToClassList("sheet-title");
            sheet.Add(title);
            var close = new Button(() => sheet.style.display = DisplayStyle.None) { text = "Close" };
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
            }
        }

        // ---------------- model → screen ----------------

        public void Render(WorldMapModel model)
        {
            var rebuild = _model == null || !_model.Nodes.Select(n => (n.Id, n.At)).SequenceEqual(model.Nodes.Select(n => (n.Id, n.At)));
            _model = model;
            _title.text = model.Title;
            _zoom.style.display = model.Pannable ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_dragging)
            {
                _nodes.style.translate = new Translate(0, 0);
                _walkers.style.translate = new Translate(0, 0);
            }

            if (rebuild)
            {
                BuildNodes();
            }

            foreach (var node in model.Nodes)
            {
                var button = _nodeButtons[node.Id];
                button.EnableInClassList("map-node-far", !node.Reachable);
                button.EnableInClassList("map-node-patron", node.Patron);
                button.EnableInClassList("map-node-camp", node.Camp);
                button.Q<Label>(className: "map-node-share").text = node.Home || node.Camp ? string.Empty : $"{node.Influence}%";
                button.parent.style.display = node.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
                _rings[node.Id].userData = new RingShares(node.Home ? 1f : node.Influence / 100f, node.RivalShare / 100f,
                    node.RivalColour >= 0 ? SharedWorldPresenter.CultColours[node.RivalColour % SharedWorldPresenter.CultColours.Length] : Color.clear);
                _rings[node.Id].MarkDirtyRepaint();
            }

            _tension.text = model.TensionLine;
            _tensionFill.style.width = Length.Percent(model.TensionFraction * 100f);
            _missionaries.text = model.MissionaryLine;
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
            _walkers.Clear();
            foreach (var walker in model.Missionaries)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("map-walker");
                dot.userData = walker.At;
                _walkers.Add(dot);
            }

            Place();
            _map.MarkDirtyRepaint();
        }

        public void ShowCard(NodeCardModel card)
        {
            ShowTithe(false);
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

        private int _selected;

        private void BuildNodes()
        {
            _nodes.Clear();
            _nodeButtons.Clear();
            _rings.Clear();
            foreach (var node in _model.Nodes)
            {
                var id = node.Id;
                var holder = new VisualElement { pickingMode = PickingMode.Ignore };
                holder.AddToClassList("map-node-holder");
                var ring = new VisualElement { pickingMode = PickingMode.Ignore, userData = default(RingShares) };
                ring.AddToClassList("map-ring");
                ring.generateVisualContent += DrawRing;
                var button = new Button(() =>
                {
                    if (_swallowClick)
                    {
                        _swallowClick = false;
                        return; // the finger was dragging the map, not tapping
                    }

                    _selected = id;
                    NodeTapped?.Invoke(id);
                }) { name = $"node-{id}" };
                button.AddToClassList("map-node");
                if (node.Home)
                {
                    button.AddToClassList("map-node-home");
                }

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("icon");
                icon.AddToClassList("icon-" + node.Icon);
                icon.AddToClassList("map-node-icon");
                var share = new Label { pickingMode = PickingMode.Ignore };
                share.AddToClassList("map-node-share");
                var banner = new VisualElement { pickingMode = PickingMode.Ignore };
                banner.AddToClassList("map-banner");
                button.Add(icon);
                button.Add(share);
                button.Add(banner);
                var name = new Label(node.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("map-node-name");
                holder.Add(ring);
                holder.Add(button);
                holder.Add(name);
                holder.userData = node.At;
                if (_model.Pannable)
                {
                    holder.style.scale = new Scale(new Vector2(0.72f, 0.72f)); // the shared world packs more places into the window
                }

                _nodes.Add(holder);
                _nodeButtons[id] = button;
                _rings[id] = ring;
            }
        }

        /// <summary>Puts every node and walker at its 0–1 spot within the map area.</summary>
        private void Place()
        {
            var w = _map.resolvedStyle.width;
            var h = _map.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f)
            {
                return;
            }

            foreach (var holder in _nodes.Children())
            {
                var at = (Vector2)holder.userData;
                holder.style.left = at.x * w - NodeSize;
                holder.style.top = at.y * h - NodeSize / 2f;
            }

            foreach (var dot in _walkers.Children())
            {
                var at = (Vector2)dot.userData;
                dot.style.left = at.x * w - 9f;
                dot.style.top = at.y * h - 30f;
            }
        }

        private void DrawRoads(MeshGenerationContext context)
        {
            if (_model == null)
            {
                return;
            }

            var w = _map.resolvedStyle.width;
            var h = _map.resolvedStyle.height;
            var at = new Dictionary<int, Vector2>();
            foreach (var n in _model.Nodes)
            {
                at[n.Id] = new Vector2(n.At.x * w, n.At.y * h);
            }

            var p = context.painter2D;
            p.strokeColor = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
            p.lineWidth = 3f;
            p.lineCap = LineCap.Round;
            foreach (var (from, to) in _model.Roads)
            {
                // A wobbly track, drawn as a few dashes: medieval things are wonky.
                var a = at[from];
                var b = at[to];
                for (var k = 0; k < 5; k++)
                {
                    p.BeginPath();
                    p.MoveTo(Vector2.Lerp(a, b, k / 5f + 0.03f));
                    p.LineTo(Vector2.Lerp(a, b, (k + 0.7f) / 5f));
                    p.Stroke();
                }
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

        private void OnDragStart(PointerDownEvent e)
        {
            if (_model == null || !_model.Pannable)
            {
                return;
            }

            _dragFrom = e.position;
            _dragBy = Vector2.zero;
            _dragging = false;
            _swallowClick = false;
        }

        private void OnDragMove(PointerMoveEvent e)
        {
            if (_dragFrom == null)
            {
                return;
            }

            _dragBy = (Vector2)e.position - _dragFrom.Value;
            if (!_dragging && _dragBy.magnitude > 14f)
            {
                _dragging = true;
            }

            if (_dragging)
            {
                _nodes.style.translate = new Translate(_dragBy.x, _dragBy.y);
                _walkers.style.translate = new Translate(_dragBy.x, _dragBy.y);
            }
        }

        private void OnDragEnd(PointerUpEvent e)
        {
            if (_dragFrom == null)
            {
                return;
            }

            _dragFrom = null;
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _swallowClick = true; // the button under the finger would take this as a tap
            _map.schedule.Execute(() => _swallowClick = false).ExecuteLater(50);
            var w = _map.resolvedStyle.width;
            var h = _map.resolvedStyle.height;
            if (w > 0f && h > 0f)
            {
                Panned?.Invoke(new Vector2(_dragBy.x / w, _dragBy.y / h));
            }
        }

        private static void DrawRing(MeshGenerationContext context)
        {
            var ring = context.visualElement;
            var shares = ring.userData is RingShares s ? s : default;
            var share = shares.Mine;
            var r = ring.resolvedStyle.width / 2f;
            var centre = new Vector2(r, r);
            var p = context.painter2D;
            p.lineWidth = 5f;
            p.strokeColor = new Color(Loyalty.r, Loyalty.g, Loyalty.b, 0.55f);
            p.BeginPath();
            p.Arc(centre, r - 3f, 0f, 360f);
            p.Stroke();
            if (share > 0f)
            {
                p.strokeColor = Faith;
                p.BeginPath();
                p.Arc(centre, r - 3f, -90f, -90f + 360f * share);
                p.Stroke();
            }

            if (shares.Rival > 0f && share < 1f)
            {
                var from = -90f + 360f * share;
                p.strokeColor = shares.RivalColour;
                p.BeginPath();
                p.Arc(centre, r - 3f, from, from + 360f * Mathf.Min(shares.Rival, 1f - share));
                p.Stroke();
            }
        }

        private static void Show(VisualElement element, bool shown) => element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
