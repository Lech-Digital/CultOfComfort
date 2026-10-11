using System;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>A placed building. Level 0 = still being built. Producers carry a lazily evaluated output buffer.</summary>
public sealed class Building
{
    internal Building(int id, BuildingType type, GridPosition position, int level, DateTimeOffset asOfUtc)
    {
        Id = id;
        Type = type;
        Position = position;
        Level = level;
        AsOfUtc = asOfUtc;
    }

    public int Id { get; }
    public BuildingType Type { get; }
    public GridPosition Position { get; }
    public int Level { get; internal set; }
    public DateTimeOffset? UpgradeEndsAtUtc { get; internal set; }
    public bool IsUpgrading => UpgradeEndsAtUtc != null;

    /// <summary>Output waiting to be collected, in thousandths of a unit, as of <see cref="AsOfUtc"/>.</summary>
    internal long BufferMilli { get; set; }

    internal DateTimeOffset AsOfUtc { get; set; }
}
