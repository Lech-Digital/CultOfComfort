using System;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Turns a near miss into the order the player meant (spec 2026-11-17 fun first, B3): a walk onto a building goes to
    /// the nearest open tile beside it, a throw past the reach lands on its edge. The playtest heard "He is not walking into
    /// that" and "He can't throw that far" far more than it saw anything happen. The snapped tile goes into the input log
    /// like any other, so replays agree. Pure: tested without Unity.
    /// </summary>
    public static class OrderSnap
    {
        /// <summary>The walkable tile nearest the tap (the tap itself if it's walkable), within <paramref name="searchRadius"/>; or null.</summary>
        public static GridPosition? Walkable(GridPosition tap, Func<GridPosition, bool> walkable, int searchRadius = 2)
        {
            if (walkable(tap))
            {
                return tap;
            }

            GridPosition? best = null;
            var bestDistance = int.MaxValue;
            for (var dy = -searchRadius; dy <= searchRadius; dy++)
            {
                for (var dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    var candidate = new GridPosition(tap.X + dx, tap.Y + dy);
                    var distance = dx * dx + dy * dy;
                    if (distance < bestDistance && walkable(candidate))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The tap if it's within <paramref name="reach"/> of the Prophet; otherwise the in-bounds tile on the line towards
        /// him that is furthest out but still in reach.
        /// </summary>
        public static GridPosition InReach(GridPosition tap, double leaderX, double leaderY, double reach, Func<GridPosition, bool> inBounds)
        {
            bool Reaches(GridPosition p) => (p.X - leaderX) * (p.X - leaderX) + (p.Y - leaderY) * (p.Y - leaderY) <= reach * reach && inBounds(p);

            if (Reaches(tap))
            {
                return tap;
            }

            var dx = tap.X - leaderX;
            var dy = tap.Y - leaderY;
            var length = Math.Sqrt(dx * dx + dy * dy);
            for (var along = reach; along > 0; along -= 0.25)
            {
                var candidate = new GridPosition((int)Math.Round(leaderX + dx / length * along), (int)Math.Round(leaderY + dy / length * along));
                if (Reaches(candidate))
                {
                    return candidate;
                }
            }

            return new GridPosition((int)Math.Round(leaderX), (int)Math.Round(leaderY));
        }
    }
}
