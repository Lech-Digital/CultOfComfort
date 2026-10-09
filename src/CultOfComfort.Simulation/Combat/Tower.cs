using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Combat;

public sealed class Tower
{
    internal Tower(int id, TowerType type, GridPosition position)
    {
        Id = id;
        Type = type;
        Position = position;
    }

    public int Id { get; }
    public TowerType Type { get; }
    public GridPosition Position { get; }
    public int CooldownTicksRemaining { get; internal set; }
}
