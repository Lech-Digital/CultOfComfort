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
    int ExtraSluiceCharges = 0);

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
public sealed record LeaderHit(int Tick, int EnemyId, EnemyType Type, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record LeaderSwooned(int Tick) : BattleEvent(Tick);

public sealed record LeaderRevived(int Tick, int Health) : BattleEvent(Tick);

public sealed record HotStonesThrown(int Tick, GridPosition Target, int EnemiesAffected) : BattleEvent(Tick);

public sealed record BattleEnded(int Tick, BattleOutcome Outcome, int Stars) : BattleEvent(Tick);
