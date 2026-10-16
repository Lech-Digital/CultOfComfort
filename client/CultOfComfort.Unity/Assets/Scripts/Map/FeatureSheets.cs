using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>One line of a feature sheet: what it says, and (optionally) a button that raises <see cref="Action"/>.</summary>
    public sealed class SheetRow
    {
        public string Text;
        public string Detail;
        public string Button;
        public bool Enabled;
        public string Action;
        public bool Heading;

        /// <summary>The tab this row lives on (null: every tab, or a sheet without tabs).</summary>
        public string Tab;
    }

    public sealed class SheetModel
    {
        public string Title;
        public List<SheetRow> Rows = new List<SheetRow>();
        public string Footer;

        /// <summary>Tabs across the top (empty: one plain list). The view remembers which one is open.</summary>
        public List<string> Tabs = new List<string>();
    }

    /// <summary>The Research, Trolley Market and Cult sheets as models (pure: tested without the UI).</summary>
    public static class FeatureSheetPresenter
    {
        public static string EffectText(Tech tech) => tech.Effect switch
        {
            TechEffect.TimberPercent => $"Timber +{tech.Amount}%",
            TechEffect.PeatPercent => $"Peat +{tech.Amount}%",
            TechEffect.ScrapPercent => $"Scrap +{tech.Amount}%",
            TechEffect.DevotionPercent => $"Devotion +{tech.Amount}%",
            TechEffect.BuildTimePercent => $"Building {tech.Amount}% quicker",
            TechEffect.MissionaryPercent => $"Missionaries win +{tech.Amount}% influence",
            TechEffect.HealthPercent => $"Buildings +{tech.Amount}% health in a Night",
            TechEffect.SluiceCharges => $"+{tech.Amount} Open Sluice a Night",
            TechEffect.TowerDamagePercent => $"Towers hit {tech.Amount}% harder",
            TechEffect.ProphetHealthPercent => $"The Prophet +{tech.Amount}% health",
            TechEffect.SalvagePercent => $"+{tech.Amount}% Scrap after a Night",
            TechEffect.BathComfort => $"Bathhouse Comfort +{tech.Amount}",
            TechEffect.TithePercent => $"Tithes +{tech.Amount}% stronger",
            TechEffect.DemandPercent => $"The lord asks {tech.Amount}% less",
            TechEffect.TemperPercent => $"The lord's temper rises {tech.Amount}% slower",
            TechEffect.SlowerDecay => $"Influence fades {tech.Amount} point a day slower",
            TechEffect.StoragePercent => $"Storage +{tech.Amount}%",
            _ => $"Comfort +{tech.Amount}",
        };

        /// <summary>The four rooms of the manual, as tabs (spec 2026-10-16 first day).</summary>
        public static string RoomName(TechCategory category) => category switch
        {
            TechCategory.Workshop => "Workshop",
            TechCategory.Night => "Night",
            TechCategory.Pulpit => "Pulpit",
            _ => "Bath",
        };

        /// <param name="confirmStop">Stop was tapped once: the button asks to be sure.</param>
        public static SheetModel Research(BaseSnapshot snapshot, DateTimeOffset now, bool confirmStop = false)
        {
            var model = new SheetModel { Title = "Research: the car's manual", Footer = "One study at a time. The Prophet reads slowly and moves his lips." };
            var views = snapshot.Research ?? Array.Empty<TechView>();
            foreach (var category in (TechCategory[])Enum.GetValues(typeof(TechCategory)))
            {
                model.Tabs.Add(RoomName(category));
            }

            // What's under way, on every tab: one study at a time, so it matters wherever you look.
            var studying = views.FirstOrDefault(v => v.State == TechState.Studying);
            if (studying != null)
            {
                model.Rows.Add(new SheetRow
                {
                    Heading = true,
                    Text = $"Reading: {studying.Tech.Name} · {VillagePresenter.Duration((long)Math.Max(0, ((studying.EndsAtUtc ?? now) - now).TotalSeconds))} left",
                });
            }

            // In each room: what can be read now first, then what's done, then what's still locked.
            foreach (var view in views.OrderBy(v => v.State switch { TechState.Studying => 0, TechState.Available => 1, TechState.Done => 2, _ => 3 }))
            {
                var state = view.State switch
                {
                    TechState.Done => "✓ Read and understood (mostly).",
                    TechState.Studying => $"Studying · {VillagePresenter.Duration((long)Math.Max(0, ((view.EndsAtUtc ?? now) - now).TotalSeconds))} left",
                    _ => $"{VillagePresenter.ShortCost(view.Tech.Cost)} · {VillagePresenter.Duration(view.Tech.Minutes * 60L)}" + (view.Reason != null && view.State == TechState.Locked ? $" · {view.Reason}" : string.Empty),
                };
                model.Rows.Add(new SheetRow
                {
                    Text = $"{view.Tech.Name}: {EffectText(view.Tech)}",
                    Detail = $"{view.Tech.Joke}\n{state}",
                    Button = view.State == TechState.Available ? "Study" : view.State == TechState.Studying ? (confirmStop ? "Sure? Tap again" : "Stop") : null,
                    Enabled = view.State == TechState.Studying || (view.State == TechState.Available && view.Reason == null),
                    Action = view.State == TechState.Studying ? "research-stop" : "research:" + view.Tech.Id,
                    Tab = RoomName(view.Tech.Category),
                });
            }

            return model;
        }

        /// <summary>The swaps the trolley offers: the useful ones, at the market's bad rate.</summary>
        public static readonly (ResourceType Give, ResourceType Get, int Amount)[] Swaps =
        {
            (ResourceType.Timber, ResourceType.Peat, 20), (ResourceType.Timber, ResourceType.Scrap, 20), (ResourceType.Timber, ResourceType.Devotion, 10),
            (ResourceType.Peat, ResourceType.Timber, 50), (ResourceType.Scrap, ResourceType.Timber, 50), (ResourceType.Scrap, ResourceType.Peat, 20),
            (ResourceType.Peat, ResourceType.Scrap, 20), (ResourceType.Devotion, ResourceType.Scrap, 40),
        };

        public static SheetModel Market(BaseSnapshot snapshot, BuildingTable table)
        {
            var model = new SheetModel
            {
                Title = "The Holy Trolley",
                Footer = "The trolley trades at a terrible rate. That's how you know it's honest.",
            };
            foreach (var (give, get, amount) in Swaps)
            {
                var price = table.TradePrice(give, get, amount);
                var room = snapshot.Caps.Get(get) - snapshot.Stock.Get(get);
                model.Rows.Add(new SheetRow
                {
                    Text = $"{amount} {get} for {price} {give}",
                    Button = "Swap",
                    Enabled = snapshot.Stock.Get(give) >= price && room >= amount,
                    Action = $"trade:{give}:{get}:{amount}",
                });
            }

            return model;
        }

        public const string HearthTab = "Hearth";
        public const string PeopleTab = "People";
        public const string ComfortTab = "Comfort";
        public const string RelicsTab = "Relics";
        public const string ErrandTab = "Errand";

        public static SheetModel Cult(BaseSnapshot snapshot, BuildingTable table = null)
        {
            table = table ?? BuildingTable.Default;
            var model = new SheetModel
            {
                Title = "The Cult of Comfort", Footer = "Five people and a duck, to begin with. Look at it now.",
                Tabs = { HearthTab, PeopleTab, ComfortTab, RelicsTab, ErrandTab },
            };
            void Add(string tab, SheetRow row)
            {
                row.Tab = tab;
                model.Rows.Add(row);
            }

            // The Hearth: what it is, what the next level costs and brings.
            var hearth = snapshot.Buildings.First(b => b.Type == BuildingType.Hearth);
            var card = VillagePresenter.Card(snapshot, table, hearth.Id);
            var spec = table.Spec(BuildingType.Hearth);
            Add(HearthTab, new SheetRow { Heading = true, Text = $"The Hearth · level {snapshot.HearthLevel} of {spec.MaxLevel}" });
            Add(HearthTab, new SheetRow
            {
                Text = card.IsUpgrading ? $"Growing · {card.Remaining}" : card.UpgradeLabel,
                Detail = snapshot.HearthLevel >= spec.MaxLevel ? "As big as a fire gets in this century." : "You can also tap the Hearth itself.",
                Button = snapshot.HearthLevel < spec.MaxLevel && !card.IsUpgrading ? "Raise" : null,
                Enabled = card.CanUpgrade,
                Action = "hearth:upgrade",
            });
            if (snapshot.HearthLevel < spec.MaxLevel)
            {
                var next = snapshot.HearthLevel + 1;
                Add(HearthTab, new SheetRow { Heading = true, Text = $"At level {next}" });
                foreach (var line in Unlocks(snapshot.HearthLevel, next, table))
                {
                    Add(HearthTab, new SheetRow { Text = "· " + line });
                }

                if (!card.CanUpgrade && !card.IsUpgrading)
                {
                    Add(HearthTab, new SheetRow
                    {
                        Detail = snapshot.ConstructionEndsAtUtc != null ? "The builder is busy: one job at a time."
                            : $"Needs {VillagePresenter.ShortCost(spec.Levels[snapshot.HearthLevel].Cost)}. You have {VillagePresenter.ShortCost(snapshot.Stock)}.",
                    });
                }
            }

            // The people: who's home, who works, who's idle.
            var away = snapshot.Domain?.Missionaries.Count ?? 0;
            var workers = snapshot.Buildings.Sum(b => b.Crew);
            Add(PeopleTab, new SheetRow { Heading = true, Text = $"{snapshot.Population} villagers in {snapshot.Beds} beds" });
            Add(PeopleTab, new SheetRow { Text = $"{workers} at work · {away} out preaching · {snapshot.IdleVillagers} idle" + (snapshot.Cold ? " · cold: the Peat ran out!" : string.Empty) });
            foreach (var b in snapshot.Buildings.Where(b => b.CrewNeeded > 0 && b.Level > 0).OrderBy(b => b.Id))
            {
                Add(PeopleTab, new SheetRow { Text = $"{table.Spec(b.Type).Name}", Detail = $"Crew {b.Crew}/{b.CrewNeeded} · +{b.PerHour}/h · tap it in the village to change" });
            }

            // Comfort.
            Add(ComfortTab, new SheetRow { Heading = true, Text = $"Comfort {snapshot.Comfort}" });
            foreach (var (label, amount) in snapshot.ComfortParts ?? Array.Empty<(string, int)>())
            {
                Add(ComfortTab, new SheetRow { Text = $"{label}  {(amount >= 0 ? "+" : string.Empty)}{amount}" });
            }

            Add(ComfortTab, new SheetRow { Detail = $"Devotion × {Mathf.Clamp(1f + snapshot.Comfort / 200f, 0.5f, 3f):0.00} from Comfort" });

            // Relics.
            var found = RelicBook.ForHearth(snapshot.HearthLevel);
            void Relic(string name, string lore, bool ok) => Add(RelicsTab, new SheetRow { Text = (ok ? "✓ " : "· ") + name, Detail = ok ? lore : "Still in the car. He'll work it out at a bigger Hearth." });
            Add(RelicsTab, new SheetRow { Heading = true, Text = "Relics from the boot" });
            Relic("The umbrella", "His staff of office. And an umbrella.", true);
            Relic("The Bluetooth speaker", "The Voice from the Box: the Phantom Voice.", true);
            Relic("The laser pointer", "The Finger of God: blinds one raider.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.LaserPointer));
            Relic("The dynamo torch", "Light without fire: raiders near him are dazzled.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.DynamoTorch));
            Relic("The magnifying glass", "Sun-fire, while the sun is up.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.Magnifier));

            // The errand.
            if (snapshot.Errand != null)
            {
                Add(ErrandTab, new SheetRow { Heading = true, Text = $"Errand {snapshot.Errand.Index + 1} of {snapshot.Errand.Count}" });
                Add(ErrandTab, new SheetRow { Text = snapshot.Errand.Text, Detail = snapshot.Errand.Done ? "Done: claim it from the note." : $"{snapshot.Errand.Progress}/{snapshot.Errand.Target}" });
            }
            else
            {
                Add(ErrandTab, new SheetRow { Text = "No errands left. The Prophet is improvising." });
            }

            return model;
        }

        /// <summary>What raising the Hearth from <paramref name="level"/> to <paramref name="next"/> brings: new buildings, more of the old, the map, studies.</summary>
        public static List<string> Unlocks(int level, int next, BuildingTable table)
        {
            var lines = new List<string>();
            var fresh = table.Specs.Values.Where(s => s.UnlockHearth == next && !BuildingTable.IsRoad(s.Type)).Select(s => s.Name).ToList();
            if (fresh.Count > 0)
            {
                lines.Add("New: " + string.Join(", ", fresh));
            }

            var more = table.Specs.Values.Where(s => s.UnlockHearth <= level && s.MaxCount(next) > s.MaxCount(level) && s.MaxCount(next) < 1000)
                .Select(s => $"{s.Name} ×{s.MaxCount(next)}").ToList();
            if (more.Count > 0)
            {
                lines.Add("More room for: " + string.Join(", ", more));
            }

            var roads = table.Specs.Values.Where(s => s.UnlockHearth == next && BuildingTable.IsRoad(s.Type)).Select(s => s.Name).ToList();
            if (roads.Count > 0)
            {
                lines.Add("Better roads: " + string.Join(", ", roads));
            }

            lines.Add($"Every building can go up to level {next}");
            if (next == table.Domain.UnlockHearth)
            {
                lines.Add("The map opens: missionaries, offerings, tithes");
            }
            else if (table.Domain.Missionaries(next) > table.Domain.Missionaries(level))
            {
                lines.Add($"{table.Domain.Missionaries(next)} missionaries at once, walking {table.Domain.Reach(next)} hexes");
            }

            var studies = table.Research.Techs.Count(t => t.UnlockHearth == next);
            if (studies > 0)
            {
                lines.Add($"{studies} new studies in the car's manual");
            }

            lines.Add($"The Hearth's own store holds {table.HearthStorageCaps[Math.Min(next, table.HearthStorageCaps.Count) - 1]} of each");
            return lines;
        }
    }

    /// <summary>A scrolling parchment sheet over the village: title, rows (with buttons), close.</summary>
    public sealed class FeatureSheetView
    {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly ScrollView _rows;
        private readonly Label _footer;
        private readonly VisualElement _tabs;
        private readonly Dictionary<string, string> _tabOf = new Dictionary<string, string>(); // the open tab per sheet
        private SheetModel _model;
        private string _shown;
        private string _signature;

        public event Action<string> ActionClicked;

        public FeatureSheetView(VisualElement parent)
        {
            _root = new VisualElement { name = "feature-sheet" };
            _root.AddToClassList("panel");
            _root.AddToClassList("world-map");
            var header = new VisualElement();
            header.AddToClassList("map-header");
            _title = new Label();
            _title.AddToClassList("map-title");
            var close = UiGlyphs.Close("feature-close", Close);
            close.AddToClassList("map-close");
            header.Add(_title);
            header.Add(close);
            _tabs = new VisualElement { name = "sheet-tabs" };
            _tabs.AddToClassList("sheet-tabs");
            _rows = new ScrollView(ScrollViewMode.Vertical);
            _rows.AddToClassList("sheet-scroll");
            _rows.style.flexGrow = 1;
            _rows.verticalScrollerVisibility = ScrollerVisibility.Hidden; // swipe to scroll, as on any phone: no bar
            _rows.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _rows.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            _footer = new Label();
            _footer.AddToClassList("small");
            _footer.AddToClassList("map-note");
            _root.Add(header);
            _root.Add(_tabs);
            _root.Add(_rows);
            _root.Add(_footer);
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);
        }

        /// <summary>Which sheet is open ("research", "shop", "cult"), or null.</summary>
        public string Shown => _shown;

        public void Close()
        {
            _shown = null;
            _signature = null;
            _root.style.display = DisplayStyle.None;
        }

        public void Show(string id, SheetModel model)
        {
            var switched = id != _shown;
            _shown = id;
            _model = model;
            _root.style.display = DisplayStyle.Flex;
            _title.text = model.Title;
            _footer.text = model.Footer ?? string.Empty;
            if (!_tabOf.TryGetValue(id, out var tab) || !model.Tabs.Contains(tab))
            {
                tab = model.Tabs.FirstOrDefault();
                _tabOf[id] = tab;
            }

            DrawTabs(model, tab);
            var rows = model.Rows.Where(r => tab == null || r.Tab == null || r.Tab == tab).ToList();
            var signature = tab + "#" + string.Join("|", rows.Select(r => r.Text + r.Detail + r.Enabled + r.Button));
            if (signature == _signature)
            {
                return; // unchanged: don't rebuild under the player's thumb
            }

            _signature = signature;
            var scroll = switched ? Vector2.zero : _rows.scrollOffset;
            _rows.Clear();
            foreach (var row in rows)
            {
                var line = new VisualElement();
                line.AddToClassList("sheet-entry");
                var texts = new VisualElement();
                texts.style.flexGrow = 1;
                texts.style.flexShrink = 1;
                if (!string.IsNullOrEmpty(row.Text))
                {
                    var text = new Label(row.Text);
                    text.AddToClassList(row.Heading ? "sheet-heading" : "sheet-line");
                    text.AddToClassList("wrap");
                    texts.Add(text);
                }

                if (!string.IsNullOrEmpty(row.Detail))
                {
                    var detail = new Label(row.Detail);
                    detail.AddToClassList("small");
                    detail.AddToClassList("wrap");
                    texts.Add(detail);
                }

                line.Add(texts);
                if (row.Button != null)
                {
                    var action = row.Action;
                    var button = new Button(() => ActionClicked?.Invoke(action)) { text = row.Button, name = action };
                    button.AddToClassList("plain");
                    button.AddToClassList("map-small-button");
                    button.SetEnabled(row.Enabled);
                    line.Add(button);
                }

                _rows.Add(line);
            }

            _rows.schedule.Execute(() => _rows.scrollOffset = scroll);
        }

        /// <summary>Opens a tab of the sheet on screen (the tab bar's buttons call this).</summary>
        public void ShowTab(string tab)
        {
            if (_shown == null || _model == null)
            {
                return;
            }

            _tabOf[_shown] = tab;
            _signature = null;
            _rows.scrollOffset = Vector2.zero;
            Show(_shown, _model);
        }

        private void DrawTabs(SheetModel model, string open)
        {
            _tabs.style.display = model.Tabs.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_tabs.childCount != model.Tabs.Count || !_tabs.Children().Select(c => c.name).SequenceEqual(model.Tabs.Select(t => "tab-" + t)))
            {
                _tabs.Clear();
                foreach (var tab in model.Tabs)
                {
                    var name = tab;
                    var button = new Button(() => ShowTab(name)) { text = tab, name = "tab-" + tab };
                    button.AddToClassList("sheet-tab");
                    _tabs.Add(button);
                }
            }

            foreach (var child in _tabs.Children())
            {
                child.EnableInClassList("sheet-tab-open", child.name == "tab-" + open);
            }
        }
    }
}
