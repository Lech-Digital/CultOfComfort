using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.World;

/// <summary>How a cult plays: when it is awake, how many missionaries it keeps out, how far they walk.</summary>
public sealed record CultHabits(int WakeHourUtc, int AwakeHours, int Slots, int Reach);

/// <summary>
/// Bot cults (spec 2026-10-14 shared world): world-only rivals with no village behind them, playing like a steady
/// player: a few missionaries out while awake, a long sermon before bed, the richest place they don't yet own. They
/// share the ledger's rules, so they are also the harness that tests those rules at full size.
/// </summary>
public static class BotCults
{
    public const string IdPrefix = "bot-";

    private static readonly string[] Firsts =
    {
        "The Damp", "The Lukewarm", "The Drowsy", "The Snug", "The Woolly", "The Steaming", "The Toasty", "The Soggy", "The Cosy",
        "The Fluffy", "The Muddled", "The Sleepy", "The Draughty", "The Crumpled", "The Tepid", "The Mumbling",
    };

    private static readonly string[] Seconds =
    {
        "Brethren", "Sock Society", "Kettle Fellowship", "Order of the Blanket", "Friends of the Fire", "Puddle Saints", "Porridge Circle",
        "Hot Stone Lodge", "Slipper Guild", "Choir of the Hearth", "Tea Leaf Seers", "Mitten Monks", "Bath Night Faithful", "Pillow Folk",
    };

    /// <summary>Adds <paramref name="count"/> bot cults (placed like any village); returns how many found room.</summary>
    public static int Populate(WorldLedger ledger, int count, ulong seed, DateTimeOffset at)
    {
        var random = new PcgRandom(seed, RandomStreams.Bots);
        var names = new HashSet<string>(ledger.Cults.Select(c => c.Name));
        var added = 0;
        var next = ledger.Cults.Count(c => c.IsBot) + 1;
        for (var i = 0; i < count; i++)
        {
            string name;
            do
            {
                name = $"{Firsts[random.Next(0, Firsts.Length)]} {Seconds[random.Next(0, Seconds.Length)]}";
            }
            while (!names.Add(name) && names.Count < Firsts.Length * Seconds.Length);

            if (ledger.Join($"{IdPrefix}{next++:D3}", name, isBot: true, random.NextUInt() | ((ulong)random.NextUInt() << 32), at) == null)
            {
                break;
            }

            added++;
        }

        return added;
    }

    /// <summary>A bot's habits, fixed by its id: spread over the clock (players live in many time zones).</summary>
    public static CultHabits HabitsOf(string cultId)
    {
        var h = Fnv(cultId);
        return new CultHabits((int)(h % 24), 14 + (int)(h / 24 % 4), 2 + (int)(h / 96 % 2), 4 + (int)(h / 192 % 2));
    }

    /// <summary>
    /// Every bot that is awake and has a missionary free sends one: 4 hours by day, 8 before bed. Run it on each world
    /// tick; it never sends more than its slots, so calling it often is harmless.
    /// </summary>
    public static int Act(WorldLedger ledger, DomainTable table, DateTimeOffset now, Func<WorldCult, CultHabits>? habits = null, bool humansToo = false)
    {
        var sent = 0;
        var outBy = ledger.Missionaries.GroupBy(m => m.CultId).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var cult in ledger.Cults.Where(c => c.IsBot || humansToo).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            var how = habits?.Invoke(cult) ?? HabitsOf(cult.Id);
            var hourAwake = ((now.UtcDateTime.Hour - how.WakeHourUtc) % 24 + 24) % 24;
            var busy = outBy.TryGetValue(cult.Id, out var n) ? n : 0;
            if (hourAwake >= how.AwakeHours || busy >= how.Slots)
            {
                continue;
            }

            var target = Target(ledger, table, cult, how.Reach);
            if (target == null)
            {
                continue;
            }

            var hours = hourAwake >= how.AwakeHours - 2 ? 8 : 4; // the last sermon of the day runs through the night
            ledger.Dispatch(cult.Id, target.Id, hours, 0, now);
            outBy[cult.Id] = busy + 1;
            sent++;
        }

        return sent;
    }

    /// <summary>Keeps the map about <paramref name="activeTarget"/> cults strong: bots with the least influence retire as humans arrive.</summary>
    public static int Retire(WorldLedger ledger, int activeTarget)
    {
        var retired = 0;
        while (ledger.Cults.Count > activeTarget)
        {
            var weakest = ledger.Cults.Where(c => c.IsBot)
                .Select(c => (c.Id, Held: ledger.PlacesOf(c.Id).Sum(node => ledger.Influence(node, c.Id))))
                .OrderBy(c => c.Held).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (weakest.Id == null)
            {
                break;
            }

            ledger.Remove(weakest.Id);
            retired++;
        }

        return retired;
    }

    /// <summary>The place worth most to this cult now: rich, receptive, not yet its own, close by, not already being preached at.</summary>
    private static WorldNode? Target(WorldLedger ledger, DomainTable table, WorldCult cult, int reach)
    {
        var preaching = new HashSet<int>(ledger.Missionaries.Where(m => m.CultId == cult.Id).Select(m => m.NodeId));
        WorldNode? best = null;
        long bestScore = 0;
        foreach (var node in ledger.Map.Near(cult.Q, cult.R, reach))
        {
            var kind = table.Kinds[node.Type];
            var worth = DomainTable.TitheValue(kind.OfferingsPerHour); // Timber 1, Peat 2, Scrap 2, Devotion 4 an hour
            if (worth == 0 || preaching.Contains(node.Id))
            {
                continue;
            }

            var open = WorldLedger.Full - ledger.Influence(node.Id, cult.Id);
            var score = worth * kind.ReceptivityPercent * open / (1 + node.DistanceTo(cult.Q, cult.R)) + (long)(Fnv(cult.Id + node.Id) % 1000); // a tie-breaker, so bots next door spread out
            if (score > bestScore)
            {
                best = node;
                bestScore = score;
            }
        }

        return best;
    }

    private static ulong Fnv(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return hash;
    }
}
