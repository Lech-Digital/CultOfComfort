using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

public enum TensionState
{
    Calm,
    Uneasy,
    Hostile,
    Punitive,
}

/// <summary>How the Authority's patience runs out (spec: Tension and raids; GDD 04).</summary>
/// <param name="RatePerHourByHearth">Tension gained per hour at Hearth level 1, 2, 3 (level 1 = new-player shield).</param>
/// <param name="AbandonedNightMinutes">A Night begun but never resolved is settled (towers only) after this long.</param>
public sealed record TensionTable(
    IReadOnlyList<int> RatePerHourByHearth,
    int Uneasy,
    int Hostile,
    int Punitive,
    int Max,
    int WarningHours,
    int ShieldHours,
    IReadOnlyList<int> BasePowerByHearth,
    int AbandonedNightMinutes)
{
    // 2026-10-14: passive rise 0/6/9 (was 0/8/12): the Marchlands add Tension from influence on top (spec world map).
    public static TensionTable Default { get; } = new(new[] { 0, 6, 9, 12 }, 40, 70, 100, 150, 8, 12, new[] { 40, 54, 72, 90 }, 15); // 2026-10-16: Hearth 4

    public int RatePerHour(int hearthLevel) => RatePerHourByHearth[Math.Min(Math.Max(hearthLevel, 1), RatePerHourByHearth.Count) - 1];

    public int BasePower(int hearthLevel) => BasePowerByHearth[Math.Min(Math.Max(hearthLevel, 1), BasePowerByHearth.Count) - 1];

    public TensionState StateOf(int tension) =>
        tension >= Punitive ? TensionState.Punitive : tension >= Hostile ? TensionState.Hostile : tension >= Uneasy ? TensionState.Uneasy : TensionState.Calm;
}

/// <summary>A raid the Authority has announced: it comes when the warning runs out (or earlier, if the cult invites it).</summary>
public sealed record ScheduledRaid(int Id, ulong Seed, DateTimeOffset WarningEndsAtUtc);
