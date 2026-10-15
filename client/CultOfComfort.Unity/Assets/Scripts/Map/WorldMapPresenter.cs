using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>A place on the map screen: where it sits (0–1 across the map), what it is, how much of it is yours.</summary>
    public sealed class MapNodeModel
    {
        public int Id;
        public string Name;
        public string Kind;
        public string Icon;
        public Vector2 At;

        /// <summary>Where it is in the world (hex), for the 3D map.</summary>
        public int Q;
        public int R;
        public int Influence;
        public bool Patron;
        public bool Reachable;
        public bool Home;

        /// <summary>Shared world: the strongest rival's share here and its colour number (-1: none), and whether it's the Patron.</summary>
        public int RivalShare;
        public int RivalColour = -1;
        public bool RivalPatron;

        /// <summary>Shared world: a rival cult's village (a camp on the map), and that cult's id.</summary>
        public bool Camp;
        public string CultId;

        /// <summary>Outside the window (your village, when you've panned away).</summary>
        public bool Hidden;

        /// <summary>A lord's seat (keep or cathedral): its plate stays up when zoomed far out.</summary>
        public bool Seat;
    }

    /// <summary>One "send a missionary" choice on a node's card.</summary>
    public sealed class PreachOption
    {
        public int Hours;
        public string Label;
        public bool Enabled;
    }

    public sealed class TitheOption
    {
        public string Label;
        public ResourceBag Amount;
        public bool Enabled;
    }

    public sealed class MissionaryModel
    {
        public string Text;
        public Vector2 At;

        /// <summary>Where they're going, what they're doing, and how far along that walk (or sermon) they are, 0–1.</summary>
        public int NodeId;
        public MissionaryPhase Phase;
        public float Fraction;
    }

    /// <summary>Everything the map screen draws, built from the Domain view (pure: tested without Unity's UI).</summary>
    public sealed class WorldMapModel
    {
        public string Title = "The Marchlands";

        /// <summary>The shared world: a window you can drag and zoom (the Marchlands fit the screen).</summary>
        public bool Pannable;
        public bool Unlocked;
        public List<MapNodeModel> Nodes = new List<MapNodeModel>();
        public List<(int From, int To)> Roads = new List<(int, int)>();
        public List<MissionaryModel> Missionaries = new List<MissionaryModel>();
        public string MissionaryLine;
        public string OfferingsLine;
        public bool CanCollect;
        public string TensionLine;
        public float TensionFraction;
        public List<TitheOption> Tithes = new List<TitheOption>();
        public string TitheNote;

        /// <summary>Who rules these lands ("Lord Wulfric", "the Bishop of Sumpwell"): the one the tithe goes to.</summary>
        public string Lord = "Lord Wulfric";

        /// <summary>One line under the temper bar: what Tension does to you.</summary>
        public string TensionHelp;

        /// <summary>Which window this is (centre and radius): the view keeps a drag's offset until the window changes.</summary>
        public string ViewKey = string.Empty;

        /// <summary>"How the lands work", for the ? sheet.</summary>
        public List<string> Help = new List<string>();

        /// <summary>The seat of the lord whose temper this is (the 3D map flies his war banner and sends his patrols), or -1.</summary>
        public int SeatId = -1;

        /// <summary>He has had enough: a raid is coming tonight.</summary>
        public bool RaidComing;

        /// <summary>The land the 3D map shows (null: nothing to show yet, e.g. waiting for the server).</summary>
        public WorldLand Land;
    }

    public sealed class NodeCardModel
    {
        public string Title;
        public string Kind;
        public List<string> Lines = new List<string>();
        public List<PreachOption> Options = new List<PreachOption>();
        public string Note;
    }

    public static class WorldMapPresenter
    {
        public const int HomeId = 0;

        public static string Icon(NodeType type) => type switch
        {
            NodeType.Hamlet => "b_cottage",
            NodeType.Village => "b_cultist_hut",
            NodeType.MarketTown => "shop",
            NodeType.Abbey => "research",
            _ => "swords",
        };

        public static string Kind(NodeType type) => type switch
        {
            NodeType.Hamlet => "Hamlet",
            NodeType.Village => "Village",
            NodeType.MarketTown => "Market town",
            NodeType.Abbey => "Abbey",
            _ => "Lord Wulfric's seat",
        };

        /// <summary>Pointy-top hex centres, scaled so the whole domain (and the village at 0, 0) fits 0–1 with a margin.</summary>
        public static Dictionary<int, Vector2> Layout(IReadOnlyList<DomainNode> nodes)
        {
            Vector2 Raw(int q, int r) => new Vector2(Mathf.Sqrt(3f) * (q + r / 2f), 1.5f * r);
            var raw = nodes.ToDictionary(n => n.Id, n => Raw(n.Q, n.R));
            raw[HomeId] = Raw(0, 0);
            var min = new Vector2(raw.Values.Min(v => v.x), raw.Values.Min(v => v.y));
            var max = new Vector2(raw.Values.Max(v => v.x), raw.Values.Max(v => v.y));
            const float margin = 0.1f;
            return raw.ToDictionary(kv => kv.Key, kv => new Vector2(
                margin + (1 - 2 * margin) * Mathf.InverseLerp(min.x, max.x, kv.Value.x),
                margin + (1 - 2 * margin) * Mathf.InverseLerp(min.y, max.y, kv.Value.y)));
        }

        public static WorldMapModel Build(BaseSnapshot snapshot, BuildingTable table, DateTimeOffset now)
        {
            var domain = snapshot.Domain;
            var model = new WorldMapModel { Unlocked = domain != null && domain.Unlocked };
            if (domain == null)
            {
                return model;
            }

            var nodes = domain.Nodes.Select(n => n.Node).ToList();
            var layout = Layout(nodes);
            model.Land = LandFor(domain);
            model.Nodes.Add(new MapNodeModel { Id = HomeId, Name = "Your village", Kind = "Home", Icon = "b_hearth", At = layout[HomeId], Influence = 100, Reachable = true, Home = true });
            foreach (var n in domain.Nodes)
            {
                model.Nodes.Add(new MapNodeModel
                {
                    Id = n.Node.Id, Name = n.Node.Name, Kind = Kind(n.Node.Type), Icon = Icon(n.Node.Type), At = layout[n.Node.Id], Q = n.Node.Q, R = n.Node.R,
                    Influence = n.InfluencePercent, Patron = n.Patron, Reachable = n.Reachable,
                    Seat = n.Node.Type == NodeType.Keep || n.Node.Type == NodeType.Cathedral,
                });
            }

            var places = new List<(int Id, int Q, int R)> { (HomeId, 0, 0) };
            places.AddRange(nodes.Select(n => (n.Id, n.Q, n.R)));
            foreach (var a in places)
            {
                foreach (var b in places.Where(b => b.Id > a.Id && HexDistance(a.Q, a.R, b.Q, b.R) == 1))
                {
                    model.Roads.Add((a.Id, b.Id));
                }
            }

            foreach (var m in domain.Missionaries)
            {
                var node = nodes.First(n => n.Id == m.NodeId);
                var left = VillagePresenter.Duration((long)Math.Max(0, (m.PhaseEndsAtUtc - now).TotalSeconds));
                var walk = node.Distance * table.Domain.MinutesPerHex * 60.0;
                var fraction = walk <= 0 ? 1f : Mathf.Clamp01((float)(1 - (m.PhaseEndsAtUtc - now).TotalSeconds / walk));
                var at = m.Phase switch
                {
                    MissionaryPhase.Travelling => Vector2.Lerp(layout[HomeId], layout[node.Id], fraction),
                    MissionaryPhase.Returning => Vector2.Lerp(layout[node.Id], layout[HomeId], fraction),
                    _ => layout[node.Id],
                };
                var text = m.Phase switch
                {
                    MissionaryPhase.Travelling => $"Walking to {node.Name} · {left}",
                    MissionaryPhase.Preaching => $"Preaching at {node.Name} · {left} left",
                    _ => $"Walking home from {node.Name} · {left}",
                };
                model.Missionaries.Add(new MissionaryModel { Text = text, At = at, NodeId = node.Id, Phase = m.Phase, Fraction = fraction });
            }

            var free = domain.MissionarySlots - domain.Missionaries.Count;
            model.MissionaryLine = domain.Missionaries.Count == 0
                ? $"Missionaries: {free} ready. Tap a place to send one."
                : string.Join("\n", model.Missionaries.Select(m => m.Text)) + (free > 0 ? $"\n{free} more ready." : string.Empty);
            model.OfferingsLine = domain.OfferingsPerHour.IsZero && domain.Offerings.IsZero
                ? "Offerings: none yet. Win the faithful first."
                : $"Offerings: {VillagePresenter.ShortCost(domain.Offerings)} waiting (+{VillagePresenter.ShortCost(domain.OfferingsPerHour)}/h)";
            model.CanCollect = !domain.Offerings.IsZero;

            var tension = snapshot.Tension;
            var max = table.Tension.Max;
            model.SeatId = nodes.FirstOrDefault(n => n.Type == NodeType.Keep)?.Id ?? -1;
            model.RaidComing = snapshot.RaidWarningEndsAtUtc != null;
            model.TensionFraction = Mathf.Clamp01(tension / (float)max);
            model.TensionLine = TemperLine(model.Lord, tension, max, domain.TensionPerHourMilli, snapshot.RaidWarningEndsAtUtc != null);
            model.TensionHelp = TemperHelp(model.Lord, max);
            model.Help = Help(model.Lord, table, world: false);
            model.Tithes = Tithes(snapshot, table);
            model.TitheNote = domain.TitheHalfUntilUtc != null
                ? $"Wulfric was paid recently: tithes work at half strength for {VillagePresenter.Duration((long)(domain.TitheHalfUntilUtc.Value - now).TotalSeconds)}."
                : "A tithe calms Wulfric down. Pay enough during a warning and he calls the raid off.";
            return model;
        }

        private static WorldLand _marchlands;

        /// <summary>The Marchlands' land (offline play): your village in the middle, the domain round it, hills beyond.</summary>
        public static WorldLand LandFor(DomainView domain)
        {
            if (_marchlands == null)
            {
                var nodes = domain.Nodes.Select(n => n.Node).ToList();
                var radius = nodes.Count == 0 ? 4 : nodes.Max(n => HexDistance(0, 0, n.Q, n.R)) + 3;
                _marchlands = new WorldLand(0x4d415243u, radius, nodes.Select(n => (n.Id, n.Type, n.Q, n.R)), (0, 0));
            }

            return _marchlands;
        }

        /// <summary>The temper bar's line: how angry the lord is and how fast it's rising.</summary>
        public static string TemperLine(string lord, int tension, int max, long perHourMilli, bool raidComing) =>
            $"{Capital(lord)}'s temper: {tension}/{max}" + (perHourMilli > 0 ? $" · rising {perHourMilli / 1000f:0.#}/h" : string.Empty)
            + (raidComing ? " · raid coming!" : string.Empty);

        public static string TemperHelp(string lord, int max) =>
            $"Your faith in his lands angers him. At {max} he raids your village. A tithe calms him.";

        /// <summary>What the map is for, in plain words (the ? sheet; shown once, the first time the map opens).</summary>
        public static List<string> Help(string lord, BuildingTable table, bool world) => new List<string>
        {
            "Spread the faith. Tap a place and send a missionary: a villager with provisions walks there, preaches, and wins a share of the place's faith. While away, they don't work at home.",
            $"Hold {table.Domain.PatronInfluence}% of a place and you're its Patron. The places you hold send offerings (Timber, Peat, Scrap, Devotion); they pile up here until you tap Collect.",
            world ? "Other players' cults and wandering cults preach here too. The faithful split between you, and faith fades a little every day unless you keep preaching."
                  : "Faith fades a little every day unless you keep preaching.",
            $"{Capital(lord)} doesn't like it. The more faith you hold, the faster his temper (Tension) rises. When it's full he raids your village at night.",
            "Pay a tithe to calm him: resources for a lower temper. During a raid warning, pay enough and he calls the raid off.",
            "A bigger Hearth sends more missionaries, further.",
        };

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>Small, large, and (during a warning) exactly enough to buy off the raid.</summary>
        public static List<TitheOption> Tithes(BaseSnapshot snapshot, BuildingTable table)
        {
            var domain = snapshot.Domain;
            var halved = domain?.TitheHalfUntilUtc != null;
            int Drop(ResourceBag bag) => DomainTable.TitheValue(bag) / table.Domain.TitheValuePerTension / (halved ? 2 : 1);
            var options = new List<TitheOption>();
            void Add(string name, ResourceBag bag) => options.Add(new TitheOption
            {
                Label = $"{name}: {VillagePresenter.ShortCost(bag)} · temper −{Drop(bag)}", Amount = bag, Enabled = snapshot.Stock.Covers(bag) && snapshot.Tension > 0,
            });
            Add("A modest tithe", new ResourceBag(200, 0, 0, 0));
            Add("A generous tithe", new ResourceBag(400, 0, 100, 50));
            if (snapshot.RaidWarningEndsAtUtc != null)
            {
                // Under Uneasy (40) calls it off: pay the difference in Devotion-and-Timber, rounded up.
                var need = Math.Max(1, snapshot.Tension - (table.Tension.Uneasy - 1)) * table.Domain.TitheValuePerTension * (halved ? 2 : 1);
                var devotion = Math.Min(snapshot.Stock.Devotion, need / 8);
                var bag = new ResourceBag(Math.Max(0, need - devotion * 4), 0, 0, devotion);
                Add("Buy off the raid", bag);
            }

            return options;
        }

        public static NodeCardModel Card(BaseSnapshot snapshot, BuildingTable table, int nodeId)
        {
            var domain = snapshot.Domain;
            var view = domain?.Nodes.FirstOrDefault(n => n.Node.Id == nodeId);
            if (view == null)
            {
                return new NodeCardModel
                {
                    Title = "Your village", Kind = "Home",
                    Lines = { "The Hearth, the cult, and the Prophet's car in the pool." },
                };
            }

            var node = view.Node;
            var card = new NodeCardModel { Title = node.Name, Kind = Kind(node.Type) };
            card.Lines.Add(node.Type == NodeType.Keep
                ? "No offerings: prestige only. Preaching here makes Wulfric furious (+15 Tension)."
                : $"Offerings at full faith: {VillagePresenter.ShortCost(view.OfferingsPerHour)}/h · Squalor {node.Squalor}");
            card.Lines.Add($"Your influence: {view.InfluencePercent}%{(view.Patron ? " · you're the Patron (+25% offerings)" : $" · Patron at {table.Domain.PatronInfluence}%")}");
            card.Lines.Add($"A walk of {VillagePresenter.Duration(view.TravelMinutes * 60L)} each way");
            if (!domain.Unlocked)
            {
                card.Note = $"The Marchlands open at Hearth {table.Domain.UnlockHearth}.";
                return card;
            }

            if (!view.Reachable)
            {
                card.Note = "Too far for now: a bigger Hearth, a longer walk.";
                return card;
            }

            var slotsFree = domain.MissionarySlots - domain.Missionaries.Count > 0;
            var villagerFree = snapshot.Population - domain.Missionaries.Count > 0;
            foreach (var (hours, _) in table.Domain.Sessions)
            {
                var cost = new ResourceBag(table.Domain.ProvisionsPerHour.Timber * hours, table.Domain.ProvisionsPerHour.Peat * hours,
                    table.Domain.ProvisionsPerHour.Scrap * hours, table.Domain.ProvisionsPerHour.Devotion * hours);
                var gain = Math.Min(100 - view.InfluencePercent, (int)(table.Domain.GainMilli(node, hours) / 1000));
                card.Options.Add(new PreachOption
                {
                    Hours = hours, Label = $"Preach {hours}h · +{gain}% · {VillagePresenter.ShortCost(cost)}", Enabled = slotsFree && villagerFree && snapshot.Stock.Covers(cost),
                });
            }

            card.Note = !slotsFree ? "Every missionary is out." : !villagerFree ? "Nobody left to send." : "A missionary is a villager: they don't work while away.";
            return card;
        }

        private static int HexDistance(int q1, int r1, int q2, int r2) => (Math.Abs(q1 - q2) + Math.Abs(r1 - r2) + Math.Abs(q1 + r1 - q2 - r2)) / 2;
    }
}
