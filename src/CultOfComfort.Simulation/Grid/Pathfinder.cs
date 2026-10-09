using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Grid;

public static class Pathfinder
{
    // Fixed neighbour order keeps BFS deterministic: same layout → same path on every platform.
    private static readonly GridPosition[] Neighbours = { new(1, 0), new(0, 1), new(0, -1), new(-1, 0) };

    /// <summary>
    /// Shortest 4-connected path from <paramref name="start"/> to <paramref name="goal"/>, inclusive of both.
    /// The goal is always enterable (it is usually the Hearth). Returns null when no path exists.
    /// </summary>
    public static IReadOnlyList<GridPosition>? FindPath(
        int width,
        int height,
        GridPosition start,
        GridPosition goal,
        Func<GridPosition, bool> isBlocked)
    {
        bool InBounds(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;
        int IndexOf(GridPosition p) => p.Y * width + p.X;

        if (!InBounds(start) || !InBounds(goal))
        {
            return null;
        }

        var visited = new bool[width * height];
        var parent = new int[width * height];
        var queue = new Queue<GridPosition>();

        visited[IndexOf(start)] = true;
        parent[IndexOf(start)] = -1;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return Reconstruct(parent, IndexOf(goal), width);
            }

            foreach (var offset in Neighbours)
            {
                var next = new GridPosition(current.X + offset.X, current.Y + offset.Y);
                if (!InBounds(next) || visited[IndexOf(next)] || (next != goal && isBlocked(next)))
                {
                    continue;
                }

                visited[IndexOf(next)] = true;
                parent[IndexOf(next)] = IndexOf(current);
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static List<GridPosition> Reconstruct(int[] parent, int goalIndex, int width)
    {
        var path = new List<GridPosition>();
        for (var index = goalIndex; index != -1; index = parent[index])
        {
            path.Add(new GridPosition(index % width, index / width));
        }

        path.Reverse();
        return path;
    }
}
