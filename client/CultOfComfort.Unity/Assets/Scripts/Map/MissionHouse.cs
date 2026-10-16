using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>One preacher's card in the Mission House.</summary>
    public sealed class PreacherCardModel
    {
        public int Id;
        public string Name;
        public string Quirk;
        public string QuirkHelp;
        public string Level;
        public float XpFraction;
        public string Status;
        public float Progress;
        public bool AtHome;

        /// <summary>Where they're bound (the map flies there), or -1 at home.</summary>
        public int NodeId = -1;
    }

    /// <summary>A road tale waiting for you to choose how it ends (spec 2026-10-16 mission board).</summary>
    public sealed class TaleCardModel
    {
        public int Id;
        public string Teller;
        public string Text;
        public List<(string Label, bool Enabled)> Choices = new List<(string, bool)>();
    }

    /// <summary>One place on the mission board: what it is, how far, whose faithful, what it gives.</summary>
    public sealed class PlaceRowModel
    {
        public int Id;
        public string Name;
        public string Kind;
        public string Icon;
        public string Walk;
        public int Mine;
        public int Rival;
        public bool Patron;
        public string Gives;
        public bool Reachable;
        public int Distance;
    }

    /// <summary>The Mission House screen (spec 2026-10-16): tales, preachers, the board of places, the road's reports.</summary>
    public sealed class MissionHouseModel
    {
        public string Title = "The Mission House";
        public string Intro;
        public List<TaleCardModel> Tales = new List<TaleCardModel>();
        public List<PreacherCardModel> Preachers = new List<PreacherCardModel>();
        public List<PlaceRowModel> Places = new List<PlaceRowModel>();
        public string BoardNote;
        public List<string> Reports = new List<string>();
        public bool Unlocked;
    }

    /// <summary>Builds the Mission House from the village's Domain view. Pure: tested without Unity's UI.</summary>
    public static class MissionHousePresenter
    {
        public static string QuirkName(PreacherQuirk quirk) => quirk switch
        {
            PreacherQuirk.SilverTongued => "Silver-tongued",
            PreacherQuirk.Thrifty => "Thrifty",
            PreacherQuirk.Tireless => "Tireless",
            _ => "Lucky",
        };

        public static string QuirkHelp(PreacherQuirk quirk) => quirk switch
        {
            PreacherQuirk.SilverTongued => $"wins {Preachers.SilverTonguedPercent}% more faith",
            PreacherQuirk.Thrifty => $"packs {Preachers.ThriftyDiscountPercent}% fewer provisions",
            PreacherQuirk.Tireless => "learns half as fast again",
            _ => "finds twice as much on the road",
        };

        /// <param name="placeName">The name of a place (node id, on the shared world?).</param>
        /// <param name="walkSeconds">How long the walk to a place takes, one way (null: unknown).</param>
        /// <param name="places">The board: the places a preacher could go (nearest first), or null while unknown.</param>
        public static MissionHouseModel Build(BaseSnapshot snapshot, BuildingTable table, Func<int, bool, string> placeName, DateTimeOffset now,
            Func<int, bool, double> walkSeconds = null, List<PlaceRowModel> places = null)
        {
            var domain = snapshot.Domain;
            var model = new MissionHouseModel { Unlocked = domain != null && domain.Unlocked };
            if (domain == null || !domain.Unlocked)
            {
                model.Intro = $"The Mission House opens at Hearth {table.Domain.UnlockHearth}: the Prophet is still writing the sermons.";
                return model;
            }

            var inWorld = domain.World != null;
            model.Intro = "Send a preacher to a place below. They win its people over (and its offerings), learn as they go, and come home with a story.";
            foreach (var t in domain.Tales ?? Array.Empty<TaleView>())
            {
                model.Tales.Add(Tale(t, placeName(t.NodeId, t.InWorld), snapshot.Stock));
            }

            foreach (var p in domain.Preachers ?? Array.Empty<PreacherView>())
            {
                var card = new PreacherCardModel
                {
                    Id = p.Id, Name = p.Name, Quirk = QuirkName(p.Quirk), QuirkHelp = QuirkHelp(p.Quirk),
                    Level = p.XpForNext > 0 ? $"Level {p.Level} · {p.Xp}/{p.XpForNext}" + (p.BonusPercent > 0 ? $" · +{p.BonusPercent}% faith" : string.Empty)
                        : $"Level {p.Level} (the most there is) · +{p.BonusPercent}% faith",
                    XpFraction = p.XpForNext > 0 ? Mathf.Clamp01(p.Xp / (float)p.XpForNext) : 1f,
                    AtHome = p.MissionaryId == null,
                };
                var m = domain.Missionaries.FirstOrDefault(x => x.Id == p.MissionaryId);
                if (m == null)
                {
                    card.Status = "At the Hearth, ready to go.";
                }
                else
                {
                    var name = placeName(m.NodeId, inWorld);
                    var left = Math.Max(0, (m.PhaseEndsAtUtc - now).TotalSeconds);
                    var duration = VillagePresenter.Duration((long)left);
                    card.NodeId = m.NodeId;
                    card.Status = m.Phase switch
                    {
                        MissionaryPhase.Travelling => $"Walking to {name}, arrives in {duration}",
                        MissionaryPhase.Preaching => $"Preaching at {name}, {duration} to go",
                        _ => $"Walking home from {name}, back in {duration}",
                    };
                    var leg = m.Phase == MissionaryPhase.Preaching ? m.Hours * 3600.0 : walkSeconds?.Invoke(m.NodeId, inWorld) ?? 0.0;
                    card.Progress = leg <= 0 ? 0f : Mathf.Clamp01((float)(1 - left / leg));
                }

                model.Preachers.Add(card);
            }

            model.Places = places ?? new List<PlaceRowModel>();
            var free = model.Preachers.Count(p => p.AtHome);
            model.BoardNote = places == null ? "Asking the road where it goes…"
                : free == 0 ? "Everyone's out. Pick a place now and send them when they're back."
                : "Tap a place to see what a sermon there wins.";
            foreach (var r in (domain.Reports ?? Array.Empty<RoadReport>()).Reverse())
            {
                model.Reports.Add(Report(r, placeName(r.NodeId, r.InWorld)));
            }

            return model;
        }

        /// <summary>The board row for a place.</summary>
        public static PlaceRowModel Place(int id, string name, NodeType type, int distance, int mine, int rival, bool patron, ResourceBag offeringsPerHour,
            bool reachable, BuildingTable table) => new PlaceRowModel
        {
            Id = id, Name = name, Kind = WorldMapPresenter.Kind(type), Icon = WorldMapPresenter.Icon(type), Distance = distance,
            Walk = VillagePresenter.Duration(distance * table.Domain.MinutesPerHex * 60L), Mine = mine, Rival = rival, Patron = patron,
            Gives = offeringsPerHour.IsZero ? "No offerings: preaching here only angers him." : $"{VillagePresenter.ShortCost(offeringsPerHour)}/h at full faith",
            Reachable = reachable,
        };

        /// <summary>A tale in words, with its two endings priced.</summary>
        public static TaleCardModel Tale(TaleView t, string place, ResourceBag stock)
        {
            var who = Preachers.Name(t.PreacherId);
            var (text, a, b) = t.Tale switch
            {
                RoadTale.Goat => ($"{who} found a lost goat on the road home from {place}.", "Sell it", "Return it to its owner"),
                RoadTale.Widow => ($"A widow near {place} has a leaking roof, and a lot to say about it.", "Fix the roof", "Sell her a bucket"),
                RoadTale.Ditch => ($"{who} spotted scrap glinting in a ditch outside {place}.", "Haul it home", "Dig deeper"),
                RoadTale.Toll => ($"A large man called Troll wants a toll at the bridge by {place}.", "Pay the toll", "Wade across"),
                RoadTale.Turnips => ($"The good people of {place} threw turnips at {who}.", "Turn the other cheek", "Bring the turnips home"),
                RoadTale.Pedlar => ($"A pedlar on the road from {place} wants to trade.", "Swap sermons for nails", "Convert him"),
                RoadTale.Bog => ($"The road home from {place} is mostly bog today.", "Go round", "Cut peat while you're in it"),
                _ => ($"A roadside shrine near {place}, its candles still lit.", "Say a prayer", "Pocket the candles"),
            };
            var card = new TaleCardModel { Id = t.Id, Teller = who, Text = text };
            var labels = new[] { a, b };
            for (var i = 0; i < t.Options.Count && i < labels.Length; i++)
            {
                card.Choices.Add(($"{labels[i]} · {Outcome(t.Options[i])}", stock.Covers(t.Options[i].Cost)));
            }

            return card;
        }

        /// <summary>"+16 Timber", "−8 Timber, +3 Devotion, +2 XP", "60%: +14 Scrap", "+6 Devotion, temper +2".</summary>
        public static string Outcome(TaleOption o)
        {
            var parts = new List<string>();
            if (!o.Cost.IsZero) parts.Add("−" + VillagePresenter.ShortCost(o.Cost));
            if (!o.Gift.IsZero) parts.Add("+" + VillagePresenter.ShortCost(o.Gift).Replace(" · ", ", +"));
            if (o.Xp > 0) parts.Add($"+{o.Xp} XP");
            if (o.Tension > 0) parts.Add($"temper +{o.Tension}");
            var text = parts.Count == 0 ? "nothing" : string.Join(", ", parts);
            return o.ChancePercent < 100 ? $"{o.ChancePercent}%: {text}" : text;
        }

        /// <summary>A report in words: who, where, and what the road gave.</summary>
        public static string Report(RoadReport r, string place)
        {
            var who = Preachers.Name(r.PreacherId);
            var gift = r.Gift.IsZero ? string.Empty : $" (+{VillagePresenter.ShortCost(r.Gift)})";
            if (r.Tale != null)
            {
                var ending = (r.Tale.Value, r.Choice) switch
                {
                    (RoadTale.Goat, 0) => "sold a lost goat",
                    (RoadTale.Goat, _) => "returned a lost goat. Its owner was moved to tears and donations",
                    (RoadTale.Widow, 0) => "fixed a widow's roof. She sent candles",
                    (RoadTale.Widow, _) => "sold a widow a bucket",
                    (RoadTale.Ditch, 0) => "hauled scrap out of a ditch",
                    (RoadTale.Ditch, 1) => "dug deeper in the ditch, and struck scrap",
                    (RoadTale.Ditch, _) => "dug deeper in the ditch and found only more ditch",
                    (RoadTale.Toll, 0) => "paid the troll's toll. Troll is a believer now",
                    (RoadTale.Toll, _) => "waded across the river and came home peaty",
                    (RoadTale.Turnips, 0) => "turned the other cheek. And then the other one",
                    (RoadTale.Turnips, _) => "brought the turnips home. They burn surprisingly well",
                    (RoadTale.Pedlar, 0) => "swapped sermons for nails",
                    (RoadTale.Pedlar, _) => "converted a pedlar",
                    (RoadTale.Bog, 0) => "went round the bog, the long way",
                    (RoadTale.Bog, _) => "cut peat in the bog",
                    (RoadTale.Shrine, 0) => "said a prayer at a shrine",
                    _ => "pocketed a shrine's candles. Someone noticed",
                };
                return $"{who}, back from {place}: {ending}{gift}.";
            }

            return r.Find switch
            {
                RoadFind.Goat => $"{who} came back from {place} with a lost goat, and sold it{gift}.",
                RoadFind.Widow => $"{who} fixed a widow's roof near {place}. She sent candles{gift}.",
                RoadFind.Ditch => $"{who} fished scrap out of a ditch on the way back from {place}{gift}.",
                RoadFind.Turnips => $"{who} was pelted with turnips at {place}. Character-building.",
                _ => $"{who} came home from {place} footsore and smug.",
            };
        }
    }

    /// <summary>
    /// The Mission House on screen (spec 2026-10-16 mission board): the tales your preachers brought home, a card per
    /// preacher, and a board of the places within reach. Tap a place to see the sermons on offer and send someone,
    /// all without the map. Raises events; the controller sends the commands.
    /// </summary>
    public sealed class MissionHouseView
    {
        private readonly VisualElement _root;
        private readonly ScrollView _scroll;
        private readonly Label _intro;
        private readonly VisualElement _tales;
        private readonly VisualElement _cards;
        private readonly Label _boardNote;
        private readonly VisualElement _board;
        private readonly VisualElement _reports;
        private readonly Label _boardTitle;
        private string _talesSignature;
        private string _cardsSignature;
        private string _boardSignature;
        private string _reportsSignature;
        private int _expanded = -1;
        private int _preacher;
        private MissionHouseModel _model;

        /// <summary>A place's card for a preacher (node, preacher id): the preach options. Set by the controller.</summary>
        public Func<int, int, NodeCardModel> CardFor;

        /// <summary>Send (node, hours, preacher).</summary>
        public event Action<int, int, int> PreachClicked;

        /// <summary>End a tale (tale id, choice).</summary>
        public event Action<int, int> TaleChosen;

        public event Action<int> ShowClicked;
        public event Action MapClicked;
        public event Action Closed;

        public MissionHouseView(VisualElement parent)
        {
            _root = new VisualElement { name = "mission-house" };
            _root.AddToClassList("panel");
            _root.AddToClassList("world-map");
            var header = new VisualElement();
            header.AddToClassList("map-header");
            var title = new Label("The Mission House");
            title.AddToClassList("map-title");
            title.AddToClassList("house-title");
            var close = UiGlyphs.Close("mission-close", () =>
            {
                Show(false);
                Closed?.Invoke();
            });
            close.AddToClassList("map-close");
            header.Add(title);
            header.Add(close);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("sheet-scroll");
            _scroll.style.flexGrow = 1;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _intro = new Label();
            _intro.AddToClassList("small");
            _intro.AddToClassList("wrap");
            _intro.AddToClassList("house-intro");
            _tales = new VisualElement { name = "house-tales" };
            _cards = new VisualElement { name = "house-preachers" };
            _boardTitle = Heading("Where to preach");
            _boardNote = new Label();
            _boardNote.AddToClassList("small");
            _boardNote.AddToClassList("wrap");
            _board = new VisualElement { name = "house-board" };
            var map = new Button(() => MapClicked?.Invoke()) { text = "See it all on the map", name = "house-map" };
            map.AddToClassList("plain");
            map.AddToClassList("house-map-button");
            _reports = new VisualElement();
            _scroll.Add(_intro);
            _scroll.Add(_tales);
            _scroll.Add(Heading("Your preachers"));
            _scroll.Add(_cards);
            _scroll.Add(_boardTitle);
            _scroll.Add(_boardNote);
            _scroll.Add(_board);
            _scroll.Add(map);
            _scroll.Add(Heading("From the road"));
            _scroll.Add(_reports);
            _root.Add(header);
            _root.Add(_scroll);
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Show(bool open)
        {
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                _talesSignature = _cardsSignature = _boardSignature = _reportsSignature = null;
                _expanded = -1;
                _preacher = 0;
            }
        }

        public void Render(MissionHouseModel model)
        {
            _model = model;
            _intro.text = model.Intro;
            _boardNote.text = model.BoardNote;
            Show(_boardTitle, model.Unlocked);

            var tales = string.Join("|", model.Tales.Select(t => t.Id + t.Text + string.Join(",", t.Choices)));
            if (tales != _talesSignature)
            {
                _talesSignature = tales;
                _tales.Clear();
                if (model.Tales.Count > 0)
                {
                    _tales.Add(Heading("Back from the road"));
                }

                foreach (var t in model.Tales)
                {
                    _tales.Add(TaleCard(t));
                }
            }

            var cards = string.Join("|", model.Preachers.Select(p => p.Name + p.Level + p.Status + p.Progress.ToString("0.00")));
            if (cards != _cardsSignature)
            {
                _cardsSignature = cards;
                _cards.Clear();
                foreach (var p in model.Preachers)
                {
                    _cards.Add(Card(p));
                }
            }

            RenderBoard(force: false);

            var reports = string.Join("|", model.Reports);
            if (reports != _reportsSignature)
            {
                _reportsSignature = reports;
                _reports.Clear();
                foreach (var line in model.Reports.DefaultIfEmpty("Nobody has been out yet. The road is quiet."))
                {
                    var l = new Label(line);
                    l.AddToClassList("sheet-line");
                    l.AddToClassList("wrap");
                    l.AddToClassList("house-report");
                    _reports.Add(l);
                }
            }
        }

        /// <summary>The board: a row per place; the open one shows who'd go and the sermons on offer.</summary>
        private void RenderBoard(bool force)
        {
            if (_model == null)
            {
                return;
            }

            var free = _model.Preachers.Where(p => p.AtHome).Select(p => p.Id).ToList();
            if (_preacher != 0 && !free.Contains(_preacher))
            {
                _preacher = 0;
            }

            var card = _expanded >= 0 ? CardFor?.Invoke(_expanded, _preacher) : null;
            var signature = string.Join("|", _model.Places.Select(p => $"{p.Id}{p.Mine}{p.Rival}{p.Reachable}")) + $"#{_expanded}#{_preacher}#"
                            + string.Join(",", free) + "#" + (card == null ? string.Empty : string.Join(",", card.Options.Select(o => o.Label + o.Enabled)) + card.Note);
            if (!force && signature == _boardSignature)
            {
                return;
            }

            _boardSignature = signature;
            _board.Clear();
            foreach (var place in _model.Places)
            {
                _board.Add(PlaceRow(place, place.Id == _expanded ? card : null));
            }
        }

        private VisualElement PlaceRow(PlaceRowModel p, NodeCardModel card)
        {
            var row = new VisualElement { name = $"place-{p.Id}" };
            row.AddToClassList("house-place");
            row.EnableInClassList("house-place-far", !p.Reachable);
            row.EnableInClassList("house-place-open", card != null);
            var top = new VisualElement();
            top.AddToClassList("map-row");
            var icon = new VisualElement();
            icon.AddToClassList("icon");
            icon.AddToClassList("icon-" + p.Icon);
            icon.AddToClassList("house-place-icon");
            var names = new VisualElement();
            names.style.flexGrow = 1;
            names.style.flexShrink = 1;
            var name = new Label(p.Name);
            name.AddToClassList("house-place-name");
            name.AddToClassList("wrap");
            var kind = new Label((p.Reachable ? $"{p.Kind} · {p.Walk} walk" : $"{p.Kind} · too far for now (a bigger Hearth walks further)")
                + (p.Patron ? " · you're Patron" : string.Empty));
            kind.AddToClassList("small");
            kind.AddToClassList("wrap");
            names.Add(name);
            names.Add(kind);
            var share = new Label($"{p.Mine}%");
            share.AddToClassList("house-place-share");
            share.style.flexShrink = 0;
            top.Add(icon);
            top.Add(names);
            top.Add(share);
            row.Add(top);
            row.Add(FaithBar(p.Mine, p.Rival));
            var id = p.Id;
            if (p.Reachable)
            {
                row.AddManipulator(new Clickable(() =>
                {
                    _expanded = _expanded == id ? -1 : id;
                    RenderBoard(force: true);
                }));
            }

            if (card == null)
            {
                return row;
            }

            var gives = new Label(p.Gives);
            gives.AddToClassList("small");
            gives.AddToClassList("wrap");
            row.Add(gives);
            foreach (var line in card.Lines.Skip(1).Take(1))
            {
                var l = new Label(line);
                l.AddToClassList("small");
                l.AddToClassList("wrap");
                row.Add(l);
            }

            if (card.Preachers.Count > 1)
            {
                var chips = new VisualElement();
                chips.AddToClassList("map-card-preachers");
                foreach (var who in card.Preachers)
                {
                    var preacherId = who.Id;
                    var chip = new Button(() =>
                    {
                        _preacher = preacherId;
                        RenderBoard(force: true);
                    }) { text = who.Label };
                    chip.AddToClassList("map-preacher-chip");
                    chip.EnableInClassList("map-preacher-chip-on", who.Selected);
                    chip.SetEnabled(who.Enabled);
                    chips.Add(chip);
                }

                row.Add(chips);
            }

            foreach (var option in card.Options)
            {
                var hours = option.Hours;
                var chosen = card.SelectedPreacher;
                var b = new Button(() => PreachClicked?.Invoke(id, hours, chosen)) { text = option.Label, name = $"preach-{id}-{hours}" };
                b.AddToClassList("plain");
                b.AddToClassList("house-preach");
                b.SetEnabled(option.Enabled);
                row.Add(b);
            }

            if (!string.IsNullOrEmpty(card.Note))
            {
                var note = new Label(card.Note);
                note.AddToClassList("small");
                note.AddToClassList("wrap");
                row.Add(note);
            }

            return row;
        }

        private VisualElement TaleCard(TaleCardModel t)
        {
            var card = new VisualElement { name = $"tale-{t.Id}" };
            card.AddToClassList("house-tale");
            var teller = new Label(t.Teller + " is back");
            teller.AddToClassList("house-tale-teller");
            var text = new Label(t.Text);
            text.AddToClassList("wrap");
            text.AddToClassList("house-tale-text");
            card.Add(teller);
            card.Add(text);
            for (var i = 0; i < t.Choices.Count; i++)
            {
                var choice = i;
                var id = t.Id;
                var b = new Button(() => TaleChosen?.Invoke(id, choice)) { text = t.Choices[i].Label, name = $"tale-{t.Id}-{i}" };
                b.AddToClassList("plain");
                b.AddToClassList("house-tale-choice");
                b.SetEnabled(t.Choices[i].Enabled);
                card.Add(b);
            }

            return card;
        }

        private VisualElement Card(PreacherCardModel p)
        {
            var card = new VisualElement { name = $"preacher-{p.Id}" };
            card.AddToClassList("house-card");
            var top = new VisualElement();
            top.AddToClassList("map-row");
            var portrait = new VisualElement();
            portrait.AddToClassList("house-portrait");
            portrait.AddToClassList(p.AtHome ? "house-portrait-home" : "house-portrait-out");
            var initial = new Label(p.Name.Split(' ').Last().Substring(0, 1));
            initial.AddToClassList("house-initial");
            portrait.Add(initial);
            var names = new VisualElement();
            names.style.flexGrow = 1;
            names.style.flexShrink = 1;
            var name = new Label(p.Name);
            name.AddToClassList("house-name");
            var quirk = new Label($"{p.Quirk}: {p.QuirkHelp} · {p.Level}");
            quirk.AddToClassList("small");
            quirk.AddToClassList("wrap");
            names.Add(name);
            names.Add(quirk);
            names.Add(Bar(p.XpFraction, "house-xp-fill"));
            top.Add(portrait);
            top.Add(names);
            card.Add(top);
            var status = new Label(p.Status);
            status.AddToClassList("sheet-line");
            status.AddToClassList("wrap");
            status.AddToClassList("house-status");
            card.Add(status);
            if (!p.AtHome)
            {
                card.Add(Bar(p.Progress, "map-roster-fill"));
                var node = p.NodeId;
                var show = new Button(() => ShowClicked?.Invoke(node)) { text = "Show on the map", name = $"show-{p.Id}" };
                show.AddToClassList("plain");
                show.AddToClassList("house-small-button");
                card.Add(show);
            }

            return card;
        }

        private static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("sheet-heading");
            return l;
        }

        private static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Your share (yellow) and the strongest rival's (red), out of the place's faithful.</summary>
        private static VisualElement FaithBar(int mine, int rival)
        {
            var track = new VisualElement();
            track.AddToClassList("map-roster-bar");
            track.AddToClassList("house-faith");
            var you = new VisualElement();
            you.AddToClassList("house-faith-mine");
            you.style.width = Length.Percent(Mathf.Clamp(mine, 0, 100));
            var them = new VisualElement();
            them.AddToClassList("house-faith-rival");
            them.style.width = Length.Percent(Mathf.Clamp(rival, 0, 100 - mine));
            track.Add(you);
            track.Add(them);
            return track;
        }

        private static VisualElement Bar(float fraction, string fillClass)
        {
            var track = new VisualElement();
            track.AddToClassList("map-roster-bar");
            var fill = new VisualElement();
            fill.AddToClassList(fillClass);
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            track.Add(fill);
            return track;
        }
    }
}
