using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>
    /// "Welcome back" (from Kingshot, 2026-10-20): what happened while the app was shut. The village is remembered as a
    /// line of text when the player leaves; on return, the difference is a short sheet. Pure: tested without the UI.
    /// </summary>
    public static class AwaySummary
    {
        public static readonly TimeSpan Worth = TimeSpan.FromMinutes(20);

        /// <summary>The village in one line: when, the four stores, villagers, and every building's id and level.</summary>
        public static string Remember(BaseSnapshot s) =>
            string.Join(";", new[]
            {
                s.AsOfUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
                $"{s.Stock.Timber},{s.Stock.Peat},{s.Stock.Scrap},{s.Stock.Devotion}",
                s.Population.ToString(CultureInfo.InvariantCulture),
                string.Join(",", s.Buildings.Select(b => $"{b.Id}:{b.Level}")),
            });

        /// <summary>The sheet for a return after <paramref name="remembered"/>, or null (too soon, nothing remembered, or unreadable).</summary>
        public static SheetModel Welcome(string remembered, BaseSnapshot now, BuildingTable table)
        {
            var parts = remembered?.Split(';');
            if (parts == null || parts.Length < 4 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return null;
            }

            var away = now.AsOfUtc - new DateTimeOffset(ticks, TimeSpan.Zero);
            if (away < Worth)
            {
                return null;
            }

            var stock = parts[1].Split(',').Select(v => int.TryParse(v, out var n) ? n : 0).ToArray();
            var gained = stock.Length == 4
                ? new ResourceBag(now.Stock.Timber - stock[0], now.Stock.Peat - stock[1], now.Stock.Scrap - stock[2], now.Stock.Devotion - stock[3])
                : default;
            int.TryParse(parts[2], out var population);
            var levels = parts[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(':')).Where(p => p.Length == 2).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]));

            var model = new SheetModel { Title = "Welcome back", Footer = $"Away {VillagePresenter.Duration((long)away.TotalSeconds)}. The cult carried on without you, mostly." };
            model.Rows.Add(new SheetRow { Heading = true, Text = "The stores" });
            var lines = new List<string>();
            void Line(string name, int delta)
            {
                if (delta != 0)
                {
                    lines.Add($"{(delta > 0 ? "+" : string.Empty)}{delta} {name}");
                }
            }

            Line("Timber", gained.Timber);
            Line("Peat", gained.Peat);
            Line("Scrap", gained.Scrap);
            Line("Devotion", gained.Devotion);
            model.Rows.Add(new SheetRow { Text = lines.Count == 0 ? "Just as you left them." : string.Join(" · ", lines),
                Detail = now.Stock.Timber >= now.Caps.Timber || now.Stock.Peat >= now.Caps.Peat ? "Some stores are full: build or upgrade a Storage Hut." : null });

            var news = new List<string>();
            if (now.Population > population)
            {
                news.Add($"{now.Population - population} new villager{(now.Population - population == 1 ? string.Empty : "s")} moved in.");
            }

            foreach (var b in now.Buildings.Where(b => b.Level > 0 && levels.TryGetValue(b.Id, out var was) && b.Level > was))
            {
                news.Add(b.Level == 1 ? $"The {table.Spec(b.Type).Name} is built." : $"The {table.Spec(b.Type).Name} reached level {b.Level}.");
            }

            if (now.Visitor != null)
            {
                news.Add($"{now.Visitor.Offer.Who} is waiting at the Hearth.");
            }

            if (now.Trial != null && !now.Trial.AlmsWaiting.IsZero)
            {
                news.Add($"The alms box holds {VillagePresenter.ShortCost(now.Trial.AlmsWaiting)}.");
            }

            if (now.Prophet?.Choice != null)
            {
                news.Add("The Prophet is ready to learn something new: tap his portrait.");
            }

            if (news.Count > 0)
            {
                model.Rows.Add(new SheetRow { Heading = true, Text = "Meanwhile" });
                model.Rows.AddRange(news.Select(n => new SheetRow { Text = n }));
            }

            return model;
        }
    }
}
