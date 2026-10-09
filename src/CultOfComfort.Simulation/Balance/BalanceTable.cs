using System.Collections.Generic;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Balance;

public enum EnemyType
{
    Levy,
    ManAtArms,
}

public enum TowerType
{
    SlurryChute,
    SaplingFlinger,
}

public sealed record EnemyStats(Fix MaxHealth, Fix TilesPerSecond, Fix BluntResistance, int HearthDamage, int WaveCost);

/// <param name="SplashRadius">Zero means single-target.</param>
/// <param name="SlowMultiplier">Speed multiplier while slowed; One means no slow.</param>
/// <param name="MuddiedCritMultiplier">Damage multiplier against Muddied targets; One means no crit.</param>
public sealed record TowerStats(
    Fix Range,
    int CooldownTicks,
    Fix Damage,
    Fix SplashRadius,
    Fix SlowMultiplier,
    int SlowTicks,
    int MuddiedTicks,
    Fix MuddiedCritMultiplier);

public sealed record SluiceGateStats(int Charges, int CooldownTicks, Fix PushbackTiles, int MuddiedTicks);

/// <summary>
/// Versioned balance data. A raid pins the version it started with so replays stay valid after rebalancing.
/// </summary>
public sealed class BalanceTable
{
    public const int TicksPerSecond = 20;

    public static BalanceTable Default { get; } = new();

    public string Version { get; init; } = "2026.10.1";

    public int HearthMaxHealth { get; init; } = 100;

    /// <summary>Safety cap: survive this long and dawn breaks regardless.</summary>
    public int MaxTicks { get; init; } = 5 * 60 * TicksPerSecond;

    public IReadOnlyDictionary<EnemyType, EnemyStats> Enemies { get; init; } = new Dictionary<EnemyType, EnemyStats>
    {
        [EnemyType.Levy] = new(
            MaxHealth: Fix.FromInt(60),
            TilesPerSecond: Fix.FromInt(1),
            BluntResistance: Fix.Zero,
            HearthDamage: 10,
            WaveCost: 1),
        [EnemyType.ManAtArms] = new(
            MaxHealth: Fix.FromInt(180),
            TilesPerSecond: Fix.Ratio(6, 10),
            BluntResistance: Fix.Ratio(4, 10),
            HearthDamage: 25,
            WaveCost: 3),
    };

    public IReadOnlyDictionary<TowerType, TowerStats> Towers { get; init; } = new Dictionary<TowerType, TowerStats>
    {
        [TowerType.SlurryChute] = new(
            Range: Fix.Ratio(5, 2),
            CooldownTicks: 2 * TicksPerSecond,
            Damage: Fix.FromInt(4),
            SplashRadius: Fix.FromInt(1),
            SlowMultiplier: Fix.Ratio(6, 10),
            SlowTicks: 3 * TicksPerSecond,
            MuddiedTicks: 3 * TicksPerSecond,
            MuddiedCritMultiplier: Fix.One),
        [TowerType.SaplingFlinger] = new(
            Range: Fix.Ratio(7, 2),
            CooldownTicks: 24,
            Damage: Fix.FromInt(22),
            SplashRadius: Fix.Zero,
            SlowMultiplier: Fix.One,
            SlowTicks: 0,
            MuddiedTicks: 0,
            MuddiedCritMultiplier: Fix.Ratio(7, 4)),
    };

    public SluiceGateStats SluiceGate { get; init; } = new(
        Charges: 2,
        CooldownTicks: 10 * TicksPerSecond,
        PushbackTiles: Fix.FromInt(3),
        MuddiedTicks: 4 * TicksPerSecond);
}
