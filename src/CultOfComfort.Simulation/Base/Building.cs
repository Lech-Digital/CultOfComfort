using System;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>A placed building. Level 0 = still being built. Producers carry a lazily evaluated output buffer.</summary>
public sealed class Building
{
    internal Building(int id, BuildingType type, GridPosition position, int level, DateTimeOffset asOfUtc,
        System.Collections.Generic.IReadOnlyList<GridPosition>? tiles = null, bool rotated = false)
    {
        Id = id;
        Type = type;
        Position = position;
        Tiles = tiles ?? new[] { position };
        Rotated = rotated;
        Level = level;
        AsOfUtc = asOfUtc;
    }

    public int Id { get; }
    public BuildingType Type { get; }
    /// <summary>The anchor: the footprint's lowest x and y.</summary>
    public GridPosition Position { get; }

    /// <summary>Every tile it covers.</summary>
    public System.Collections.Generic.IReadOnlyList<GridPosition> Tiles { get; }

    /// <summary>Turned a quarter: width and depth swap (and a fence runs north–south).</summary>
    public bool Rotated { get; }
    public int Level { get; internal set; }
    public DateTimeOffset? UpgradeEndsAtUtc { get; internal set; }
    public bool IsUpgrading => UpgradeEndsAtUtc != null;

    /// <summary>The crew the player chose (0 to the level's slots); null: a full crew, whatever the level.</summary>
    public int? CrewWanted { get; internal set; }

    /// <summary>Smashed in a Night: produces nothing and gives no bonuses until repaired. Still in the way.</summary>
    public bool Wrecked { get; internal set; }

    /// <summary>Output waiting to be collected, in thousandths of a unit, as of <see cref="AsOfUtc"/>.</summary>
    internal long BufferMilli { get; set; }

    internal DateTimeOffset AsOfUtc { get; set; }
}
