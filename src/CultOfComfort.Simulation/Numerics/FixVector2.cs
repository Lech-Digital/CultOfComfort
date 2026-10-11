namespace CultOfComfort.Simulation.Numerics;

/// <summary>Position in tile units; integer coordinates are tile centres.</summary>
public readonly record struct FixVector2(Fix X, Fix Y)
{
    /// <summary>Squared distance — comparing against range² avoids a square root entirely.</summary>
    public Fix DistanceSquaredTo(FixVector2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public static FixVector2 operator +(FixVector2 a, FixVector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static FixVector2 operator -(FixVector2 a, FixVector2 b) => new(a.X - b.X, a.Y - b.Y);
}
