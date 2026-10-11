using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Grid;

public readonly record struct PlacedTower(TowerType Type, GridPosition Position);

/// <summary>A village building or tower raiders can smash. It sits on tower or obstacle tiles; it adds no blocking itself.</summary>
public readonly record struct PlacedStructure(int Id, GridPosition Position, int MaxHealth, int Health)
{
    /// <summary>Every tile of its footprint (null = just <see cref="Position"/>).</summary>
    public IReadOnlyList<GridPosition>? Tiles { get; init; }
}

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
        IEnumerable<GridPosition> obstacles,
        GridPosition? leaderStart = null,
        IEnumerable<PlacedStructure>? structures = null,
        IReadOnlyList<GridPosition>? fixedPath = null)
    {
        FixedPath = fixedPath;
        Width = width;
        Height = height;
        Spawn = spawn;
        Hearth = hearth;
        Towers = towers.ToList();
        Obstacles = obstacles.ToList();
        Structures = structures?.ToList() ?? new List<PlacedStructure>();
        _blocked = new HashSet<GridPosition>(Obstacles.Concat(Towers.Select(t => t.Position)));
        LeaderStart = leaderStart ?? FindDefaultLeaderStart();
    }

    public int Width { get; }
    public int Height { get; }
    public GridPosition Spawn { get; }
    public GridPosition Hearth { get; }
    public IReadOnlyList<PlacedTower> Towers { get; }
    public IReadOnlyList<GridPosition> Obstacles { get; }

    /// <summary>What raiders can smash during the Night (empty for layouts without a village).</summary>
    public IReadOnlyList<PlacedStructure> Structures { get; }

    /// <summary>The village's road, which nothing may move (null: raiders find their own way round the obstacles).</summary>
    public IReadOnlyList<GridPosition>? FixedPath { get; }

    /// <summary>Where the cult leader stands when the Night begins. Defaults to a free tile beside the Hearth.</summary>
    public GridPosition LeaderStart { get; }

    public bool IsBlocked(GridPosition position) => _blocked.Contains(position);

    public bool IsInBounds(GridPosition position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    /// <summary>The leader can stand anywhere except rocks, towers and the Hearth itself.</summary>
    public bool IsWalkableForLeader(GridPosition position) =>
        IsInBounds(position) && !IsBlocked(position) && position != Hearth;

    public IReadOnlyList<GridPosition> ResolveEnemyPath()
    {
        if (FixedPath == null)
        {
            return Pathfinder.FindPath(Width, Height, Spawn, Hearth, IsBlocked)
                   ?? throw new InvalidLayoutException("Enemies have no path to the Hearth. Even medieval soldiers need a way in.");
        }

        if (FixedPath.Count == 0 || FixedPath[0] != Spawn || FixedPath[FixedPath.Count - 1] != Hearth)
        {
            throw new InvalidLayoutException("The road must run from the spawn to the Hearth.");
        }

        for (var i = 0; i < FixedPath.Count; i++)
        {
            var tile = FixedPath[i];
            var step = i == 0 ? 1 : Math.Abs(tile.X - FixedPath[i - 1].X) + Math.Abs(tile.Y - FixedPath[i - 1].Y);
            if (!IsInBounds(tile) || step != 1 || (tile != Hearth && IsBlocked(tile)))
            {
                throw new InvalidLayoutException($"The road is broken or built over at {tile}.");
            }
        }

        return FixedPath;
    }

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
            if (!IsInBounds(position))
            {
                throw new InvalidLayoutException($"Tile {position} is outside the {Width}x{Height} grid.");
            }

            if (!occupied.Add(position))
            {
                throw new InvalidLayoutException($"Tile {position} is occupied twice.");
            }
        }

        if (!IsWalkableForLeader(LeaderStart))
        {
            throw new InvalidLayoutException($"The cult leader cannot start on {LeaderStart}. He refuses to stand on rocks.");
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

        AddPosition(hasher, LeaderStart);
        foreach (var tile in FixedPath ?? Array.Empty<GridPosition>())
        {
            AddPosition(hasher, tile); // only village layouts have one, so the demo's hash is unchanged
        }

        return hasher.Value;
    }

    private GridPosition FindDefaultLeaderStart()
    {
        GridPosition[] offsets = { new(-1, 0), new(0, -1), new(1, 0), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
        foreach (var offset in offsets)
        {
            var candidate = new GridPosition(Hearth.X + offset.X, Hearth.Y + offset.Y);
            if (IsWalkableForLeader(candidate))
            {
                return candidate;
            }
        }

        return Hearth; // Fully enclosed Hearth; Validate() reports it.
    }

    private static void AddPosition(Fnv1aHasher hasher, GridPosition position)
    {
        hasher.Add(position.X);
        hasher.Add(position.Y);
    }
}

public sealed class InvalidLayoutException(string message) : Exception(message);
