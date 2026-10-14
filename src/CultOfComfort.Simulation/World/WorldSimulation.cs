using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.World;

/// <summary>What a world looked like at the end of one simulated day.</summary>
/// <param name="Held">Places where any cult has influence.</param>
/// <param name="Contested">Places with two or more cults.</param>
/// <param name="TensionAvg">The average cult's influence-driven Tension per hour (whole points).</param>
/// <param name="OfferingsAvg">The average cult's offerings an hour, as tithe worth (Timber 1, Peat 2, Scrap 2, Devotion 4).</param>
public sealed record WorldDay(
    int Day, int Cults, int Bots, int Held, int Contested, int Patrons, double TensionAvg, double TensionMax, double OfferingsAvg, int Sermons);

/// <summary>
/// The world harness (spec 2026-10-14 shared world, slice 3): a whole world on an accelerated clock. Bots play by
/// <see cref="BotCults"/>; "humans" are steady players with Hearth 2–3 habits who arrive over the first days. It runs the
/// same ledger the server runs, so whatever it reports is what the server would see.
/// </summary>
public static class WorldSimulation
{
    public static IReadOnlyList<WorldDay> Run(ulong seed, int humans, int bots, int days, int activeTarget = 150, int stepMinutes = 30)
    {
        var table = DomainTable.Default;
        var map = WorldGenerator.Generate(seed);
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var ledger = new WorldLedger(map, table, start);
        BotCults.Populate(ledger, bots, seed, start);
        var arrivals = Enumerable.Range(0, humans).Select(i => (At: start.AddMinutes(i * days * 24.0 * 60 / 3 / Math.Max(1, humans)), Id: $"p{i:D4}")).ToList();
        var report = new List<WorldDay>();
        var sermons = 0;
        var landedBefore = 0;
        var nextArrival = 0;
        var stepsPerDay = 24 * 60 / stepMinutes;
        for (var step = 0; step < days * stepsPerDay; step++)
        {
            var now = start.AddMinutes(step * stepMinutes);
            while (nextArrival < arrivals.Count && arrivals[nextArrival].At <= now)
            {
                var (_, id) = arrivals[nextArrival++];
                ledger.Join(id, string.Empty, isBot: false, (ulong)(nextArrival * 7919) ^ seed, now);
                BotCults.Retire(ledger, Math.Max(activeTarget, ledger.Humans));
            }

            var before = ledger.Missionaries.Count;
            ledger.Advance(now);
            landedBefore += before - ledger.Missionaries.Count;
            sermons += BotCults.Act(ledger, table, now, Habits, humansToo: true);
            if ((step + 1) % stepsPerDay == 0)
            {
                report.Add(Measure(ledger, table, (step + 1) / stepsPerDay, landedBefore));
                landedBefore = 0;
            }
        }

        return report;
    }

    /// <summary>A plain-text table of the report (for the test output and the sandbox).</summary>
    public static string Format(IReadOnlyList<WorldDay> days)
    {
        var text = new StringBuilder();
        text.AppendLine("day  cults  bots  held  contested  patrons  tension/h avg  max  offerings/h avg  sermons");
        foreach (var d in days)
        {
            text.AppendLine($"{d.Day,3}  {d.Cults,5}  {d.Bots,4}  {d.Held,4}  {d.Contested,9}  {d.Patrons,7}  {d.TensionAvg,13:F1}  {d.TensionMax,4:F1}  {d.OfferingsAvg,15:F1}  {d.Sermons,7}");
        }

        return text.ToString();
    }

    /// <summary>Humans: bots' habits, but a Hearth 2 reach (3) and slot (1) for half, Hearth 3 (5, 2) for the rest.</summary>
    private static CultHabits Habits(WorldCult cult)
    {
        if (cult.IsBot)
        {
            return BotCults.HabitsOf(cult.Id);
        }

        var bot = BotCults.HabitsOf(cult.Id);
        return bot.Slots == 3 ? bot with { Slots = 2, Reach = 5 } : bot with { Slots = 1, Reach = 3 };
    }

    private static WorldDay Measure(WorldLedger ledger, DomainTable table, int day, int sermons)
    {
        var held = 0;
        var contested = 0;
        var patrons = 0;
        foreach (var node in ledger.Map.Nodes)
        {
            var holders = ledger.Holders(node.Id).Count;
            held += holders > 0 ? 1 : 0;
            contested += holders > 1 ? 1 : 0;
            patrons += ledger.PatronOf(node.Id) != null ? 1 : 0;
        }

        var cults = ledger.Cults.ToList();
        var tension = cults.Select(c => ledger.TensionPerHourMilli(c.Id) / 1000.0).ToList();
        var offerings = cults.Select(c => (double)DomainTable.TitheValue(ledger.OfferingsPerHour(c.Id))).ToList();
        return new WorldDay(day, cults.Count, cults.Count(c => c.IsBot), held, contested, patrons,
            tension.Count == 0 ? 0 : tension.Average(), tension.Count == 0 ? 0 : tension.Max(), offerings.Count == 0 ? 0 : offerings.Average(), sermons);
    }
}
