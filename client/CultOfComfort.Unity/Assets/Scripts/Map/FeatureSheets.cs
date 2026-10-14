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
    }

    public sealed class SheetModel
    {
        public string Title;
        public List<SheetRow> Rows = new List<SheetRow>();
        public string Footer;
    }

    /// <summary>The Research, Trolley Market and Cult sheets as models (pure: tested without the UI).</summary>
    public static class FeatureSheetPresenter
    {
        public static string EffectText(Tech tech) => tech.Effect switch
        {
            TechEffect.TimberPercent => $"Timber +{tech.Amount}%",
            TechEffect.PeatPercent => $"Peat +{tech.Amount}%",
            TechEffect.MissionaryPercent => $"Missionaries win +{tech.Amount}% influence",
            TechEffect.HealthPercent => $"Buildings +{tech.Amount}% health in a Night",
            TechEffect.BathComfort => $"Bathhouse Comfort +{tech.Amount}",
            TechEffect.TithePercent => $"Tithes +{tech.Amount}% stronger",
            TechEffect.SlowerDecay => $"Influence fades {tech.Amount} point a day slower",
            TechEffect.StoragePercent => $"Storage +{tech.Amount}%",
            _ => $"Comfort +{tech.Amount}",
        };

        public static SheetModel Research(BaseSnapshot snapshot, DateTimeOffset now)
        {
            var model = new SheetModel { Title = "Research: the car's manual", Footer = "One study at a time. The Prophet reads slowly and moves his lips." };
            var era = 0;
            foreach (var view in snapshot.Research ?? Array.Empty<TechView>())
            {
                if (view.Tech.Era != era)
                {
                    era = view.Tech.Era;
                    model.Rows.Add(new SheetRow { Heading = true, Text = era == 1 ? "The Mud Era" : "The Comfort Era (\"Plumbing, Probably\")" });
                }

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
                    Button = view.State == TechState.Available ? "Study" : null,
                    Enabled = view.State == TechState.Available && view.Reason == null,
                    Action = "research:" + view.Tech.Id,
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

        public static SheetModel Cult(BaseSnapshot snapshot)
        {
            var model = new SheetModel { Title = "The Cult of Comfort", Footer = "Five people and a duck, to begin with. Look at it now." };
            var away = snapshot.Domain?.Missionaries.Count ?? 0;
            var workers = snapshot.Buildings.Sum(b => b.Crew);
            model.Rows.Add(new SheetRow { Heading = true, Text = "The faithful" });
            model.Rows.Add(new SheetRow { Text = $"{snapshot.Population} villagers in {snapshot.Beds} beds", Detail = $"{workers} at work · {away} out preaching · {Math.Max(0, snapshot.Population - workers - away)} idle" + (snapshot.Cold ? " · cold: the Peat ran out!" : string.Empty) });
            model.Rows.Add(new SheetRow { Heading = true, Text = $"Comfort {snapshot.Comfort}" });
            foreach (var (label, amount) in snapshot.ComfortParts ?? Array.Empty<(string, int)>())
            {
                model.Rows.Add(new SheetRow { Text = $"{label}  {(amount >= 0 ? "+" : string.Empty)}{amount}" });
            }

            model.Rows.Add(new SheetRow { Detail = $"Devotion × {Mathf.Clamp(1f + snapshot.Comfort / 200f, 0.5f, 3f):0.00} from Comfort" });
            model.Rows.Add(new SheetRow { Heading = true, Text = "Relics from the boot" });
            var found = RelicBook.ForHearth(snapshot.HearthLevel);
            void Relic(string name, string lore, bool ok) => model.Rows.Add(new SheetRow { Text = (ok ? "✓ " : "· ") + name, Detail = ok ? lore : "Still in the car. He'll work it out at a bigger Hearth." });
            Relic("The umbrella", "His staff of office. And an umbrella.", true);
            Relic("The Bluetooth speaker", "The Voice from the Box: the Phantom Voice.", true);
            Relic("The laser pointer", "The Finger of God: blinds one raider.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.LaserPointer));
            Relic("The dynamo torch", "Light without fire: raiders near him are dazzled.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.DynamoTorch));
            Relic("The magnifying glass", "Sun-fire, while the sun is up.", found.HasFlag(CultOfComfort.Simulation.Combat.Relic.Magnifier));
            if (snapshot.Errand != null)
            {
                model.Rows.Add(new SheetRow { Heading = true, Text = $"Errand {snapshot.Errand.Index + 1} of {snapshot.Errand.Count}" });
                model.Rows.Add(new SheetRow { Text = snapshot.Errand.Text });
            }

            return model;
        }
    }

    /// <summary>A scrolling parchment sheet over the village: title, rows (with buttons), close.</summary>
    public sealed class FeatureSheetView
    {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly ScrollView _rows;
        private readonly Label _footer;
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
            var close = new Button(Close) { name = "feature-close", text = "✕" };
            close.AddToClassList("map-close");
            header.Add(_title);
            header.Add(close);
            _rows = new ScrollView(ScrollViewMode.Vertical);
            _rows.style.flexGrow = 1;
            _footer = new Label();
            _footer.AddToClassList("small");
            _footer.AddToClassList("map-note");
            _root.Add(header);
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
            _shown = id;
            _root.style.display = DisplayStyle.Flex;
            _title.text = model.Title;
            _footer.text = model.Footer ?? string.Empty;
            var signature = string.Join("|", model.Rows.Select(r => r.Text + r.Detail + r.Enabled));
            if (signature == _signature)
            {
                return; // unchanged: don't rebuild under the player's thumb
            }

            _signature = signature;
            var scroll = _rows.scrollOffset;
            _rows.Clear();
            foreach (var row in model.Rows)
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
    }
}
