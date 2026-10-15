using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

public enum PlayerActionType
{
    OpenSluiceGate,
    MoveLeader,
    PhantomVoice,
    HotStones,

    /// <summary>Relic: the Finger of God. Target: a tile; the raider nearest it is blinded where it stands.</summary>
    LaserPointer,

    /// <summary>Relic: light without fire. Raiders near the Prophet are dazzled while it burns.</summary>
    DynamoTorch,

    /// <summary>Relic: sun-fire. Target: a tile; raiders there catch fire. Only while the sun is up (the first wave).</summary>
    SunFire,
}

/// <summary>The relics from the boot the Prophet has worked out how to use (GDD 05: The Boot).</summary>
[System.Flags]
public enum Relic
{
    None = 0,
    LaserPointer = 1,
    DynamoTorch = 2,
    Magnifier = 4,
}

/// <summary>Which relics he can use at each Hearth level (the village grows; he finds things in the car).</summary>
public static class RelicBook
{
    public static Relic ForHearth(int hearthLevel) => hearthLevel switch
    {
        <= 1 => Relic.None,
        2 => Relic.LaserPointer | Relic.DynamoTorch,
        _ => Relic.LaserPointer | Relic.DynamoTorch | Relic.Magnifier,
    };

    public static Relic For(PlayerActionType action) => action switch
    {
        PlayerActionType.LaserPointer => Relic.LaserPointer,
        PlayerActionType.DynamoTorch => Relic.DynamoTorch,
        PlayerActionType.SunFire => Relic.Magnifier,
        _ => Relic.None,
    };
}

public enum BattleOutcome
{
    Won,
    Lost,
}

/// <summary>One recorded player action. The input log is all the server needs to replay a Night.</summary>
/// <param name="Target">Tile for <see cref="PlayerActionType.MoveLeader"/> and <see cref="PlayerActionType.HotStones"/>; ignored otherwise.</param>
public readonly record struct PlayerInput(int Tick, PlayerActionType Type, GridPosition Target = default);

/// <param name="ExtraSluiceCharges">Open Sluice charges on top of the balance's (the village's Cisterns).</param>
public sealed record BattleSetup(
    BattleLayout Layout,
    ulong Seed,
    int Budget,
    int WaveCount,
    IReadOnlyList<PlayerInput> Inputs,
    BalanceTable? Balance = null,
    int ExtraSluiceCharges = 0)
{
    /// <summary>Who comes (null: the classic demo mix, so old replays and the demo hash stay as they were).</summary>
    public Waves.RaidRoster? Roster { get; init; }

    /// <summary>The relics he can use tonight (none: old replays and the demo hash stay as they were).</summary>
    public Relic Relics { get; init; }

    /// <summary>
    /// The relics fire themselves when they're ready and worth it (owner 2026-10-15: "laser should act automatically").
    /// Off for Nights planned before, so their replays stay as they were.
    /// </summary>
    public bool AutoRelics { get; init; }

    /// <summary>Cultists out defending: one post each (beside their hut). None for Nights planned before.</summary>
    public IReadOnlyList<GridPosition> Defenders { get; init; } = System.Array.Empty<GridPosition>();
}

public sealed record BattleResult(
    BattleOutcome Outcome,
    int Stars,
    int HearthHealth,
    int HearthMaxHealth,
    int TicksElapsed,
    int EnemiesDefeated,
    int EnemiesLeaked,
    ulong StateHash)
{
    public string StateHashHex => StateHash.ToString("x16");
}

public abstract record BattleEvent(int Tick);

public sealed record WaveStarted(int Tick, int Wave) : BattleEvent(Tick);

public sealed record EnemySpawned(int Tick, int EnemyId, EnemyType Type, int Wave) : BattleEvent(Tick);

public sealed record TowerFired(int Tick, int TowerId, int TargetId, Fix Damage, bool Critical) : BattleEvent(Tick);

public sealed record EnemyDefeated(int Tick, int EnemyId, EnemyType Type) : BattleEvent(Tick);

public sealed record HearthDamaged(int Tick, int EnemyId, EnemyType Type, int Damage, int HearthHealthRemaining) : BattleEvent(Tick);

public sealed record InterventionUsed(int Tick, PlayerActionType Type, int EnemiesAffected) : BattleEvent(Tick);

public sealed record InputRejected(int Tick, PlayerActionType Type, string Reason) : BattleEvent(Tick);

public sealed record LeaderMoveOrdered(int Tick, GridPosition Target) : BattleEvent(Tick);

public sealed record LeaderAttacked(int Tick, int TargetId, Fix Damage) : BattleEvent(Tick);

/// <summary>An enemy stopped to fight the Prophet and landed a hit.</summary>
public sealed record StructureHit(int Tick, int StructureId, int EnemyId, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record StructureWrecked(int Tick, int StructureId) : BattleEvent(Tick);

public sealed record LeaderHit(int Tick, int EnemyId, EnemyType Type, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record LeaderSwooned(int Tick) : BattleEvent(Tick);

public sealed record LeaderRevived(int Tick, int Health) : BattleEvent(Tick);

public sealed record HotStonesThrown(int Tick, GridPosition Target, int EnemiesAffected) : BattleEvent(Tick);

/// <summary>A relic went off: the laser at <paramref name="TargetId"/> (−1: none), the torch lit, the glass focused on <paramref name="Target"/>.</summary>
public sealed record RelicUsed(int Tick, PlayerActionType Type, GridPosition Target, int TargetId, int EnemiesAffected) : BattleEvent(Tick);

public sealed record DefenderAttacked(int Tick, int DefenderId, int EnemyId, Fix Damage) : BattleEvent(Tick);

public sealed record DefenderHit(int Tick, int DefenderId, int EnemyId, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record DefenderDowned(int Tick, int DefenderId) : BattleEvent(Tick);

public sealed record DefenderBack(int Tick, int DefenderId) : BattleEvent(Tick);

public sealed record BattleEnded(int Tick, BattleOutcome Outcome, int Stars) : BattleEvent(Tick);
