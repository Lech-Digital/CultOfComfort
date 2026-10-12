using System;
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

        /// <summary>A new building's ghost, on the nearest spot to <paramref name="near"/> where it fits (or there, if none).</summary>
        public static PlacementTool StartNew(BuildingType type, GridPosition near, BaseSnapshot snapshot, VillageMap map, BuildingTable table)
        {
            var spec = table.Spec(type);
            for (var radius = 0; radius <= 6; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var anchor = new GridPosition(near.X + dx, near.Y + dy);
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == radius && map.PlacementProblem(snapshot, spec, anchor, false, null, table) == null)
                        {
                            return new PlacementTool(type, anchor, false, null);
                        }
                    }
                }
            }

            return new PlacementTool(type, near, false, null);
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
