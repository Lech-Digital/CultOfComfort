using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

public enum InterventionType
{
    OpenSluiceGate,
}

public enum BattleOutcome
{
    Won,
    Lost,
}

/// <summary>One recorded player action. The input log is all the server needs to replay a Night.</summary>
public readonly record struct PlayerInput(int Tick, InterventionType Type);

public sealed record BattleSetup(
    BattleLayout Layout,
    ulong Seed,
    int Budget,
    int WaveCount,
    IReadOnlyList<PlayerInput> Inputs,
    BalanceTable? Balance = null);

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

public sealed record InterventionUsed(int Tick, InterventionType Type, int EnemiesAffected) : BattleEvent(Tick);

public sealed record InputRejected(int Tick, InterventionType Type, string Reason) : BattleEvent(Tick);

public sealed record BattleEnded(int Tick, BattleOutcome Outcome, int Stars) : BattleEvent(Tick);
