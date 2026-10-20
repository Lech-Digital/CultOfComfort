using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Combat;

public sealed class Tower
{
    internal Tower(int id, TowerType type, GridPosition position, int level = 1, bool manned = false)
    {
        Id = id;
        Type = type;
        Position = position;
        Level = System.Math.Max(1, level);
        Manned = manned;
    }

    public int Id { get; }
    public TowerType Type { get; }
    public GridPosition Position { get; }

    /// <summary>1 to 3.</summary>
    public int Level { get; }

    /// <summary>A warden works it: it fires faster.</summary>
    public bool Manned { get; }
    public int CooldownTicksRemaining { get; internal set; }
}
