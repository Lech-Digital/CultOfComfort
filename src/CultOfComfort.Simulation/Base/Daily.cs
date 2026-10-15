using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>What a daily errand counts (spec 2026-10-15 daily errands).</summary>
public enum DailyGoal
{
    PreachHours,
    Calm,
    WinNight,
    SurviveNight,
    Offerings,
    Upgrade,
    Repair,
    PaintRoads,
    Study,
    Trade,
    RoadFind,
    Build,
}

/// <summary>Ways to spend Towels.</summary>
public enum TowelUse
{
    /// <summary>Thirty minutes off the construction under way.</summary>
    Hurry,

    /// <summary>A different errand in one of today's slots.</summary>
    Reroll,
}

/// <summary>One kind of daily errand: what it counts, how many, and what it pays (at Hearth 1–2; scaled up after).</summary>
public sealed record DailyErrand(string Id, DailyGoal Goal, string Text, int Target, ResourceBag Reward);

/// <summary>One of today's errands as the client shows it.</summary>
public sealed record DailyErrandView(int Slot, string Id, DailyGoal Goal, string Text, int Progress, int Target, bool Claimed, ResourceBag Reward)
{
    public bool Done => Progress >= Target;
}

/// <summary>
/// Today's list (spec 2026-10-15): three errands, the Thermos once all three are claimed, the streak, the Towels, and
/// when the next list comes (04:00 on the player's clock).
/// </summary>
public sealed record DailyView(int Day, IReadOnlyList<DailyErrandView> Errands, bool ThermosReady, bool ThermosClaimed, int Streak, int Towels,
    IReadOnlyList<string> Cosmetics, DateTimeOffset NextListAtUtc);

/// <summary>The daily errands as stored.</summary>
public sealed record DailyData(int Day, int OffsetMinutes, IReadOnlyList<string> Ids, IReadOnlyList<int> Progress, IReadOnlyList<bool> Claimed, bool Thermos,
    int Streak, int LastThermosDay, int Towels, IReadOnlyList<string> Cosmetics);

/// <summary>The pool of daily errands, the Thermos and the streak. Pure.</summary>
public static class DailyBook
{
    public const int Slots = 3;
    public const int DayStartsAtMinutes = 4 * 60; // 04:00 local: a late night isn't a lost day
    public const int ThermosTowels = 3;
    public static readonly ResourceBag ThermosReward = new(0, 0, 0, 30);
    public const int StreakLength = 7;
    public const int StreakTowels = 10;
    public const int HurryCost = 1;
    public const int HurryMinutes = 30;
    public const int RerollCost = 2;

    /// <summary>Cosmetics a full week brings, in order (the first one shows at once: a golden duck by the Hearth).</summary>
    public static readonly string[] Cosmetics = { "golden-duck", "woad-bunting", "prophet-sunhat", "towel-flags", "garden-gnome" };

    public static readonly IReadOnlyList<DailyErrand> Pool = new DailyErrand[]
    {
        new("preach", DailyGoal.PreachHours, "Send preachers out for {0} hours in all.", 4, new ResourceBag(0, 0, 0, 25)),
        new("calm", DailyGoal.Calm, "Calm the lord: deliver his demand, or pay a tithe.", 1, new ResourceBag(120, 0, 0, 0)),
        new("win-night", DailyGoal.WinNight, "Win a Night with two stars or more.", 1, new ResourceBag(0, 0, 50, 0)),
        new("night", DailyGoal.SurviveNight, "See a Night through.", 1, new ResourceBag(100, 30, 0, 0)),
        new("offerings", DailyGoal.Offerings, "Collect {0} worth of offerings.", 150, new ResourceBag(0, 0, 0, 20)),
        new("upgrade", DailyGoal.Upgrade, "Upgrade a building.", 1, new ResourceBag(150, 0, 0, 0)),
        new("repair", DailyGoal.Repair, "Repair something the raiders broke.", 1, new ResourceBag(0, 0, 40, 0)),
        new("roads", DailyGoal.PaintRoads, "Lay {0} tiles of road.", 5, new ResourceBag(80, 0, 0, 0)),
        new("study", DailyGoal.Study, "Finish a study.", 1, new ResourceBag(0, 0, 0, 30)),
        new("trade", DailyGoal.Trade, "Strike a deal at the Market.", 1, new ResourceBag(0, 60, 0, 0)),
        new("road-find", DailyGoal.RoadFind, "Bring a preacher home with something from the road.", 1, new ResourceBag(0, 0, 0, 20)),
        new("build", DailyGoal.Build, "Build {0} new things (roads don't count).", 2, new ResourceBag(120, 0, 0, 0)),
    };

    public static DailyErrand? Get(string id) => Pool.FirstOrDefault(e => e.Id == id);

    /// <summary>How the targets and rewards grow with the Hearth, in percent.</summary>
    public static int Scale(int hearthLevel) => hearthLevel switch { <= 2 => 100, 3 => 150, _ => 200 };

    public static int Target(DailyErrand errand, int hearthLevel) =>
        errand.Goal is DailyGoal.PreachHours or DailyGoal.Offerings or DailyGoal.PaintRoads ? errand.Target * Scale(hearthLevel) / 100 : errand.Target;

    public static ResourceBag Reward(DailyErrand errand, int hearthLevel)
    {
        var s = Scale(hearthLevel);
        return new ResourceBag(errand.Reward.Timber * s / 100, errand.Reward.Peat * s / 100, errand.Reward.Scrap * s / 100, errand.Reward.Devotion * s / 100);
    }

    public static string Text(DailyErrand errand, int hearthLevel) => string.Format(errand.Text, Target(errand, hearthLevel));

    /// <summary>The day a moment falls in, on the player's clock (<paramref name="offsetMinutes"/> from UTC), days starting at 04:00.</summary>
    public static int DayOf(DateTimeOffset at, int offsetMinutes) =>
        (int)Math.Floor((at.UtcTicks / (double)TimeSpan.TicksPerMinute + offsetMinutes - DayStartsAtMinutes) / (24 * 60));

    public static DateTimeOffset DayStart(int day, int offsetMinutes) =>
        new DateTimeOffset((long)((day * 24L * 60 + DayStartsAtMinutes - offsetMinutes) * TimeSpan.TicksPerMinute), TimeSpan.Zero);

    /// <summary>
    /// Today's three: the same list for every cult on day <paramref name="day"/> (a shuffle of the pool by the day),
    /// skipping what can't be done (a study when every study is done).
    /// </summary>
    public static List<string> Pick(int day, Func<DailyErrand, bool> possible, IEnumerable<string>? avoid = null)
    {
        var skip = new HashSet<string>(avoid ?? Array.Empty<string>());
        return Pool.Where(e => possible(e) && !skip.Contains(e.Id)).OrderBy(e => Mix(day, e.Id)).Select(e => e.Id).Take(Slots).ToList();
    }

    private static uint Mix(int day, string id)
    {
        unchecked
        {
            var h = (uint)day * 0x9e3779b1u;
            foreach (var c in id)
            {
                h = (h ^ c) * 0x01000193u;
            }

            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h;
        }
    }
}
