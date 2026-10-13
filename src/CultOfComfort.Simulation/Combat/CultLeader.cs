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

    internal CultLeader(GridPosition start, int maxHealth)
    {
        Position = start.ToVector();
        Destination = start;
        MaxHealth = maxHealth;
        Health = maxHealth;
        LastHitTick = int.MinValue / 2;
    }

    public FixVector2 Position { get; private set; }

    /// <summary>The tile he is walking to (or standing on, when idle).</summary>
    public GridPosition Destination { get; private set; }

    public bool IsMoving => _waypoints.Count > 0;
    public int AttackCooldownTicksRemaining { get; internal set; }
    public int VoiceReadyAtTick { get; internal set; }
    public int HotStonesReadyAtTick { get; internal set; }
    public int LaserReadyAtTick { get; internal set; }
    public int TorchReadyAtTick { get; internal set; }

    /// <summary>The dynamo torch shines until this tick (someone stopped cranking).</summary>
    public int TorchLitUntilTick { get; internal set; }

    public int SunFireReadyAtTick { get; internal set; }

    public bool IsTorchLit(int tick) => tick < TorchLitUntilTick;
    public int MaxHealth { get; }
    public int Health { get; private set; }

    /// <summary>While swooning he can't walk, fight or use skills, and enemies ignore him.</summary>
    public int SwoonedUntilTick { get; private set; }
    public int LastHitTick { get; private set; }

    public bool IsSwooning(int tick) => tick < SwoonedUntilTick;

    /// <summary>Enemies won't engage him: swooning, or just back on his feet.</summary>
    public bool IsIgnoredByEnemies(int tick, int graceTicks) => tick < SwoonedUntilTick + graceTicks;

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

    /// <returns>True if this hit knocked him out.</returns>
    internal bool TakeHit(int tick, int damage, int swoonTicks)
    {
        LastHitTick = tick;
        Health = System.Math.Max(0, Health - damage);
        if (Health > 0)
        {
            return false;
        }

        // He drops where he stands, possibly between two tiles; that is still axis-aligned, so later routes stay valid.
        SwoonedUntilTick = tick + swoonTicks;
        _waypoints.Clear();
        Destination = RouteOrigin;
        return true;
    }

    /// <returns>True on the tick he gets back up.</returns>
    internal bool TryRevive(int tick)
    {
        if (Health > 0 || tick < SwoonedUntilTick)
        {
            return false;
        }

        Health = MaxHealth / 2;
        return true;
    }

    internal void Regenerate(int tick, int delayTicks, int intervalTicks)
    {
        if (Health > 0 && Health < MaxHealth && tick - LastHitTick >= delayTicks && tick % intervalTicks == 0)
        {
            Health++;
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
        hasher.Add(HotStonesReadyAtTick);
        hasher.Add(Health);
        hasher.Add(SwoonedUntilTick);
    }

    /// <summary>Only for Nights with relics, so older replays keep their hashes.</summary>
    internal void AddRelicsToHash(Fnv1aHasher hasher)
    {
        hasher.Add(LaserReadyAtTick);
        hasher.Add(TorchReadyAtTick);
        hasher.Add(TorchLitUntilTick);
        hasher.Add(SunFireReadyAtTick);
    }
}
