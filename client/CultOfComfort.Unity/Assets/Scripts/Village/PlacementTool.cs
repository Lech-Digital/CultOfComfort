using System;
using System.Linq;
using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The ghost of a building being placed or moved: where it would stand, which way it faces, and the order that ✓
    /// sends. Nothing is built until then. Plain C#, so the whole flow is testable without a scene.
    /// </summary>
    public sealed class PlacementTool
    {
        private GridPosition _grab; // which of its tiles the finger holds

        private PlacementTool(BuildingType type, GridPosition anchor, bool rotated, int? movingId)
        {
            Type = type;
            Anchor = anchor;
            Rotated = rotated;
            MovingId = movingId;
        }

        public BuildingType Type { get; }
        public GridPosition Anchor { get; private set; }
        public bool Rotated { get; private set; }

        /// <summary>The building being moved, or null for a new one.</summary>
        public int? MovingId { get; }

        public bool Dragging { get; private set; }

        /// <summary>
        /// A new building's ghost, on the nearest good spot to <paramref name="near"/>: one where it fits and works
        /// (on the road network; a tower beside the raiders' road), either way round. Else the nearest spot where it
        /// fits, else <paramref name="near"/>. A newcomer used to hunt for a spot five times (found on the Pixel).
        /// </summary>
        public static PlacementTool StartNew(BuildingType type, GridPosition near, BaseSnapshot snapshot, VillageMap map, BuildingTable table)
        {
            var spec = table.Spec(type);
            var road = new HashSet<GridPosition>(map.Road);
            var needsRoad = !BuildingTable.IsRoad(type) && !BuildingTable.IsBarrier(type) && type is not (BuildingType.Tree or BuildingType.Stone) && BuildingTable.NeedsRoad(type);
            bool Good(GridPosition anchor, bool rotated)
            {
                var tiles = Footprint.Tiles(spec, anchor, rotated);
                return spec.IsTower
                    ? tiles.Any(t => road.Contains(new GridPosition(t.X + 1, t.Y)) || road.Contains(new GridPosition(t.X - 1, t.Y)) || road.Contains(new GridPosition(t.X, t.Y + 1)) || road.Contains(new GridPosition(t.X, t.Y - 1)))
                    : !needsRoad || map.WouldConnect(snapshot, tiles);
            }

            (GridPosition, bool)? fits = null;
            for (var radius = 0; radius <= 8; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        {
                            continue;
                        }

                        var anchor = new GridPosition(near.X + dx, near.Y + dy);
                        foreach (var rotated in spec.Width == spec.Depth ? new[] { false } : new[] { false, true })
                        {
                            if (map.PlacementProblem(snapshot, spec, anchor, rotated, null, table) != null)
                            {
                                continue;
                            }

                            if (Good(anchor, rotated))
                            {
                                return new PlacementTool(type, anchor, rotated, null);
                            }

                            fits ??= (anchor, rotated);
                        }
                    }
                }
            }

            return fits is var (a, r) ? new PlacementTool(type, a, r, null) : new PlacementTool(type, near, false, null);
        }

        /// <summary>A standing building lifted for moving: it starts where and as it stands.</summary>
        public static PlacementTool StartMove(BuildingView building) =>
            new PlacementTool(building.Type, building.Position, building.Rotated, building.Id);

        public IReadOnlyList<GridPosition> Tiles(BuildingTable table) => Footprint.Tiles(table.Spec(Type), Anchor, Rotated);

        /// <summary>A finger down on one of its tiles picks it up; anywhere else, the camera pans instead.</summary>
        public bool TryGrab(GridPosition tile, BuildingTable table)
        {
            foreach (var t in Tiles(table))
            {
                if (t == tile)
                {
                    _grab = new GridPosition(tile.X - Anchor.X, tile.Y - Anchor.Y);
                    Dragging = true;
                    return true;
                }
            }

            return false;
        }

        public void DragTo(GridPosition tile) => Anchor = new GridPosition(tile.X - _grab.X, tile.Y - _grab.Y);

        public void Release() => Dragging = false;

        public void Rotate() => Rotated = !Rotated;

        /// <summary>Why it can't stand here (null: it can) — the village's own rule; costs are checked when ✓ is pressed.</summary>
        public string Problem(BaseSnapshot snapshot, VillageMap map, BuildingTable table) =>
            map.PlacementProblem(snapshot, table.Spec(Type), Anchor, Rotated, MovingId, table);

        public BaseCommand Command() => MovingId != null
            ? new MoveBuilding(Guid.NewGuid(), MovingId.Value, Anchor, Rotated)
            : (BaseCommand)new PlaceBuilding(Guid.NewGuid(), Type, Anchor, Rotated);
    }
}
