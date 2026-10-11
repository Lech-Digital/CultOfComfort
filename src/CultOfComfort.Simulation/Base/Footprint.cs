using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>The tiles a building covers: <c>Width</c> × <c>Depth</c> from its anchor (lowest x and y), swapped when rotated.</summary>
public static class Footprint
{
    public static IReadOnlyList<GridPosition> Tiles(GridPosition anchor, int width, int depth, bool rotated)
    {
        var (w, d) = rotated ? (depth, width) : (width, depth);
        var tiles = new List<GridPosition>(w * d);
        for (var dy = 0; dy < d; dy++)
        {
            for (var dx = 0; dx < w; dx++)
            {
                tiles.Add(new GridPosition(anchor.X + dx, anchor.Y + dy));
            }
        }

        return tiles;
    }

    public static IReadOnlyList<GridPosition> Tiles(BuildingSpec spec, GridPosition anchor, bool rotated) =>
        Tiles(anchor, spec.Width, spec.Depth, rotated);
}
