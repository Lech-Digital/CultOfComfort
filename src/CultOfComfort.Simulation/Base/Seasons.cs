using System;

namespace CultOfComfort.Simulation.Base;

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

/// <summary>
/// The seasons (spec 2026-10-20 seasons; owner: "the world changes, so a layout stays good for a while, not forever"):
/// three days each on the clock, the same for everyone. Each changes the Nights: a spring flood shuts the south bridge,
/// summer nights are short, autumn raids come for the harvest (and leave more behind), winter snow slows the raiders.
/// </summary>
public static class SeasonBook
{
    public const int DaysPerSeason = 3;

    public static Season Of(DateTimeOffset at) => (Season)(int)(at.UtcTicks / TimeSpan.TicksPerDay / DaysPerSeason % 4);

    public static DateTimeOffset EndsAt(DateTimeOffset at)
    {
        var length = TimeSpan.TicksPerDay * DaysPerSeason;
        return new DateTimeOffset((at.UtcTicks / length + 1) * length, TimeSpan.Zero);
    }

    /// <summary>The road a spring flood shuts (the south road's bridge is under water).</summary>
    public const int FloodedRoad = 1;

    /// <summary>A Night's size, in percent, by season.</summary>
    public static int BudgetPercent(Season season) => season switch
    {
        Season.Summer => 85,
        Season.Autumn => 115,
        _ => 100,
    };

    /// <summary>Raiders' speed change, in percent (winter: snow).</summary>
    public static int EnemySpeedPercent(Season season) => season == Season.Winter ? -15 : 0;

    /// <summary>Extra Scrap after a Night, in percent (autumn: they came for the harvest and dropped it).</summary>
    public static int SalvagePercent(Season season) => season == Season.Autumn ? 50 : 0;

    public static string Name(Season season) => season.ToString();

    public static string Note(Season season) => season switch
    {
        Season.Spring => "The river's up: the south bridge is under water and nobody comes that way.",
        Season.Summer => "Short nights: the raids are smaller.",
        Season.Autumn => "Harvest: bigger raids, and more Scrap left behind.",
        _ => "Snow: the raiders trudge, slower on every road.",
    };
}
