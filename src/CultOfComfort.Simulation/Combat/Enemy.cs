using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

public enum EnemyState
{
    Marching,
    Defeated,
    ReachedHearth,
}

public sealed class Enemy
{
    internal Enemy(int id, EnemyType type, int wave, Fix maxHealth)
    {
        Id = id;
        Type = type;
        Wave = wave;
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public int Id { get; }
    public EnemyType Type { get; }
    public int Wave { get; }
    public Fix MaxHealth { get; }
    public Fix Health { get; private set; }
    public Fix DistanceAlongPath { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Marching;
    public int MuddiedUntilTick { get; private set; }
    public int SlowedUntilTick { get; private set; }
    public Fix SlowMultiplier { get; private set; } = Fix.One;
    public int FrightenedUntilTick { get; private set; }

    /// <summary>How far it has stepped off the road (tiles) to chase the Prophet; zero while on the road.</summary>
    public FixVector2 Offset { get; private set; }

    public bool IsOffRoad => Offset != default;

    /// <summary>True while it has stopped on the road to fight the Prophet.</summary>
    public bool IsFightingLeader { get; internal set; }
    public int LeaderAttackCooldownTicksRemaining { get; internal set; }

    /// <summary>Ticks left in a swing at a structure (0 = not smashing).</summary>
    public int SmashTicksRemaining { get; internal set; }

    /// <summary>The structure being smashed, or −1.</summary>
    public int SmashTargetId { get; internal set; } = -1;

    internal HashSet<int> StructuresHit { get; } = new();

    /// <summary>Holds one of the Prophet's engaged slots this tick (chasing or fighting him).</summary>
    public bool HasLeaderSlot { get; internal set; }

    public bool IsActive => State == EnemyState.Marching;

    public bool IsMuddied(int tick) => tick < MuddiedUntilTick;

    public bool IsSlowed(int tick) => tick < SlowedUntilTick;

    /// <summary>Frightened enemies walk back the way they came.</summary>
    public bool IsFrightened(int tick) => tick < FrightenedUntilTick;

    /// <returns>True if this damage defeated the enemy.</returns>
    internal bool TakeDamage(Fix amount)
    {
        Health -= amount;
        if (Health > Fix.Zero)
        {
            return false;
        }

        Health = Fix.Zero;
        State = EnemyState.Defeated;
        return true;
    }

    internal void Advance(Fix distance) => DistanceAlongPath += distance;

    internal void PushBack(Fix distance) => DistanceAlongPath = Fix.Max(Fix.Zero, DistanceAlongPath - distance);

    internal void ApplyMud(int untilTick) => MuddiedUntilTick = Math.Max(MuddiedUntilTick, untilTick);

    internal void ApplySlow(Fix multiplier, int untilTick)
    {
        SlowMultiplier = multiplier;
        SlowedUntilTick = Math.Max(SlowedUntilTick, untilTick);
    }

    internal void Frighten(int untilTick) => FrightenedUntilTick = Math.Max(FrightenedUntilTick, untilTick);

    /// <summary>Moves the offset towards <paramref name="target"/>, each axis by at most <paramref name="step"/> (no square roots).</summary>
    internal void StepOffsetToward(FixVector2 target, Fix step) =>
        Offset = new FixVector2(Approach(Offset.X, target.X, step), Approach(Offset.Y, target.Y, step));

    internal void ReachHearth() => State = EnemyState.ReachedHearth;

    private static Fix Approach(Fix from, Fix to, Fix step)
    {
        var delta = to - from;
        if (delta.Abs() <= step)
        {
            return to;
        }

        return delta > Fix.Zero ? from + step : from - step;
    }
}
