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

/// <summary>A preacher's report on getting home: where they went and what the road gave.</summary>
public sealed record RoadReport(int PreacherId, int NodeId, bool InWorld, int Hours, RoadFind Find, ResourceBag Gift, long HomeAtUtcTicks);

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

    /// <summary>What the road gives on the way home: fixed by the missionary's id, scaled by the hours preached.</summary>
    public static (RoadFind Find, ResourceBag Gift, int Xp) Find(int missionaryId, int preacherId, int hours)
    {
        unchecked
        {
            var h = (uint)missionaryId * 0x9e3779b1u ^ (uint)preacherId * 0x85ebca77u;
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            var roll = (int)(h % 100);
            var lucky = Quirk(preacherId) == PreacherQuirk.Lucky ? 2 : 1;
            return roll switch
            {
                < 40 => (RoadFind.Nothing, default, 0),
                < 65 => (RoadFind.Goat, new ResourceBag(8 * hours * lucky, 0, 0, 0), 0),
                < 80 => (RoadFind.Widow, new ResourceBag(0, 0, 0, 2 * hours * lucky), 0),
                < 90 => (RoadFind.Ditch, new ResourceBag(0, 0, 3 * hours * lucky, 0), 0),
                _ => (RoadFind.Turnips, default, 2),
            };
        }
    }

    private static int Cut(int amount) => amount - amount * ThriftyDiscountPercent / 100;

    private static string Ordinal(int n) => n switch { 2 => "Second", 3 => "Third", 4 => "Fourth", _ => $"{n}th" };
}

/// <summary>What the lord wants (spec 2026-10-15 Lord's demands), as the village sees it.</summary>
public sealed record DemandView(int Id, ResourceBag Amount, DateTimeOffset DueAtUtc, int TemperDrop, int Penalty, int Reason);

/// <summary>A demand as stored.</summary>
public sealed record DemandData(int Id, ResourceBag Amount, long DueAtUtcTicks);

/// <summary>The lord's demands: when they come, what he asks for, what meeting or missing one does. Pure.</summary>
public static class Demands
{
    public const int FirstGapHours = 10;
    public const int GapHours = 10;
    public const int GapJitterHours = 4;
    public const int WindowHours = 6;
    public const int MissPenalty = 15;

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
