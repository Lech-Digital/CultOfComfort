using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Combat;

/// <summary>A village building or tower that raiders can smash during the Night.</summary>
public sealed class Structure
{
    internal Structure(int id, GridPosition position, int maxHealth, int health)
    {
        Id = id;
        Position = position;
        MaxHealth = maxHealth;
        Health = health;
    }

    public int Id { get; }
    public GridPosition Position { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool IsWrecked => Health <= 0;

    /// <returns>True when this hit wrecked it.</returns>
    internal bool TakeHit(int damage)
    {
        if (IsWrecked)
        {
            return false;
        }

        Health = System.Math.Max(0, Health - damage);
        return IsWrecked;
    }
}
