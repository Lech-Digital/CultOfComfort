using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Grid;

public readonly record struct PlacedTower(TowerType Type, GridPosition Position);

/// <summary>Immutable snapshot of a base as it stands when the Night starts (the "locked layout").</summary>
public sealed class BattleLayout
{
    private readonly HashSet<GridPosition> _blocked;

    public BattleLayout(
        int width,
        int height,
        GridPosition spawn,
        GridPosition hearth,
        IEnumerable<PlacedTower> towers,
        IEnumerable<GridPosition> obstacles)
    {
        Width = width;
        Height = height;
        Spawn = spawn;
        Hearth = hearth;
        Towers = towers.ToList();
        Obstacles = obstacles.ToList();
        _blocked = new HashSet<GridPosition>(Obstacles.Concat(Towers.Select(t => t.Position)));
    }

    public int Width { get; }
    public int Height { get; }
    public GridPosition Spawn { get; }
    public GridPosition Hearth { get; }
    public IReadOnlyList<PlacedTower> Towers { get; }
    public IReadOnlyList<GridPosition> Obstacles { get; }

    public bool IsBlocked(GridPosition position) => _blocked.Contains(position);

    public IReadOnlyList<GridPosition> ResolveEnemyPath() =>
        Pathfinder.FindPath(Width, Height, Spawn, Hearth, IsBlocked)
        ?? throw new InvalidLayoutException("Enemies have no path to the Hearth. Even medieval soldiers need a way in.");

    /// <exception cref="InvalidLayoutException">The layout cannot be used for a battle.</exception>
    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidLayoutException("Grid dimensions must be positive.");
        }

        var occupied = new HashSet<GridPosition>();
        foreach (var position in new[] { Spawn, Hearth }.Concat(Obstacles).Concat(Towers.Select(t => t.Position)))
        {
            if (position.X < 0 || position.Y < 0 || position.X >= Width || position.Y >= Height)
            {
                throw new InvalidLayoutException($"Tile {position} is outside the {Width}x{Height} grid.");
            }

            if (!occupied.Add(position))
            {
                throw new InvalidLayoutException($"Tile {position} is occupied twice.");
            }
        }

        ResolveEnemyPath();
    }

    /// <summary>Order-sensitive hash; the server compares it with its own BaseState before a Night starts.</summary>
    public ulong ComputeHash()
    {
        var hasher = new Fnv1aHasher();
        hasher.Add(Width);
        hasher.Add(Height);
        AddPosition(hasher, Spawn);
        AddPosition(hasher, Hearth);
        foreach (var tower in Towers)
        {
            hasher.Add((int)tower.Type);
            AddPosition(hasher, tower.Position);
        }

        foreach (var obstacle in Obstacles)
        {
            AddPosition(hasher, obstacle);
        }

        return hasher.Value;
    }

    private static void AddPosition(Fnv1aHasher hasher, GridPosition position)
    {
        hasher.Add(position.X);
        hasher.Add(position.Y);
    }
}

public sealed class InvalidLayoutException(string message) : Exception(message);
