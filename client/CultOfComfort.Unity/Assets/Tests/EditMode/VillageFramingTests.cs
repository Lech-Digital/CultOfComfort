using CultOfComfort.Simulation.Base;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class VillageFramingTests
    {
        [Test]
        public void HomeBounds_BigOpenLand_FramesTheCoreWithTheEntranceAndTheHearth()
        {
            // Arrange — 30×20 is open at Hearth 1: framing all of it makes the village tiny on a phone.
            var map = VillageMap.Demo;

            // Act
            var (min, max) = VillageFraming.HomeBounds(map, 1);

            // Assert
            Assert.AreEqual(new Vector2(0f, -15f), min);
            Assert.AreEqual(new Vector2(21f, 0f), max);
        }

        [Test]
        public void HomeBounds_SmallOpenLand_FramesAllOfIt()
        {
            // Arrange
            var map = new VillageMap(18, 11, new CultOfComfort.Simulation.Grid.GridPosition(0, 1), new CultOfComfort.Simulation.Grid.GridPosition(17, 9),
                System.Array.Empty<CultOfComfort.Simulation.Grid.GridPosition>());

            // Act
            var (min, max) = VillageFraming.HomeBounds(map, 1);

            // Assert
            Assert.AreEqual(new Vector2(0f, -10f), min);
            Assert.AreEqual(new Vector2(17f, 0f), max);
        }

        [Test]
        public void RoamBounds_DemoMap_ReachesPastTheEdgeToTheRiver()
        {
            // Act
            var (min, max) = VillageFraming.RoamBounds(VillageMap.Demo);

            // Assert — the river runs 4 tiles past the south edge and 5 past the east edge (WorldScenery.RiverPath).
            Assert.LessOrEqual(min.y, -(VillageMap.Demo.Height - 1) - 6f);
            Assert.GreaterOrEqual(max.x, VillageMap.Demo.Width - 1 + 6f);
        }
    }
}
