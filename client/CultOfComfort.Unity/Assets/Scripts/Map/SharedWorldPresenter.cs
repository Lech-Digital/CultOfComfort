using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The shared world on the map screen (spec 2026-10-14 shared world): the window the server sent, drawn on the map
    /// generated from the world's seed, with your share, the strongest rival's colour, and rival cults' camps. Builds
    /// the same <see cref="WorldMapModel"/> as the Marchlands, so the view draws both. Pure: tested without Unity's UI.
    /// </summary>
    public static class SharedWorldPresenter
    {
        /// <summary>The window's radius in hexes: 4 by default (places stay tappable on a phone), zoomable 3–7.</summary>
        public const int DefaultRadius = 4;
        public const int MinRadius = 3;
        public const int MaxRadius = 7;

        /// <summary>Twelve cult colours, readable on parchment (index: the cult's colour number).</summary>
        public static readonly Color[] CultColours =
        {
            new Color(0.80f, 0.25f, 0.20f), new Color(0.20f, 0.45f, 0.75f), new Color(0.55f, 0.30f, 0.65f), new Color(0.15f, 0.55f, 0.45f),
            new Color(0.85f, 0.55f, 0.15f), new Color(0.45f, 0.30f, 0.20f), new Color(0.85f, 0.40f, 0.60f), new Color(0.30f, 0.30f, 0.30f),
            new Color(0.10f, 0.60f, 0.75f), new Color(0.60f, 0.55f, 0.15f), new Color(0.40f, 0.20f, 0.45f), new Color(0.65f, 0.15f, 0.30f),
        };

        private static readonly Dictionary<ulong, WorldMap> Maps = new Dictionary<ulong, WorldMap>();

        /// <summary>The world's map, generated once per seed (the same on the phone as on the server).</summary>
        public static WorldMap MapFor(WorldDto world)
        {
            var seed = unchecked((ulong)world.seed);
            if (!Maps.TryGetValue(seed, out var map))
            {
                map = WorldGenerator.Generate(seed);
                Maps[seed] = map;
            }

            return map;
        }

        /// <summary>Hex (q, r) at 0–1 across a window of <paramref name="radius"/> around (cq, cr) (pointy-top, a fixed scale so panning doesn't jump).</summary>
        public static Vector2 At(int q, int r, int cq, int cr, int radius)
        {
            var dq = q - cq;
            var dr = r - cr;
            var x = Mathf.Sqrt(3f) * (dq + dr / 2f);
            var y = 1.5f * dr;
            var span = Mathf.Sqrt(3f) * (radius + 0.6f);
            return new Vector2(0.5f + x / (2f * span), 0.5f + y / (2f * span));
        }

        /// <summary>The hex offset a drag of <paramref name="fraction"/> of the map (x, y in 0–1) moves the window by.</summary>
        public static (int Dq, int Dr) PanBy(Vector2 fraction, int radius)
        {
            var span = Mathf.Sqrt(3f) * (radius + 0.6f);
            var x = -fraction.x * 2f * span;
            var y = -fraction.y * 2f * span;
            var r = y / 1.5f;
            var q = x / Mathf.Sqrt(3f) - r / 2f;
            return (Mathf.RoundToInt(q), Mathf.RoundToInt(r));
        }

        public static WorldMapModel Build(BaseSnapshot snapshot, BuildingTable table, WorldDto world, DateTimeOffset now)
        {
            var domain = snapshot.Domain;
            var model = new WorldMapModel { Unlocked = domain != null && domain.Unlocked, Pannable = true };
            var map = MapFor(world);
            var (cq, cr, radius) = (world.viewQ, world.viewR, world.viewRadius);
            var reach = table.Domain.Reach(snapshot.HearthLevel);
            var holdings = Holdings(world);
            var cults = (world.cults ?? new List<WorldCultDto>()).ToDictionary(c => c.id);

            model.Nodes.Add(new MapNodeModel
            {
                Id = WorldMapPresenter.HomeId, Name = "Your village", Kind = "Home", Icon = "b_hearth", At = At(world.q, world.r, cq, cr, radius),
                Influence = 100, Reachable = true, Home = true, Hidden = Hex.Distance(world.q, world.r, cq, cr) > radius,
            });
            foreach (var node in map.Near(cq, cr, radius).OrderBy(n => n.Id))
            {
                holdings.TryGetValue(node.Id, out var here);
                var mine = here?.FirstOrDefault(h => h.cult == world.cultId);
                var rival = here?.Where(h => h.cult != world.cultId).OrderByDescending(h => h.milli).FirstOrDefault();
                model.Nodes.Add(new MapNodeModel
                {
                    Id = node.Id, Name = node.Name, Kind = WorldMapPresenter.Kind(node.Type), Icon = WorldMapPresenter.Icon(node.Type),
                    At = At(node.Q, node.R, cq, cr, radius), Influence = (int)((mine?.milli ?? 0) / 1000), Patron = mine?.patron == true,
                    Reachable = model.Unlocked && node.DistanceTo(world.q, world.r) <= reach,
                    RivalShare = (int)((rival?.milli ?? 0) / 1000),
                    RivalColour = rival != null && cults.TryGetValue(rival.cult, out var rc) ? rc.colour : -1,
                    RivalPatron = rival?.patron == true,
                });
            }

            var places = map.Near(cq, cr, radius).ToList();
            foreach (var a in places)
            {
                foreach (var b in places.Where(b => b.Id > a.Id && b.DistanceTo(a.Q, a.R) == 1))
                {
                    model.Roads.Add((a.Id, b.Id)); // tracks between neighbouring places
                }
            }

            var camp = 0;
            foreach (var cult in cults.Values.Where(c => c.id != world.cultId).OrderBy(c => c.id, StringComparer.Ordinal))
            {
                model.Nodes.Add(new MapNodeModel
                {
                    Id = -(++camp), Name = cult.name, Kind = "Rival cult", Icon = "cult", At = At(cult.q, cult.r, cq, cr, radius),
                    Influence = 0, Reachable = false, RivalShare = 100, RivalColour = cult.colour, Camp = true, CultId = cult.id,
                });
            }

            foreach (var m in domain?.Missionaries ?? Array.Empty<MissionaryView>())
            {
                var node = map.Node(m.NodeId);
                if (node == null)
                {
                    continue;
                }

                var left = VillagePresenter.Duration((long)Math.Max(0, (m.PhaseEndsAtUtc - now).TotalSeconds));
                var walk = node.DistanceTo(world.q, world.r) * table.Domain.MinutesPerHex * 60.0;
                var fraction = walk <= 0 ? 1f : Mathf.Clamp01((float)(1 - (m.PhaseEndsAtUtc - now).TotalSeconds / walk));
                var home = At(world.q, world.r, cq, cr, radius);
                var there = At(node.Q, node.R, cq, cr, radius);
                var at = m.Phase == MissionaryPhase.Travelling ? Vector2.Lerp(home, there, fraction)
                    : m.Phase == MissionaryPhase.Returning ? Vector2.Lerp(there, home, fraction) : there;
                var text = m.Phase == MissionaryPhase.Travelling ? $"Walking to {node.Name} · {left}"
                    : m.Phase == MissionaryPhase.Preaching ? $"Preaching at {node.Name} · {left} left"
                    : $"Walking home from {node.Name} · {left}";
                model.Missionaries.Add(new MissionaryModel { Text = text, At = at });
            }

            var slots = domain?.MissionarySlots ?? 0;
            var free = slots - (domain?.Missionaries.Count ?? 0);
            model.MissionaryLine = !model.Unlocked ? $"Missionaries go out from Hearth {table.Domain.UnlockHearth}."
                : model.Missionaries.Count == 0 ? $"Missionaries: {free} ready. Tap a place to send one."
                : string.Join("\n", model.Missionaries.Select(m => m.Text)) + (free > 0 ? $"\n{free} more ready." : string.Empty);
            var purse = new ResourceBag(world.purseTimber, world.pursePeat, world.purseScrap, world.purseDevotion);
            var perHour = new ResourceBag(world.perHourTimber, world.perHourPeat, world.perHourScrap, world.perHourDevotion);
            model.OfferingsLine = purse.IsZero && perHour.IsZero
                ? "Offerings: none yet. Win the faithful first."
                : $"Offerings: {VillagePresenter.ShortCost(purse)} waiting (+{VillagePresenter.ShortCost(perHour)}/h)";
            model.CanCollect = !purse.IsZero;

            var tension = snapshot.Tension;
            var max = table.Tension.Max;
            var (title, lord) = Authority(map, world);
            model.Title = title;
            model.Lord = lord;
            model.ViewKey = $"{cq},{cr},{radius}";
            model.TensionFraction = Mathf.Clamp01(tension / (float)max);
            model.TensionLine = WorldMapPresenter.TemperLine(lord, tension, max, domain?.TensionPerHourMilli ?? 0, snapshot.RaidWarningEndsAtUtc != null);
            model.TensionHelp = WorldMapPresenter.TemperHelp(lord, max);
            model.Help = WorldMapPresenter.Help(lord, table, world: true);
            model.Tithes = WorldMapPresenter.Tithes(snapshot, table);
            model.TitheNote = domain?.TitheHalfUntilUtc != null
                ? $"He was paid recently: tithes work at half strength for {VillagePresenter.Duration((long)(domain.TitheHalfUntilUtc.Value - now).TotalSeconds)}."
                : $"Each tithe lowers his temper by the amount shown. Pay enough during a raid warning and he calls the raid off. Right after a tithe, the next one works at half strength for a while.";
            return model;
        }

        /// <summary>The card for a tapped place (or camp, or your village), with who holds what there.</summary>
        public static NodeCardModel Card(BaseSnapshot snapshot, BuildingTable table, WorldDto world, int nodeId)
        {
            var map = MapFor(world);
            var cults = (world.cults ?? new List<WorldCultDto>()).ToDictionary(c => c.id);
            if (nodeId == WorldMapPresenter.HomeId)
            {
                return new NodeCardModel { Title = "Your village", Kind = "Home", Lines = { "The Hearth, the cult, and the Prophet's car in the pool." } };
            }

            if (nodeId < 0)
            {
                var camp = cults.Values.Where(c => c.id != world.cultId).OrderBy(c => c.id, StringComparer.Ordinal).ElementAtOrDefault(-nodeId - 1);
                return new NodeCardModel
                {
                    Title = camp?.name ?? "A rival cult", Kind = "Rival cult",
                    Lines = { "Another cult with its own ideas about comfort. They preach where you preach; the faithful split between you." },
                };
            }

            var node = map.Node(nodeId);
            if (node == null)
            {
                return new NodeCardModel { Title = "Nowhere", Kind = string.Empty };
            }

            Holdings(world).TryGetValue(nodeId, out var here);
            here = here ?? new List<WorldHoldingDto>();
            var mine = here.FirstOrDefault(h => h.cult == world.cultId);
            var myShare = (int)((mine?.milli ?? 0) / 1000);
            var card = new NodeCardModel { Title = node.Name, Kind = WorldMapPresenter.Kind(node.Type) };
            var kind = table.Domain.Kinds[node.Type];
            card.Lines.Add(kind.OfferingsPerHour.IsZero
                ? "No offerings: prestige only. Preaching at a lord's seat makes him furious (+15 Tension)."
                : $"Offerings at full faith: {VillagePresenter.ShortCost(kind.OfferingsPerHour)}/h · Squalor {node.Squalor}");
            var shares = here.OrderByDescending(h => h.milli).Select(h =>
                $"{(h.cult == world.cultId ? "You" : cults.TryGetValue(h.cult, out var c) ? c.name : "A cult")} {h.milli / 1000}%{(h.patron ? " (Patron)" : string.Empty)}").ToList();
            var loyalty = 100 - (int)(here.Sum(h => h.milli) / 1000);
            shares.Add($"the Authority {loyalty}%");
            card.Lines.Add("Faith here: " + string.Join(" · ", shares));
            var distance = node.DistanceTo(world.q, world.r);
            card.Lines.Add($"{distance} hexes away · a walk of {VillagePresenter.Duration(distance * table.Domain.MinutesPerHex * 60L)} each way");
            var domain = snapshot.Domain;
            if (domain == null || !domain.Unlocked)
            {
                card.Note = $"Missionaries go out from Hearth {table.Domain.UnlockHearth}.";
                return card;
            }

            if (distance > table.Domain.Reach(snapshot.HearthLevel))
            {
                card.Note = "Too far for now: a bigger Hearth, a longer walk.";
                return card;
            }

            var slotsFree = domain.MissionarySlots - domain.Missionaries.Count > 0;
            var villagerFree = snapshot.Population - domain.Missionaries.Count > 0;
            var asMarchlands = new DomainNode(node.Id, node.Name, node.Type, node.Q, node.R, node.Squalor);
            foreach (var (hours, _) in table.Domain.Sessions)
            {
                var p = table.Domain.ProvisionsPerHour;
                var cost = new ResourceBag(p.Timber * hours, p.Peat * hours, p.Scrap * hours, p.Devotion * hours);
                var gain = Math.Min(100 - myShare, (int)(table.Domain.GainMilli(asMarchlands, hours) * (100 + domain.MissionaryBonusPercent) / 100 / 1000));
                card.Options.Add(new PreachOption
                {
                    Hours = hours, Label = $"Preach {hours}h · +{gain}% · {VillagePresenter.ShortCost(cost)}", Enabled = slotsFree && villagerFree && snapshot.Stock.Covers(cost),
                });
            }

            card.Note = !slotsFree ? "Every missionary is out." : !villagerFree ? "Nobody left to send."
                : here.Any(h => h.cult != world.cultId) ? "Rivals preach here too: your gain comes first from the Authority, then from them." : "A missionary is a villager: they don't work while away.";
            return card;
        }

        /// <summary>The hexes from your village to a place (what SendWorldMissionary carries).</summary>
        public static int DistanceTo(WorldDto world, int nodeId) => MapFor(world).Node(nodeId)?.DistanceTo(world.q, world.r) ?? 0;

        /// <summary>The map's title (whose lands these are) and who rules them ("Lord Thorkel", "the Bishop of Sumpwell").</summary>
        public static (string Title, string Lord) Authority(WorldMap map, WorldDto world)
        {
            var nearest = map.Near(world.q, world.r, 6).OrderBy(n => n.DistanceTo(world.q, world.r)).ThenBy(n => n.Id).FirstOrDefault();
            var domain = nearest == null ? null : map.Domains.FirstOrDefault(d => d.Id == nearest.DomainId);
            if (domain == null)
            {
                return ("The wider world", "the lord");
            }

            const string lands = "The lands of ";
            const string see = "The see of ";
            var lord = domain.Name.StartsWith(lands, StringComparison.Ordinal) ? domain.Name.Substring(lands.Length)
                : domain.Name.StartsWith(see, StringComparison.Ordinal) ? "the Bishop of " + domain.Name.Substring(see.Length)
                : "the lord";
            return (domain.Name, lord);
        }

        private static Dictionary<int, List<WorldHoldingDto>> Holdings(WorldDto world) =>
            (world.holdings ?? new List<WorldHoldingDto>()).GroupBy(h => h.node).ToDictionary(g => g.Key, g => g.ToList());
    }
}
