using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class OrderSnapTests
    {
        [Test]
        public void Walkable_TapOnOpenGround_StaysPut()
        {
            // Act & Assert
            Assert.AreEqual(new GridPosition(4, 4), OrderSnap.Walkable(new GridPosition(4, 4), _ => true));
        }

        [Test]
        public void Walkable_TapOnABuilding_GoesToTheNearestOpenTileBesideIt()
        {
            // Arrange — a 3×3 building round (5, 5); only (5, 7) and beyond are open below it.
            bool Open(GridPosition p) => !(p.X >= 4 && p.X <= 6 && p.Y >= 4 && p.Y <= 6);

            // Act
            var snapped = OrderSnap.Walkable(new GridPosition(5, 6), Open);

            // Assert
            Assert.AreEqual(new GridPosition(5, 7), snapped);
        }

        [Test]
        public void Walkable_NothingOpenNearby_Null()
        {
            // Act & Assert
            Assert.IsNull(OrderSnap.Walkable(new GridPosition(5, 5), _ => false));
        }

        [Test]
        public void InReach_TapPastTheReach_LandsOnTheEdgeTowardsTheTap()
        {
            // Act — reach 4 from (0, 0), a tap at (10, 0).
            var snapped = OrderSnap.InReach(new GridPosition(10, 0), 0, 0, 4, _ => true);

            // Assert
            Assert.AreEqual(new GridPosition(4, 0), snapped);
        }

        [Test]
        public void InReach_TapInsideTheReach_StaysPut()
        {
            // Act & Assert
            Assert.AreEqual(new GridPosition(2, 2), OrderSnap.InReach(new GridPosition(2, 2), 0, 0, 4, _ => true));
        }
    }
}
