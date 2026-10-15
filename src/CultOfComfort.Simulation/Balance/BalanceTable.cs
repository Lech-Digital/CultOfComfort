using System.Collections.Generic;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Balance;

public enum EnemyType
{
    Levy,
    ManAtArms,

    /// <summary>Friar Odo's flock. Light, quick, and throws hymnals at heretics from range.</summary>
    Zealot,

    /// <summary>Hearth 1: a goose. Fast, furious, goes for the Prophet's ankles.</summary>
    Goose,

    /// <summary>Hearth 1: a goat. Butts every fence and wall it sees.</summary>
    Goat,

    /// <summary>Hearth 1: a wild boar. Tough, quick, rude.</summary>
    Boar,

    /// <summary>Hearth 1: a nosy peasant. Won't fight anyone; just pokes at your buildings to see what they do.</summary>
    Peasant,

    /// <summary>Hearth 3: a priest. Slow; his blessing makes raiders around him harder to hurt.</summary>
    Priest,

    /// <summary>Hearth 3: Lord Wulfric himself. One per raid, in the last wave, and he is not amused.</summary>
    Lord,

    /// <summary>Hearth 3: a bishop. Very slow, very sure; blesses everyone within three tiles. Takes the laser for a miracle and flees.</summary>
    Bishop,
}

public enum TowerType
{
    SlurryChute,
    SaplingFlinger,
}

/// <param name="EngageRange">Reach: attacks the Prophet from this close (tiles). Zero means never.</param>
/// <param name="LeaderDamage">Damage per hit on the Prophet.</param>
/// <param name="LeaderAttackCooldownTicks">Ticks between hits on the Prophet.</param>
/// <param name="StructureDamage">Damage of one swing at a building or tower beside the road.</param>
/// <param name="WallBreakerPercent">Chance (from the seed) that one of these leaves the road to hack at fences and walls.</param>
/// <param name="NoticeRange">Leaves the road to chase the Prophet when he is this close to its road point (tiles). Zero means it fights from the road.</param>
public sealed record EnemyStats(
    Fix MaxHealth,
    Fix TilesPerSecond,
    Fix BluntResistance,
    int HearthDamage,
    int WaveCost,
    Fix EngageRange = default,
    int LeaderDamage = 0,
    int LeaderAttackCooldownTicks = 0,
    Fix NoticeRange = default,
    int StructureDamage = 0,
    int WallBreakerPercent = 0);

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
/// <param name="LureLeash">A chasing raider gives up and walks back to the road when he is farther than this from its road point.</param>
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
    int ReviveGraceTicks,
    Fix LureLeash = default);

/// <summary>Hot Stone Therapy: he lobs heated river stones at a tile near him. Burns, and leaves them muddied.</summary>
/// <param name="Reach">Furthest target tile from the Prophet (tiles).</param>
public sealed record HotStonesStats(int CooldownTicks, Fix Reach, Fix Radius, Fix Damage, int MuddiedTicks);

/// <param name="LaserReach">How far from him the red dot can land (tiles).</param>
/// <param name="LaserPick">How close to the dot a raider must be to be the one blinded (tiles).</param>
/// <param name="TorchRadius">The torch's dazzle around him (tiles); dazzled raiders slow to <paramref name="TorchSlow"/> and can't fight him.</param>
/// <param name="SunFireBurnPerSecond">Fire damage per second while a raider burns.</param>
public sealed record RelicStats(
    Fix LaserReach, Fix LaserPick, int LaserBlindTicks, int LaserCooldownTicks,
    Fix TorchRadius, Fix TorchSlow, int TorchLitTicks, int TorchCooldownTicks,
    Fix SunFireReach, Fix SunFireRadius, int SunFireBurnTicks, Fix SunFireBurnPerSecond, int SunFireCooldownTicks);

/// <summary>A cultist out defending (spec 2026-10-15 defenders): plain folk with a stick, not soldiers.</summary>
/// <param name="GuardRadius">How far from their post they'll go after a raider (tiles).</param>
/// <param name="DownTicks">Knocked down: this long being patched up at the hut before they're back.</param>
public sealed record DefenderStats(int MaxHealth, Fix TilesPerSecond, Fix AttackRange, Fix AttackDamage, int AttackCooldownTicks, Fix GuardRadius, int DownTicks);

/// <summary>
/// Versioned balance data. A raid pins the version it started with so replays stay valid after rebalancing.
/// </summary>
public sealed class BalanceTable
{
    public const int TicksPerSecond = 20;

    public static BalanceTable Default { get; } = new();

    public string Version { get; init; } = "2026.10.7";

    /// <summary>A priest blesses raiders this close to him (tiles), not himself.</summary>
    public Fix BlessingRadius { get; init; } = Fix.FromInt(2);

    /// <summary>A blessed raider takes this much less damage.</summary>
    public Fix BlessingReduction { get; init; } = Fix.Ratio(1, 4);

    /// <summary>A bishop's blessing reaches this far (tiles).</summary>
    public Fix BishopBlessingRadius { get; init; } = Fix.FromInt(3);

    /// <summary>A raider leaves the road for a structure it hasn't hit this many tiles away (whole tiles, diagonals count).</summary>
    public int RaidRange { get; init; } = 3;

    /// <summary>How long a raider stops to swing at a building or tower beside the road.</summary>
    public int StructureSwingTicks { get; init; } = 16;

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
            EngageRange: Fix.Ratio(1, 2),
            LeaderDamage: 3,
            LeaderAttackCooldownTicks: 20,
            NoticeRange: Fix.FromInt(2),
            StructureDamage: 15,
            WallBreakerPercent: 30),
        [EnemyType.ManAtArms] = new(
            MaxHealth: Fix.FromInt(180),
            TilesPerSecond: Fix.Ratio(6, 10),
            BluntResistance: Fix.Ratio(4, 10),
            HearthDamage: 25,
            WaveCost: 3,
            EngageRange: Fix.Ratio(1, 2),
            LeaderDamage: 10,
            LeaderAttackCooldownTicks: 24,
            NoticeRange: Fix.Ratio(3, 2),
            StructureDamage: 40,
            WallBreakerPercent: 50),
        [EnemyType.Zealot] = new(
            MaxHealth: Fix.FromInt(45),
            TilesPerSecond: Fix.Ratio(11, 10),
            BluntResistance: Fix.Zero,
            HearthDamage: 10,
            WaveCost: 2,
            EngageRange: Fix.Ratio(5, 2),
            LeaderDamage: 6,
            LeaderAttackCooldownTicks: 30,
            StructureDamage: 10),
        [EnemyType.Goose] = new(
            MaxHealth: Fix.FromInt(25), TilesPerSecond: Fix.Ratio(16, 10), BluntResistance: Fix.Zero, HearthDamage: 4, WaveCost: 1,
            EngageRange: Fix.Ratio(1, 2), LeaderDamage: 2, LeaderAttackCooldownTicks: 15, NoticeRange: Fix.FromInt(3), StructureDamage: 5),
        [EnemyType.Goat] = new(
            MaxHealth: Fix.FromInt(55), TilesPerSecond: Fix.Ratio(11, 10), BluntResistance: Fix.Zero, HearthDamage: 6, WaveCost: 1,
            EngageRange: Fix.Ratio(1, 2), LeaderDamage: 2, LeaderAttackCooldownTicks: 20, NoticeRange: Fix.FromInt(1), StructureDamage: 30,
            WallBreakerPercent: 100),
        [EnemyType.Boar] = new(
            MaxHealth: Fix.FromInt(90), TilesPerSecond: Fix.Ratio(14, 10), BluntResistance: Fix.Ratio(2, 10), HearthDamage: 12, WaveCost: 2,
            EngageRange: Fix.Ratio(1, 2), LeaderDamage: 6, LeaderAttackCooldownTicks: 18, NoticeRange: Fix.Ratio(3, 2), StructureDamage: 25,
            WallBreakerPercent: 20),
        [EnemyType.Peasant] = new(
            MaxHealth: Fix.FromInt(40), TilesPerSecond: Fix.Ratio(9, 10), BluntResistance: Fix.Zero, HearthDamage: 5, WaveCost: 1,
            StructureDamage: 8),
        [EnemyType.Priest] = new(
            MaxHealth: Fix.FromInt(160), TilesPerSecond: Fix.Ratio(6, 10), BluntResistance: Fix.Ratio(2, 10), HearthDamage: 20, WaveCost: 4,
            EngageRange: Fix.FromInt(2), LeaderDamage: 5, LeaderAttackCooldownTicks: 30, StructureDamage: 10),
        [EnemyType.Bishop] = new(
            MaxHealth: Fix.FromInt(320), TilesPerSecond: Fix.Ratio(2, 5), BluntResistance: Fix.Ratio(3, 10), HearthDamage: 30, WaveCost: 6,
            EngageRange: Fix.FromInt(2), LeaderDamage: 6, LeaderAttackCooldownTicks: 30, StructureDamage: 20),
        [EnemyType.Lord] = new(
            MaxHealth: Fix.FromInt(700), TilesPerSecond: Fix.Ratio(1, 2), BluntResistance: Fix.Ratio(4, 10), HearthDamage: 50, WaveCost: 12,
            EngageRange: Fix.Ratio(6, 10), LeaderDamage: 15, LeaderAttackCooldownTicks: 24, NoticeRange: Fix.Ratio(3, 2), StructureDamage: 80,
            WallBreakerPercent: 100),
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
        ReviveGraceTicks: 2 * TicksPerSecond,
        LureLeash: Fix.Ratio(5, 2));

    public HotStonesStats HotStones { get; init; } = new(
        CooldownTicks: 20 * TicksPerSecond,
        Reach: Fix.FromInt(4),
        Radius: Fix.Ratio(6, 5),
        Damage: Fix.FromInt(45),
        MuddiedTicks: 3 * TicksPerSecond);

    /// <summary>The relics from the boot (GDD 05). Balance 2026.10.14: strong but rare; each has its own catch.</summary>
    public DefenderStats Defender { get; init; } = new(
        MaxHealth: 30, TilesPerSecond: Fix.Ratio(8, 5), AttackRange: Fix.One, AttackDamage: Fix.FromInt(4), AttackCooldownTicks: 24,
        GuardRadius: Fix.FromInt(4), DownTicks: 15 * TicksPerSecond);

    /// <summary>The torch lights itself when this many raiders crowd him (auto relics).</summary>
    public int TorchAutoCrowd { get; init; } = 2;

    public RelicStats Relics { get; init; } = new(
        LaserReach: Fix.FromInt(7), LaserPick: Fix.Ratio(3, 2), LaserBlindTicks: 5 * TicksPerSecond / 2, LaserCooldownTicks: 10 * TicksPerSecond,
        TorchRadius: Fix.Ratio(5, 2), TorchSlow: Fix.Ratio(1, 2), TorchLitTicks: 6 * TicksPerSecond, TorchCooldownTicks: 20 * TicksPerSecond,
        SunFireReach: Fix.FromInt(4), SunFireRadius: Fix.One, SunFireBurnTicks: 4 * TicksPerSecond, SunFireBurnPerSecond: Fix.FromInt(12),
        SunFireCooldownTicks: 12 * TicksPerSecond);
}
