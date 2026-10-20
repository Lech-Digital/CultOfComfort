using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class WorldSceneryTests
    {
        private static readonly VillageMap Map = VillageMap.Demo;

        private static bool OnLand(GridPosition t) => t.X >= 0 && t.Y >= 0 && t.X < Map.Width && t.Y < Map.Height;

        [Test]
        public void River_PastTheMap_FlowsOnWestAndNorth()
        {
            // Assert — spec 2026-10-20 river: it comes in from the west and leaves to the north.
            Assert.Less(Map.River(-6, 39), 0.0, "in from the west");
            Assert.Less(Map.River(Map.Width - 12, -6), 0.0, "off to the north");
        }

        [Test]
        public void WorldLayout_DemoMap_FarForestOffTheLandAndTheRiverWithinBudget()
        {
            // Act
            var layout = WorldScenery.WorldLayout(Map);

            // Assert
            Assert.LessOrEqual(layout.Count, 220, "the phone keeps 60 fps");
            Assert.GreaterOrEqual(layout.Count, 40, "no bare edges when zoomed out");
            foreach (var item in layout)
            {
                Assert.IsFalse(item.Tile.X >= -3 && item.Tile.Y >= -3 && item.Tile.X < Map.Width + 3 && item.Tile.Y < Map.Height + 3, $"{item.Model} at {item.Tile} crowds the village");
                Assert.GreaterOrEqual(Map.River(item.Tile.X, item.Tile.Y), 1.0, $"{item.Model} at {item.Tile} stands in the river");
            }
        }

        [Test]
        public void BorderLayout_DemoMap_WallsTheLandOffExceptWhereRoadsComeIn()
        {
            // Act
            var layout = WorldScenery.BorderLayout(Map);
            var tracks = WorldScenery.PassTracks(Map).ToHashSet();

            // Assert — every dry tile just outside the land is under deep forest or a mountain, so the edge is obvious.
            for (var y = -1; y <= Map.Height; y++)
            {
                for (var x = -1; x <= Map.Width; x++)
                {
                    var tile = new GridPosition(x, y);
                    var outside = x == -1 || y == -1 || x == Map.Width || y == Map.Height;
                    var open = tracks.Any(t => Math.Max(Math.Abs(t.X - x), Math.Abs(t.Y - y)) <= 1) || Map.River(x, y) < 1.5;
                    if (!outside || open)
                    {
                        continue;
                    }

                    Assert.IsTrue(layout.Any(i => Math.Max(Math.Abs(i.Tile.X - x), Math.Abs(i.Tile.Y - y)) <= 1), $"the edge is open at {tile}");
                }
            }

            Assert.IsTrue(layout.Any(i => i.Model.StartsWith("peak_")), "mountain ranges on the horizon");
            Assert.IsTrue(layout.Any(i => i.Model == "deep_forest"), "deep forest at the edge");
        }

        [Test]
        public void BorderLayout_DemoMap_KeepsTheWaysInClearAndTheLandFree()
        {
            // Act
            var layout = WorldScenery.BorderLayout(Map);
            var tracks = WorldScenery.PassTracks(Map).ToHashSet();

            // Assert
            Assert.LessOrEqual(layout.Count, 420, "the phone keeps 60 fps");
            foreach (var item in layout)
            {
                Assert.IsFalse(OnLand(item.Tile), $"{item.Model} at {item.Tile} is on the land");
                Assert.IsFalse(tracks.Contains(item.Tile), $"{item.Model} at {item.Tile} blocks a raiders' track");
            }
        }

        [Test]
        public void PassTracks_DemoMap_OneOutOfEveryRoadsEntrance()
        {
            // Act
            var tracks = WorldScenery.PassTracks(Map);

            // Assert
            for (var road = 0; road < Map.RoadCount; road++)
            {
                var spawn = Map.SpawnOf(road);
                Assert.IsTrue(tracks.Any(t => Math.Abs(t.X - spawn.X) + Math.Abs(t.Y - spawn.Y) == 1 && !OnLand(t)), $"{Map.RoadName(road)} leads off the map");
            }

            Assert.IsTrue(tracks.All(t => !OnLand(t)));
        }

        [Test]
        public void RockModel_BedrockAndBoulders_LookDifferent()
        {
            // Arrange
            var bedrock = Map.Rocks.First(Map.IsBedrock);
            var boulder = Map.Boulders.First();

            // Act & Assert — bare crags can't be moved; mossy field stones can.
            Assert.AreEqual("crag", BoardView.RockModel(Map, bedrock));
            StringAssert.StartsWith("rock_", BoardView.RockModel(Map, boulder));
        }
    }
}
