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

    /// <summary>The Mission House screen (spec 2026-10-15): the preachers and what the road told them.</summary>
    public sealed class MissionHouseModel
    {
        public string Title = "The Mission House";
        public string Intro;
        public List<PreacherCardModel> Preachers = new List<PreacherCardModel>();
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
        public static MissionHouseModel Build(BaseSnapshot snapshot, BuildingTable table, Func<int, bool, string> placeName, DateTimeOffset now,
            Func<int, bool, double> walkSeconds = null)
        {
            var domain = snapshot.Domain;
            var model = new MissionHouseModel { Unlocked = domain != null && domain.Unlocked };
            if (domain == null || !domain.Unlocked)
            {
                model.Intro = $"The Mission House opens at Hearth {table.Domain.UnlockHearth}: the Prophet is still writing the sermons.";
                return model;
            }

            var inWorld = domain.World != null;
            model.Intro = "Your preachers. Send one to a place on the map: they win its faithful over, learn as they go, and come home with stories. A bigger Hearth seats another.";
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

            foreach (var r in (domain.Reports ?? Array.Empty<RoadReport>()).Reverse())
            {
                model.Reports.Add(Report(r, placeName(r.NodeId, r.InWorld)));
            }

            return model;
        }

        /// <summary>A report in words: who, where, and what the road gave.</summary>
        public static string Report(RoadReport r, string place)
        {
            var who = Preachers.Name(r.PreacherId);
            var gift = r.Gift.IsZero ? string.Empty : $" (+{VillagePresenter.ShortCost(r.Gift)})";
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
    /// The Mission House on screen: a card per preacher (who they are, how far they've come, what they're doing) and
    /// the latest road reports. Raises events; the controller opens the map to send or find them.
    /// </summary>
    public sealed class MissionHouseView
    {
        private readonly VisualElement _root;
        private readonly Label _intro;
        private readonly VisualElement _cards;
        private readonly VisualElement _reports;
        private string _signature;

        public event Action<int> SendClicked;
        public event Action<int> ShowClicked;
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
            var close = UiGlyphs.Close("mission-close", () =>
            {
                Show(false);
                Closed?.Invoke();
            });
            close.AddToClassList("map-close");
            header.Add(title);
            header.Add(close);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sheet-scroll");
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _intro = new Label();
            _intro.AddToClassList("small");
            _intro.AddToClassList("wrap");
            _intro.AddToClassList("house-intro");
            _cards = new VisualElement();
            var reportsTitle = new Label("From the road");
            reportsTitle.AddToClassList("sheet-heading");
            _reports = new VisualElement();
            scroll.Add(_intro);
            scroll.Add(_cards);
            scroll.Add(reportsTitle);
            scroll.Add(_reports);
            _root.Add(header);
            _root.Add(scroll);
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Show(bool open)
        {
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                _signature = null;
            }
        }

        public void Render(MissionHouseModel model)
        {
            var signature = model.Intro + "#" + string.Join("|", model.Preachers.Select(p => p.Name + p.Level + p.Status + p.Progress.ToString("0.00"))) + "#" + string.Join("|", model.Reports);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            _intro.text = model.Intro;
            _cards.Clear();
            foreach (var p in model.Preachers)
            {
                _cards.Add(Card(p));
            }

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
            var quirk = new Label($"{p.Quirk}: {p.QuirkHelp}");
            quirk.AddToClassList("small");
            quirk.AddToClassList("wrap");
            names.Add(name);
            names.Add(quirk);
            top.Add(portrait);
            top.Add(names);
            card.Add(top);

            var level = new Label(p.Level);
            level.AddToClassList("small");
            card.Add(level);
            card.Add(Bar(p.XpFraction, "house-xp-fill"));
            var status = new Label(p.Status);
            status.AddToClassList("sheet-line");
            status.AddToClassList("wrap");
            card.Add(status);
            if (!p.AtHome)
            {
                card.Add(Bar(p.Progress, "map-roster-fill"));
            }

            var id = p.Id;
            var node = p.NodeId;
            var button = p.AtHome
                ? new Button(() => SendClicked?.Invoke(id)) { text = "Send to a place", name = $"send-{id}" }
                : new Button(() => ShowClicked?.Invoke(node)) { text = "Show on the map", name = $"show-{id}" };
            button.AddToClassList("plain");
            card.Add(button);
            return card;
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
