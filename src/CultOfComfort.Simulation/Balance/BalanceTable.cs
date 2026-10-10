using System.Collections.Generic;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Balance;

public enum EnemyType
{
    Levy,
    ManAtArms,

    /// <summary>Friar Odo's flock. Light, quick, and throws hymnals at heretics from range.</summary>
    Zealot,
}

public enum TowerType
{
    SlurryChute,
    SaplingFlinger,
}

/// <param name="EngageRange">Stops marching to fight the Prophet when he is this close (tiles). Zero means never.</param>
/// <param name="LeaderDamage">Damage per hit on the Prophet.</param>
/// <param name="LeaderAttackCooldownTicks">Ticks between hits on the Prophet.</param>
public sealed record EnemyStats(
    Fix MaxHealth,
    Fix TilesPerSecond,
    Fix BluntResistance,
    int HearthDamage,
    int WaveCost,
    Fix EngageRange = default,
    int LeaderDamage = 0,
    int LeaderAttackCooldownTicks = 0);

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

/// <param name="VoiceRadius">Phantom Voice (tin-can megaphone) reach in tiles.</param>
/// <param name="VoiceFleeTicks">How long frightened enemies walk backwards.</param>
/// <param name="MaxEngaged">How many enemies can fight him at once; the rest walk past.</param>
/// <param name="SwoonTicks">How long he lies on the ground after his health runs out.</param>
/// <param name="RegenDelayTicks">Ticks without being hit before he starts recovering.</param>
/// <param name="RegenIntervalTicks">One health point back every this many ticks.</param>
/// <param name="ReviveGraceTicks">After getting up, enemies leave him alone this long, so he can walk out of trouble.</param>
public sealed record LeaderStats(
    Fix TilesPerSecond,
    Fix AttackRange,
    Fix AttackDamage,
    int AttackCooldownTicks,
    Fix VoiceRadius,
    int VoiceFleeTicks,
    int VoiceCooldownTicks,
    int MaxHealth,
    int MaxEngaged,
    int SwoonTicks,
    int RegenDelayTicks,
    int RegenIntervalTicks,
    int ReviveGraceTicks);

/// <summary>Hot Stone Therapy: he lobs heated river stones at a tile near him. Burns, and leaves them muddied.</summary>
/// <param name="Reach">Furthest target tile from the Prophet (tiles).</param>
public sealed record HotStonesStats(int CooldownTicks, Fix Reach, Fix Radius, Fix Damage, int MuddiedTicks);

/// <summary>
/// Versioned balance data. A raid pins the version it started with so replays stay valid after rebalancing.
/// </summary>
public sealed class BalanceTable
{
    public const int TicksPerSecond = 20;

    public static BalanceTable Default { get; } = new();

    public string Version { get; init; } = "2026.10.2";

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
            WaveCost: 1,
            EngageRange: Fix.Ratio(3, 4),
            LeaderDamage: 3,
            LeaderAttackCooldownTicks: 20),
        [EnemyType.ManAtArms] = new(
            MaxHealth: Fix.FromInt(180),
            TilesPerSecond: Fix.Ratio(6, 10),
            BluntResistance: Fix.Ratio(4, 10),
            HearthDamage: 25,
            WaveCost: 3,
            EngageRange: Fix.One,
            LeaderDamage: 10,
            LeaderAttackCooldownTicks: 24),
        [EnemyType.Zealot] = new(
            MaxHealth: Fix.FromInt(45),
            TilesPerSecond: Fix.Ratio(11, 10),
            BluntResistance: Fix.Zero,
            HearthDamage: 10,
            WaveCost: 2,
            EngageRange: Fix.Ratio(5, 2),
            LeaderDamage: 6,
            LeaderAttackCooldownTicks: 30),
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

    public LeaderStats Leader { get; init; } = new(
        TilesPerSecond: Fix.FromInt(3),
        AttackRange: Fix.Ratio(3, 2),
        AttackDamage: Fix.FromInt(14),
        AttackCooldownTicks: 16,
        VoiceRadius: Fix.Ratio(5, 2),
        VoiceFleeTicks: 2 * TicksPerSecond,
        VoiceCooldownTicks: 15 * TicksPerSecond,
        MaxHealth: 120,
        MaxEngaged: 3,
        SwoonTicks: 10 * TicksPerSecond,
        RegenDelayTicks: 3 * TicksPerSecond,
        RegenIntervalTicks: 10,
        ReviveGraceTicks: 2 * TicksPerSecond);

    public HotStonesStats HotStones { get; init; } = new(
        CooldownTicks: 20 * TicksPerSecond,
        Reach: Fix.FromInt(4),
        Radius: Fix.Ratio(6, 5),
        Damage: Fix.FromInt(45),
        MuddiedTicks: 3 * TicksPerSecond);
}
