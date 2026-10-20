using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>Who's at the Hearth (spec 2026-10-20 visitors).</summary>
public enum VisitorKind
{
    Pilgrim,
    Pedlar,
    LostSheep,
}

/// <summary>What a visitor asks and what it gives back.</summary>
public sealed record VisitorOffer(VisitorKind Kind, string Who, string Says, ResourceBag Asks, ResourceBag Gives, int Towels = 0, int Villagers = 0);

/// <summary>A visitor as shown: the offer, when it leaves, a seed for where it stands, and whether the stores can pay.</summary>
public sealed record VisitorView(VisitorOffer Offer, int OfferIndex, DateTimeOffset LeavesAtUtc, int Seed, bool CanPay, long Slot);

/// <summary>
/// The visitors (spec 2026-10-20 visitors; owner: "more in game activities"): from Hearth 2, someone turns up at the
/// Hearth every few hours with something to ask or to trade, and waits a while. Short, small choices for a short visit.
/// </summary>
public static class VisitorBook
{
    public const int FromHearth = 2;

    /// <summary>
    /// Visitors come in slots on the clock (every four hours, UTC), one per slot, arriving within the slot's first hour:
    /// the same visitor on every device, whenever it looks, with nothing to schedule or store but which slot was answered.
    /// </summary>
    public const int SlotMinutes = 240;
    public const int StayMinutes = 180;
    public const int SheepStayMinutes = 120;

    public static long SlotOf(DateTimeOffset at) => at.UtcTicks / TimeSpan.TicksPerMinute / SlotMinutes;

    public static DateTimeOffset SlotStart(long slot) => new(slot * SlotMinutes * TimeSpan.TicksPerMinute, TimeSpan.Zero);

    /// <summary>When the visitor of a slot arrives (within the slot's first hour) and which offer it brings.</summary>
    public static (DateTimeOffset Arrives, DateTimeOffset Leaves, int Offer) Visit(long slot, int hearthLevel)
    {
        var start = SlotStart(slot);
        var arrives = start.AddMinutes(Hash(start) % 60);
        var offer = Pick(start, hearthLevel);
        var stay = Offers[offer].Kind == VisitorKind.LostSheep ? SheepStayMinutes : StayMinutes;
        return (arrives, arrives.AddMinutes(stay), offer);
    }

    public static IReadOnlyList<VisitorOffer> Offers { get; } = new[]
    {
        new VisitorOffer(VisitorKind.Pilgrim, "A muddy pilgrim", "\"A hot bath and somewhere to sleep, and I'll stay and believe whatever you like.\"",
            new ResourceBag(0, 40, 0, 0), new ResourceBag(0, 0, 0, 30), Villagers: 1),
        new VisitorOffer(VisitorKind.Pilgrim, "A cold pilgrim", "\"Something to burn for the road? I'll say a word for your Hearth wherever I go.\"",
            new ResourceBag(0, 60, 0, 0), new ResourceBag(0, 0, 0, 60)),
        new VisitorOffer(VisitorKind.Pilgrim, "A lost pilgrim", "\"Is this the way to the comfy place? It is? Bless you.\"",
            default, new ResourceBag(0, 0, 0, 20)),
        new VisitorOffer(VisitorKind.Pilgrim, "A carpenter on pilgrimage", "\"Feed me and I'll mend whatever needs mending. Then I'll mend more.\"",
            new ResourceBag(0, 30, 0, 20), new ResourceBag(250, 0, 0, 0), Villagers: 1),
        new VisitorOffer(VisitorKind.Pedlar, "A pedlar's cart", "\"Nails, hinges, pots with only a small hole. For timber, mind.\"",
            new ResourceBag(300, 0, 0, 0), new ResourceBag(0, 0, 120, 0)),
        new VisitorOffer(VisitorKind.Pedlar, "A pedlar's cart", "\"Towels! Fluffy ones! Barely stolen.\"",
            new ResourceBag(0, 0, 150, 0), default, Towels: 2),
        new VisitorOffer(VisitorKind.Pedlar, "A pedlar's cart", "\"Pray for me and I'll leave you a cartload of planks.\"",
            new ResourceBag(0, 0, 0, 80), new ResourceBag(400, 0, 0, 0)),
        new VisitorOffer(VisitorKind.Pedlar, "A pedlar's cart", "\"Peat for prayers? Prayers for peat. Whichever way round.\"",
            new ResourceBag(0, 120, 0, 0), new ResourceBag(0, 0, 0, 70)),
        new VisitorOffer(VisitorKind.LostSheep, "A lost sheep", "It has wandered in and is eating the thatch. Its shepherd will be glad to have it back.",
            default, new ResourceBag(80, 0, 0, 15), Towels: 1),
    };

    /// <summary>A deterministic hash of a moment, for which visitor comes and when the next one does.</summary>
    public static int Hash(DateTimeOffset at)
    {
        unchecked
        {
            var h = (ulong)at.UtcTicks * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29;
            return (int)(h & 0x7fffffff);
        }
    }

    /// <summary>Which offer arrives at <paramref name="at"/>: pilgrims most often, a pedlar from Hearth 3, now and then a sheep.</summary>
    public static int Pick(DateTimeOffset at, int hearthLevel)
    {
        var h = Hash(at);
        var roll = h % 10;
        var kind = roll < 5 ? VisitorKind.Pilgrim : roll < 8 ? (hearthLevel >= 3 ? VisitorKind.Pedlar : VisitorKind.Pilgrim) : VisitorKind.LostSheep;
        var choices = new List<int>();
        for (var i = 0; i < Offers.Count; i++)
        {
            if (Offers[i].Kind == kind)
            {
                choices.Add(i);
            }
        }

        return choices[(h / 10) % choices.Count];
    }
}
