using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    [Flags]
    public enum WallLink
    {
        None = 0,
        North = 1, // y − 1
        East = 2,  // x + 1
        South = 4, // y + 1
        West = 8,  // x − 1
    }

    public enum WallShape
    {
        Post,
        End,
        Straight,
        Corner,
        Tee,
        Cross,
    }

    /// <summary>Fences and walls join up by themselves: each tile looks at its four neighbours (diagonals never join).</summary>
    public static class WallJoints
    {
        public static WallLink Mask(GridPosition tile, ISet<GridPosition> barriers)
        {
            var mask = WallLink.None;
            if (barriers.Contains(new GridPosition(tile.X, tile.Y - 1))) mask |= WallLink.North;
            if (barriers.Contains(new GridPosition(tile.X + 1, tile.Y))) mask |= WallLink.East;
            if (barriers.Contains(new GridPosition(tile.X, tile.Y + 1))) mask |= WallLink.South;
            if (barriers.Contains(new GridPosition(tile.X - 1, tile.Y))) mask |= WallLink.West;
            return mask;
        }

        public static WallShape Shape(WallLink mask)
        {
            var count = 0;
            foreach (var link in new[] { WallLink.North, WallLink.East, WallLink.South, WallLink.West })
            {
                if ((mask & link) != 0)
                {
                    count++;
                }
            }

            switch (count)
            {
                case 0: return WallShape.Post;
                case 1: return WallShape.End;
                case 2: return mask == (WallLink.North | WallLink.South) || mask == (WallLink.East | WallLink.West) ? WallShape.Straight : WallShape.Corner;
                case 3: return WallShape.Tee;
                default: return WallShape.Cross;
            }
        }
    }
}
