using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>What earns festival points.</summary>
public enum FestivalDeed
{
    BuildMinutes,
    Stars,
    Visitors,
    StudyMinutes,
    PreachHours,
    Stages,
}

/// <summary>A festival day: its name, what counts, how much each counts, and its three reward tiers.</summary>
public sealed record FestivalDay(string Name, string Text, IReadOnlyList<(FestivalDeed Deed, int Points)> Counts, IReadOnlyList<(int Points, ResourceBag Bag, int Towels)> Tiers);

/// <summary>Today's festival as shown: the day, its points, and which tiers are claimed.</summary>
public sealed record FestivalView(int DayOfWeek, FestivalDay Day, int Points, IReadOnlyList<bool> Claimed, DateTimeOffset EndsAtUtc);

/// <summary>
/// The Comfort Festival (spec 2026-10-20 the festival; from Kingshot's weekly events): every week, seven days, each with
/// its own theme and three reward tiers. Days start at 00:00 UTC, Monday first, the same for everyone.
/// </summary>
public static class FestivalBook
{
    private static readonly IReadOnlyList<(int, ResourceBag, int)> Usual = new (int, ResourceBag, int)[]
    {
        (100, new ResourceBag(300, 0, 0, 0), 1),
        (300, new ResourceBag(0, 150, 150, 0), 1),
        (600, new ResourceBag(0, 0, 0, 120), 2),
    };

    public static IReadOnlyList<FestivalDay> Days { get; } = new[]
    {
        new FestivalDay("Builders' Day", "Every minute of building started counts.", new[] { (FestivalDeed.BuildMinutes, 1) }, Usual),
        new FestivalDay("The Night Watch", "Every star, in a Night or on the Pilgrims' Way.", new[] { (FestivalDeed.Stars, 40) }, Usual),
        new FestivalDay("Hospitality", "Every visitor taken in.", new[] { (FestivalDeed.Visitors, 120) }, Usual),
        new FestivalDay("The Scholars' Day", "Every minute of study started.", new[] { (FestivalDeed.StudyMinutes, 2) }, Usual),
        new FestivalDay("The Preachers' Day", "Every hour a preacher spends preaching.", new[] { (FestivalDeed.PreachHours, 30) }, Usual),
        new FestivalDay("The Pilgrims' Day", "Every stage of the Pilgrims' Way cleared.", new[] { (FestivalDeed.Stages, 120), (FestivalDeed.Stars, 20) }, Usual),
        new FestivalDay("The Grand Festival", "Everything counts, at half.", new[]
        {
            (FestivalDeed.BuildMinutes, 1), (FestivalDeed.Stars, 20), (FestivalDeed.Visitors, 60), (FestivalDeed.StudyMinutes, 1),
            (FestivalDeed.PreachHours, 15), (FestivalDeed.Stages, 60),
        }, new (int, ResourceBag, int)[]
        {
            (150, new ResourceBag(500, 100, 100, 0), 2),
            (450, new ResourceBag(0, 0, 300, 150), 2),
            (900, new ResourceBag(1000, 300, 300, 300), 4),
        }),
    };

    /// <summary>Days since the epoch (UTC).</summary>
    public static long DayOf(DateTimeOffset at) => at.UtcTicks / TimeSpan.TicksPerDay;

    /// <summary>Monday is 0.</summary>
    public static int DayOfWeek(long day) => (int)((day + 0) % 7); // 0001-01-01 was a Monday

    public static FestivalDay On(long day) => Days[DayOfWeek(day)];

    public static DateTimeOffset EndOf(long day) => new((day + 1) * TimeSpan.TicksPerDay, TimeSpan.Zero);
}
