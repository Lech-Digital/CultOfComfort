using System.Collections.Generic;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>One marker on the edge of the board: where it sits (viewport, 0..1, bottom-left origin), which way it points, how many.</summary>
    public readonly struct EdgeMarker
    {
        public EdgeMarker(Vector2 at, float angleDegrees, int count)
        {
            At = at;
            AngleDegrees = angleDegrees;
            Count = count;
        }

        public Vector2 At { get; }

        /// <summary>0 points up the screen, 90 right, 180 down, 270 left.</summary>
        public float AngleDegrees { get; }

        public int Count { get; }
    }

    /// <summary>
    /// Raiders out of sight get a marker on the edge of the board they're beyond (spec 2026-11-17 fun first, B4): one per
    /// side, at the average of where they'd be, with how many. Pure: tested without the camera.
    /// </summary>
    public static class EdgeMarkers
    {
        /// <param name="viewportPoints">Each raider's viewport position (z &lt;= 0: behind the camera).</param>
        /// <param name="clear">The part of the screen the board shows in (between the HUD's top and its bar).</param>
        public static List<EdgeMarker> Compute(IEnumerable<Vector3> viewportPoints, Rect clear)
        {
            var sums = new Vector2[4];
            var counts = new int[4];
            foreach (var p in viewportPoints)
            {
                var point = p.z < 0f ? new Vector2(1f - p.x, 1f - p.y) : new Vector2(p.x, p.y); // behind: mirrored
                if (p.z >= 0f && clear.Contains(point))
                {
                    continue;
                }

                var side = Side(point, clear);
                sums[side] += point;
                counts[side]++;
            }

            var markers = new List<EdgeMarker>();
            const float inset = 0.035f;
            for (var side = 0; side < 4; side++)
            {
                if (counts[side] == 0)
                {
                    continue;
                }

                var mean = sums[side] / counts[side];
                var x = Mathf.Clamp(mean.x, clear.xMin + inset, clear.xMax - inset);
                var y = Mathf.Clamp(mean.y, clear.yMin + inset, clear.yMax - inset);
                var at = side switch
                {
                    0 => new Vector2(x, clear.yMax - inset), // top
                    1 => new Vector2(clear.xMax - inset, y), // right
                    2 => new Vector2(x, clear.yMin + inset), // bottom
                    _ => new Vector2(clear.xMin + inset, y), // left
                };
                markers.Add(new EdgeMarker(at, side * 90f, counts[side]));
            }

            return markers;
        }

        /// <summary>0 top, 1 right, 2 bottom, 3 left: the side the point is furthest beyond, relative to the clear area's size.</summary>
        private static int Side(Vector2 point, Rect clear)
        {
            var dx = (point.x - clear.center.x) / Mathf.Max(0.01f, clear.width);
            var dy = (point.y - clear.center.y) / Mathf.Max(0.01f, clear.height);
            return Mathf.Abs(dy) >= Mathf.Abs(dx) ? (dy > 0 ? 0 : 2) : (dx > 0 ? 1 : 3);
        }
    }
}
