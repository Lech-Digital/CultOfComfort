using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Combat;

/// <summary>A village building or tower that raiders can smash during the Night.</summary>
public sealed class Structure
{
    internal Structure(int id, GridPosition position, int maxHealth, int health, System.Collections.Generic.IReadOnlyList<GridPosition>? tiles = null, bool barrier = false,
        bool decoy = false)
    {
        IsDecoy = decoy;
        IsBarrier = barrier;
        Id = id;
        Position = position;
        Tiles = tiles ?? new[] { position };
        MaxHealth = maxHealth;
        Health = health;
    }

    public int Id { get; }
    public GridPosition Position { get; }

    /// <summary>Every tile it covers (one for a hut, eight for a peat trench).</summary>
    public System.Collections.Generic.IReadOnlyList<GridPosition> Tiles { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool IsWrecked => Health <= 0;

    /// <summary>A fence, wall or gate: hacked through when it's in the way; a gap once wrecked.</summary>
    public bool IsBarrier { get; }

    /// <summary>A decoy duck: irresistible to anyone who smashes things.</summary>
    public bool IsDecoy { get; }

    /// <param name="mayWreck">False once the first Night's one wreck is spent: it stops at 1.</param>
    /// <returns>True when this hit wrecked it.</returns>
    internal bool TakeHit(int damage, bool mayWreck = true)
    {
        if (IsWrecked)
        {
            return false;
        }

        Health = System.Math.Max(mayWreck ? 0 : 1, Health - damage);
        return IsWrecked;
    }

    /// <summary>The umbrellas' patching (spec 2026-11-17 fun first, C2): never above its full health, never a wreck.</summary>
    internal void Mend(int amount)
    {
        if (!IsWrecked)
        {
            Health = System.Math.Min(MaxHealth, Health + amount);
        }
    }
}
