using System;
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

    public bool IsActive => State == EnemyState.Marching;

    public bool IsMuddied(int tick) => tick < MuddiedUntilTick;

    public bool IsSlowed(int tick) => tick < SlowedUntilTick;

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

    internal void ReachHearth() => State = EnemyState.ReachedHearth;
}
