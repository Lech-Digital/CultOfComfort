using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

/// <summary>
/// The time traveller. Walks the grid on tap-to-move orders; his position is always a tile centre or
/// between two adjacent tile centres, so every movement segment is axis-aligned and needs no square root.
/// </summary>
public sealed class CultLeader
{
    private readonly Queue<GridPosition> _waypoints = new();

    internal CultLeader(GridPosition start)
    {
        Position = start.ToVector();
        Destination = start;
    }

    public FixVector2 Position { get; private set; }

    /// <summary>The tile he is walking to (or standing on, when idle).</summary>
    public GridPosition Destination { get; private set; }

    public bool IsMoving => _waypoints.Count > 0;
    public int AttackCooldownTicksRemaining { get; internal set; }
    public int VoiceReadyAtTick { get; internal set; }

    /// <summary>
    /// The tile from which a new route should start: the waypoint he is currently heading to,
    /// or the tile he stands on. He finishes the current one-tile step before turning.
    /// </summary>
    internal GridPosition RouteOrigin =>
        _waypoints.Count > 0 ? _waypoints.Peek() : new GridPosition(Position.X.FloorToInt(), Position.Y.FloorToInt());

    internal void SetRoute(IReadOnlyList<GridPosition> route, GridPosition destination)
    {
        var heading = RouteOrigin;
        _waypoints.Clear();
        _waypoints.Enqueue(heading);
        for (var i = 1; i < route.Count; i++)
        {
            _waypoints.Enqueue(route[i]);
        }

        Destination = destination;
    }

    internal void Walk(Fix distance)
    {
        var remaining = distance;
        while (remaining > Fix.Zero && _waypoints.Count > 0)
        {
            var target = _waypoints.Peek().ToVector();
            var dx = target.X - Position.X;
            var dy = target.Y - Position.Y;
            var gap = dx.Abs() + dy.Abs(); // Exact: one of dx/dy is always zero.

            if (gap <= remaining)
            {
                Position = target;
                remaining -= gap;
                _waypoints.Dequeue();
                continue;
            }

            Position = new FixVector2(Position.X + Sign(dx) * remaining, Position.Y + Sign(dy) * remaining);
            remaining = Fix.Zero;
        }
    }

    private static Fix Sign(Fix value) => value > Fix.Zero ? Fix.One : value < Fix.Zero ? -Fix.One : Fix.Zero;

    internal void AddToHash(Fnv1aHasher hasher)
    {
        hasher.Add(Position.X.Raw);
        hasher.Add(Position.Y.Raw);
        hasher.Add(_waypoints.Count);
        hasher.Add(AttackCooldownTicksRemaining);
        hasher.Add(VoiceReadyAtTick);
    }
}
