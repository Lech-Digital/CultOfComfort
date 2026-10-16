using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>A preacher's quirk (spec 2026-10-15 Mission House).</summary>
public enum PreacherQuirk
{
    SilverTongued,
    Thrifty,
    Tireless,
    Lucky,
}

/// <summary>What the road gave a preacher on the way home.</summary>
public enum RoadFind
{
    Nothing,
    Goat,
    Widow,
    Ditch,
    Turnips,
}

/// <summary>One preacher of the Mission House: who they are (from their id), how far they've come, where they are.</summary>
public sealed record PreacherView(int Id, string Name, PreacherQuirk Quirk, int Level, int Xp, int XpForNext, int BonusPercent, int? MissionaryId);

/// <summary>
/// A preacher's report on getting home: where they went and what the road gave. <paramref name="Tale"/> and
/// <paramref name="Choice"/> say which road tale it was and what you chose (null for a report from before the tales).
/// </summary>
public sealed record RoadReport(int PreacherId, int NodeId, bool InWorld, int Hours, RoadFind Find, ResourceBag Gift, long HomeAtUtcTicks,
    RoadTale? Tale = null, int Choice = 0);

/// <summary>What happened on the road (spec 2026-10-16 mission board): each comes home with a choice of two.</summary>
public enum RoadTale
{
    Goat,
    Widow,
    Ditch,
    Toll,
    Turnips,
    Pedlar,
    Bog,
    Shrine,
}

/// <summary>A road tale waiting for you to choose: told by a preacher who just got home.</summary>
public sealed record TaleData(int Id, int PreacherId, int NodeId, bool InWorld, int Hours, RoadTale Tale, long HomeAtUtcTicks);

/// <summary>One way a road tale can end: what it gives, what it costs, and the odds (100: certain).</summary>
public sealed record TaleOption(ResourceBag Gift, ResourceBag Cost, int Xp, int Tension, int ChancePercent);

/// <summary>A road tale as the village sees it: who's telling it, and its two endings.</summary>
public sealed record TaleView(int Id, int PreacherId, int NodeId, bool InWorld, int Hours, RoadTale Tale, IReadOnlyList<TaleOption> Options);

/// <summary>
/// The Mission House's preachers (spec 2026-10-15): one per missionary slot, named and quirked by their id so the phone
/// and the server agree without storing it; only experience is kept. Pure.
/// </summary>
public static class Preachers
{
    private static readonly string[] Names =
    {
        "Brother Damp", "Sister Drizzle", "Friar Puddle", "Mother Soak", "Brother Mildew", "Sister Murk", "Old Barnaby Bog",
        "Deacon Squelch", "Sister Wellington", "Brother Fen", "Little Agnes Ooze", "Father Sludgeric", "Sister Steam", "Brother Ladle",
        "Auntie Mire", "Wee Cuthbert", "Sister Towel", "Brother Sponge", "Granny Peat", "Brother Plinth",
    };

    /// <summary>Experience for each level, from level 1 (8, 24, 56 and 112 hours of preaching for a plain preacher).</summary>
    public static readonly int[] LevelXp = { 0, 16, 48, 112, 224 };

    public const int BonusPerLevelPercent = 6;
    public const int SilverTonguedPercent = 15;
    public const int ThriftyDiscountPercent = 30;

    public static string Name(int id) => Names[(id - 1) % Names.Length] + (id > Names.Length ? $" the {Ordinal((id - 1) / Names.Length + 1)}" : string.Empty);

    private static readonly PreacherQuirk[] QuirkOrder = { PreacherQuirk.Tireless, PreacherQuirk.Thrifty, PreacherQuirk.Lucky, PreacherQuirk.SilverTongued };

    /// <summary>
    /// Quirks in a fixed order: the first preacher is Tireless (more experience, not more faith), so the first hours on
    /// the map are no faster than before; the Silver-tongued comes with the fourth seat.
    /// </summary>
    public static PreacherQuirk Quirk(int id) => QuirkOrder[(id - 1) % QuirkOrder.Length];

    public static int Level(int xp)
    {
        var level = 1;
        for (var i = 1; i < LevelXp.Length; i++)
        {
            if (xp >= LevelXp[i])
            {
                level = i + 1;
            }
        }

        return level;
    }

    public static int XpForNext(int xp) => Level(xp) >= LevelXp.Length ? 0 : LevelXp[Level(xp)];

    /// <summary>How much more faith a preacher wins than a plain missionary, in percent (level and quirk).</summary>
    public static int BonusPercent(int id, int xp) =>
        (Level(xp) - 1) * BonusPerLevelPercent + (Quirk(id) == PreacherQuirk.SilverTongued ? SilverTonguedPercent : 0);

    /// <summary>Experience a sermon of <paramref name="hours"/> earns: 2 an hour, 3 for the Tireless.</summary>
    public static int XpFor(int id, int hours) => hours * (Quirk(id) == PreacherQuirk.Tireless ? 3 : 2);

    /// <summary>Provisions for a sermon, after the Thrifty's haggling.</summary>
    public static ResourceBag Provisions(int id, ResourceBag full) => Quirk(id) != PreacherQuirk.Thrifty
        ? full
        : new ResourceBag(Cut(full.Timber), Cut(full.Peat), Cut(full.Scrap), Cut(full.Devotion));

    private static int Cut(int amount) => amount - amount * ThriftyDiscountPercent / 100;

    private static string Ordinal(int n) => n switch { 2 => "Second", 3 => "Third", 4 => "Fourth", _ => $"{n}th" };
}

/// <summary>
/// Road tales (spec 2026-10-16 mission board): which one a preacher brings home (fixed by the missionary's id) and
/// what each choice gives. Amounts scale with the hours preached; the Lucky find twice as much. Pure.
/// </summary>
public static class Tales
{
    /// <summary>Tales waiting at once: one more and the oldest ends the free way on its own.</summary>
    public const int Kept = 4;

    public static RoadTale Pick(int missionaryId, int preacherId)
    {
        var tales = (RoadTale[])Enum.GetValues(typeof(RoadTale));
        return tales[(int)(Mix((uint)missionaryId, (uint)preacherId) % (uint)tales.Length)];
    }

    /// <summary>The tale's two endings for a sermon of <paramref name="hours"/> by <paramref name="preacherId"/>.</summary>
    public static IReadOnlyList<TaleOption> Options(RoadTale tale, int preacherId, int hours)
    {
        var k = Preachers.Quirk(preacherId) == PreacherQuirk.Lucky ? 2 : 1;
        ResourceBag T(int n) => new(n * hours * k, 0, 0, 0);
        ResourceBag P(int n) => new(0, n * hours * k, 0, 0);
        ResourceBag S(int n) => new(0, 0, n * hours * k, 0);
        ResourceBag D(int n) => new(0, 0, 0, n * hours * k);
        TaleOption Give(ResourceBag gift, int xp = 0) => new(gift, default, xp, 0, 100);
        return tale switch
        {
            RoadTale.Goat => new[] { Give(T(8)), Give(D(3)) },
            RoadTale.Widow => new[] { Give(D(2), 2), Give(T(6)) },
            RoadTale.Ditch => new[] { Give(S(3)), new TaleOption(S(7), default, 0, 0, 60) },
            RoadTale.Toll => new[] { new TaleOption(D(3), new ResourceBag(4 * hours, 0, 0, 0), 2, 0, 100), Give(P(4)) },
            RoadTale.Turnips => new[] { Give(default, 3), Give(P(5)) },
            RoadTale.Pedlar => new[] { Give(S(3)), Give(D(2), 1) },
            RoadTale.Bog => new[] { Give(default, 2), Give(P(6)) },
            _ => new[] { Give(default, 3), new TaleOption(D(3), default, 0, 2, 100) },
        };
    }

    /// <summary>Whether a gamble (<see cref="TaleOption.ChancePercent"/> under 100) pays off for tale <paramref name="taleId"/>.</summary>
    public static bool Lucky(int taleId, int chancePercent) => chancePercent >= 100 || Mix((uint)taleId, 0x7a1eu) % 100 < (uint)chancePercent;

    /// <summary>The ending a tale gets if you never choose: the first that costs nothing.</summary>
    public static int FreeChoice(IReadOnlyList<TaleOption> options)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Cost.IsZero && options[i].Tension == 0)
            {
                return i;
            }
        }

        return 0;
    }

    private static uint Mix(uint a, uint b)
    {
        unchecked
        {
            var h = a * 0x9e3779b1u ^ b * 0x85ebca77u;
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h;
        }
    }
}

/// <summary>What the lord wants (spec 2026-10-15 Lord's demands), as the village sees it.</summary>
/// <param name="Haggle">Whether you've haggled, and how it went.</param>
/// <param name="Flattered">You've flattered him: the next haggle can't fail.</param>
/// <param name="HaggledAmount">What he'd take if a haggle works.</param>
/// <param name="FlatterCost">What flattering him costs (Devotion: candles, a poem, a portrait).</param>
public sealed record DemandView(int Id, ResourceBag Amount, DateTimeOffset DueAtUtc, int TemperDrop, int Penalty, int Reason,
    bool Stalled = false, DemandHaggle Haggle = DemandHaggle.None, bool Flattered = false, ResourceBag HaggledAmount = default,
    ResourceBag FlatterCost = default, int HaggleChancePercent = 0);

/// <summary>How haggling over a demand went.</summary>
public enum DemandHaggle
{
    None,
    Won,
    Refused,
}

/// <summary>What you try on the lord in his hall instead of simply paying (spec 2026-10-16 the lord's hall).</summary>
public enum Plea
{
    /// <summary>Ask him to take less. Half the time he does; otherwise he's insulted.</summary>
    Haggle,

    /// <summary>Candles and a poem about his chin: the next haggle can't fail.</summary>
    Flatter,

    /// <summary>"The cart has a wobbly wheel": more time, but he doesn't like waiting.</summary>
    Stall,
}

/// <summary>A demand as stored.</summary>
public sealed record DemandData(int Id, ResourceBag Amount, long DueAtUtcTicks, bool Stalled = false, DemandHaggle Haggle = DemandHaggle.None, bool Flattered = false);

/// <summary>The lord's demands: when they come, what he asks for, what meeting or missing one does. Pure.</summary>
public static class Demands
{
    public const int FirstGapHours = 10;
    public const int GapHours = 10;
    public const int GapJitterHours = 4;
    public const int WindowHours = 6;
    public const int MissPenalty = 15;

    /// <summary>Stalling: hours more, and the temper it costs.</summary>
    public const int StallHours = 3;
    public const int StallPenalty = 5;

    /// <summary>Haggling: what's left of the demand when he agrees (percent), the odds, and the temper when he refuses.</summary>
    public const int HaggledPercent = 60;
    public const int HaggleChancePercent = 50;
    public const int RefusePenalty = 8;

    /// <summary>Flattery's price in Devotion, per Hearth level.</summary>
    public const int FlatterDevotionPerHearth = 15;

    public static ResourceBag Haggled(ResourceBag amount)
    {
        int Cut(int n) => n * HaggledPercent / 100 / 10 * 10;
        return new ResourceBag(Cut(amount.Timber), Cut(amount.Peat), Cut(amount.Scrap), Cut(amount.Devotion));
    }

    public static ResourceBag FlatterCost(int hearthLevel) => new(0, 0, 0, FlatterDevotionPerHearth * Math.Max(1, hearthLevel));

    /// <summary>Whether a haggle over demand <paramref name="id"/> works (always, once he's been flattered).</summary>
    public static bool HaggleWorks(int id, bool flattered) => flattered || Mix(id, 19) % 100 < HaggleChancePercent;

    /// <summary>A demand counts double a tithe of the same worth.</summary>
    public const int DropMultiplier = 2;

    /// <summary>The reasons he gives (the client words them; the index is stored).</summary>
    public const int Reasons = 8;

    /// <summary>The gap after a demand is settled before the next (10–14 hours, fixed by the next demand's id).</summary>
    public static TimeSpan Gap(int nextId) => TimeSpan.FromHours(GapHours + Mix(nextId, 7) % (GapJitterHours + 1));

    /// <summary>What demand <paramref name="id"/> asks of a village at <paramref name="hearthLevel"/>.</summary>
    public static ResourceBag Amount(int id, int hearthLevel)
    {
        var scale = hearthLevel switch { <= 2 => 100, 3 => 170, _ => 260 };
        int S(int baseAmount) => baseAmount * scale / 100 / 10 * 10;
        return (Mix(id, 3) % 5) switch
        {
            0 => new ResourceBag(S(300), 0, 0, 0),
            1 => new ResourceBag(S(150), S(60), 0, 0),
            2 => new ResourceBag(0, 0, S(70), 0),
            3 => new ResourceBag(S(120), 0, 0, S(40)),
            _ => new ResourceBag(0, S(90), S(30), 0),
        };
    }

    public static int Reason(int id) => (int)(Mix(id, 11) % Reasons);

    /// <summary>How much his temper drops when the demand is met (twice a tithe's worth, never halved).</summary>
    public static int TemperDrop(ResourceBag amount, DomainTable table) => DomainTable.TitheValue(amount) * DropMultiplier / table.TitheValuePerTension;

    private static uint Mix(int id, uint salt)
    {
        unchecked
        {
            var h = (uint)id * 0x27d4eb2du ^ salt * 0x165667b1u;
            h ^= h >> 15;
            h *= 0x85ebca77u;
            h ^= h >> 13;
            return h;
        }
    }
}
