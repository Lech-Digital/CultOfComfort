using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class WallJointsTests
    {
        private static WallShape ShapeAt(params (int X, int Y)[] tiles)
        {
            var set = new HashSet<GridPosition>();
            foreach (var t in tiles)
            {
                set.Add(new GridPosition(t.X, t.Y));
            }

            return WallJoints.Shape(WallJoints.Mask(new GridPosition(5, 5), set));
        }

        [Test]
        public void Shape_FromNeighbours_PicksTheRightPiece()
        {
            // Assert
            Assert.AreEqual(WallShape.Post, ShapeAt((5, 5)));
            Assert.AreEqual(WallShape.End, ShapeAt((5, 5), (6, 5)));
            Assert.AreEqual(WallShape.Straight, ShapeAt((5, 5), (4, 5), (6, 5)));
            Assert.AreEqual(WallShape.Corner, ShapeAt((5, 5), (6, 5), (5, 6)));
            Assert.AreEqual(WallShape.Tee, ShapeAt((5, 5), (4, 5), (6, 5), (5, 4)));
            Assert.AreEqual(WallShape.Cross, ShapeAt((5, 5), (4, 5), (6, 5), (5, 4), (5, 6)));
            Assert.AreEqual(WallShape.Post, ShapeAt((5, 5), (6, 6)), "diagonals don't join");
        }
    }
}
