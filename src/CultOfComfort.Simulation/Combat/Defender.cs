using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

/// <summary>
/// A cultist out of their hut to defend the village (owner 2026-10-15: "our cultists should also defend the base"):
/// guards the ground round their post, goes for the nearest raider in reach, and walks back when it's quiet. Knocked
/// down, they're patched up at the hut and come back out. Walks the grid like the Prophet: always on a tile centre or
/// between two neighbouring ones.
/// </summary>
public sealed class Defender
{
    private readonly Queue<GridPosition> _waypoints = new();

    internal Defender(int id, GridPosition post, int maxHealth)
    {
        Id = id;
        Post = post;
        Position = post.ToVector();
        MaxHealth = maxHealth;
        Health = maxHealth;
        TargetId = -1;
    }

    public int Id { get; }

    /// <summary>Where they stand guard (beside their hut).</summary>
    public GridPosition Post { get; }

    public FixVector2 Position { get; private set; }

    public int MaxHealth { get; }

    public int Health { get; private set; }

    /// <summary>The raider they're after, or -1.</summary>
    public int TargetId { get; internal set; }

    public int AttackCooldownTicksRemaining { get; internal set; }

    /// <summary>Down (being patched up at the hut) until this tick.</summary>
    public int DownUntilTick { get; private set; }

    /// <summary>When their route was last worked out (they re-plan a few times a second, not every tick).</summary>
    internal int RoutedAtTick { get; set; } = int.MinValue / 2;

    internal GridPosition RoutedTo { get; set; }

    public bool IsDown(int tick) => Health <= 0 && tick < DownUntilTick;

    public bool IsUp => Health > 0;

    public bool IsMoving => _waypoints.Count > 0;

    internal GridPosition RouteOrigin =>
        _waypoints.Count > 0 ? _waypoints.Peek() : new GridPosition(Position.X.FloorToInt(), Position.Y.FloorToInt());

    internal void SetRoute(IReadOnlyList<GridPosition> route)
    {
        var heading = RouteOrigin;
        _waypoints.Clear();
        _waypoints.Enqueue(heading);
        for (var i = 1; i < route.Count; i++)
        {
            _waypoints.Enqueue(route[i]);
        }
    }

    internal void Stop()
    {
        var heading = RouteOrigin;
        _waypoints.Clear();
        _waypoints.Enqueue(heading); // finish the step under way, then stand
    }

    internal void Walk(Fix distance)
    {
        var remaining = distance;
        while (remaining > Fix.Zero && _waypoints.Count > 0)
        {
            var target = _waypoints.Peek().ToVector();
            var dx = target.X - Position.X;
            var dy = target.Y - Position.Y;
            var gap = dx.Abs() + dy.Abs(); // exact: one of dx/dy is always zero

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

    /// <returns>True if this hit knocked them down.</returns>
    internal bool TakeHit(int tick, int damage, int downTicks)
    {
        if (Health <= 0)
        {
            return false;
        }

        Health = System.Math.Max(0, Health - damage);
        if (Health > 0)
        {
            return false;
        }

        DownUntilTick = tick + downTicks;
        TargetId = -1;
        _waypoints.Clear();
        return true;
    }

    /// <returns>True on the tick they come back out, patched up, at their post.</returns>
    internal bool TryRevive(int tick)
    {
        if (Health > 0 || tick < DownUntilTick)
        {
            return false;
        }

        Health = MaxHealth / 2;
        Position = Post.ToVector();
        _waypoints.Clear();
        return true;
    }

    private static Fix Sign(Fix value) => value > Fix.Zero ? Fix.One : value < Fix.Zero ? -Fix.One : Fix.Zero;

    internal void AddToHash(Fnv1aHasher hasher)
    {
        hasher.Add(Id);
        hasher.Add(Position.X.Raw);
        hasher.Add(Position.Y.Raw);
        hasher.Add(Health);
        hasher.Add(TargetId);
        hasher.Add(AttackCooldownTicksRemaining);
        hasher.Add(DownUntilTick);
        hasher.Add(_waypoints.Count);
    }
}
