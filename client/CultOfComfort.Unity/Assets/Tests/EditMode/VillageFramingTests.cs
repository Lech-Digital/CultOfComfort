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
        public void NightBounds_EveryRoad_FramesASmallStageWithTheHearth()
        {
            // Arrange — playtest 2026-11-17: on a long road the Night framed the valley and the village was a speck.
            var map = VillageMap.Demo;
            var hearth = map.HearthTiles[0];

            for (var road = 0; road < map.RoadCount; road++)
            {
                // Act
                var (min, max) = VillageFraming.NightBounds(map, road);

                // Assert
                Assert.LessOrEqual(max.x - min.x, VillageFraming.NightRoadTiles + 6f, $"road {road} is too wide a stage");
                Assert.LessOrEqual(max.y - min.y, VillageFraming.NightRoadTiles + 6f, $"road {road} is too tall a stage");
                Assert.That(hearth.X, Is.InRange(min.x, max.x));
                Assert.That(-hearth.Y, Is.InRange(min.y, max.y));
            }
        }

        [Test]
        public void RoamBounds_DemoMap_ReachesPastTheEdgeToTheRiver()
        {
            // Act
            var (min, max) = VillageFraming.RoamBounds(VillageMap.Demo);

            // Assert — room to look past the edge, at the forest and the river running on.
            Assert.LessOrEqual(min.y, -(VillageMap.Demo.Height - 1) - 6f);
            Assert.GreaterOrEqual(max.x, VillageMap.Demo.Width - 1 + 6f);
        }
    }
}
