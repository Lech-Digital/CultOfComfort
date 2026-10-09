using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Grid;

public readonly record struct GridPosition(int X, int Y)
{
    public FixVector2 ToVector() => new(Fix.FromInt(X), Fix.FromInt(Y));
}
