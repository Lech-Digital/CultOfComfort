using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class WorldSceneryTests
    {
        private const int Width = 30;
        private const int Height = 20;

        [Test]
        public void RiverPath_DemoMap_StaysOffTheLandWithAGrassMargin()
        {
            // Act
            var river = WorldScenery.RiverPath(Width, Height);

            // Assert
            Assert.Greater(river.Count, 40);
            foreach (var tile in river)
            {
                var nearLand = tile.X >= -1 && tile.Y >= -1 && tile.X <= Width && tile.Y <= Height;
                Assert.IsFalse(nearLand, $"river tile {tile} is on or next to the village");
            }
        }

        [Test]
        public void RiverPath_DemoMap_IsOneUnbrokenStream()
        {
            // Act
            var river = WorldScenery.RiverPath(Width, Height);

            // Assert
            for (var i = 1; i < river.Count; i++)
            {
                var step = Math.Max(Math.Abs(river[i].X - river[i - 1].X), Math.Abs(river[i].Y - river[i - 1].Y));
                Assert.LessOrEqual(step, 1, $"gap between {river[i - 1]} and {river[i]}");
            }
        }

        [Test]
        public void WorldLayout_DemoMap_FarForestOffTheLandAndTheRiverWithinBudget()
        {
            // Act
            var river = WorldScenery.RiverPath(Width, Height).ToHashSet();
            var layout = WorldScenery.WorldLayout(Width, Height);

            // Assert
            Assert.LessOrEqual(layout.Count, 160, "the phone keeps 60 fps");
            var mountains = WorldScenery.BorderLayout(Width, Height, new GridPosition(0, 1)).Count(i => i.Model == "mountain");
            Assert.GreaterOrEqual(layout.Count + mountains, 60, "no bare edges when zoomed out (mountains to the north and west)");
            foreach (var item in layout)
            {
                Assert.IsFalse(item.Tile.X >= -3 && item.Tile.Y >= -3 && item.Tile.X < Width + 3 && item.Tile.Y < Height + 3, $"{item.Model} at {item.Tile} crowds the village");
                Assert.IsFalse(river.Contains(item.Tile), $"{item.Model} at {item.Tile} stands in the river");
            }
        }

        private static readonly GridPosition Spawn = new GridPosition(0, 1);

        private static bool InPass(GridPosition tile) => tile.X < 0 && Math.Abs(tile.Y - Spawn.Y) <= 1;

        [Test]
        public void BorderLayout_DemoMap_WallsTheLandOffExceptTheRaidersPass()
        {
            // Act
            var layout = WorldScenery.BorderLayout(Width, Height, Spawn);

            // Assert — every tile just outside the land is under deep forest or a mountain, so the edge is obvious.
            for (var y = -1; y <= Height; y++)
            {
                for (var x = -1; x <= Width; x++)
                {
                    var tile = new GridPosition(x, y);
                    var outside = x == -1 || y == -1 || x == Width || y == Height;
                    if (!outside || InPass(tile))
                    {
                        continue;
                    }

                    Assert.IsTrue(layout.Any(i => Math.Max(Math.Abs(i.Tile.X - x), Math.Abs(i.Tile.Y - y)) <= 1), $"the edge is open at {tile}");
                }
            }

            Assert.IsTrue(layout.Any(i => i.Model == "mountain"), "mountains on the horizon");
            Assert.IsTrue(layout.Any(i => i.Model == "deep_forest"), "deep forest at the edge");
        }

        [Test]
        public void BorderLayout_DemoMap_KeepsThePassClearAndTheLandFree()
        {
            // Act
            var layout = WorldScenery.BorderLayout(Width, Height, Spawn);

            // Assert
            Assert.LessOrEqual(layout.Count, 260, "the phone keeps 60 fps");
            foreach (var item in layout)
            {
                Assert.IsFalse(item.Tile.X >= 0 && item.Tile.Y >= 0 && item.Tile.X < Width && item.Tile.Y < Height, $"{item.Model} at {item.Tile} is on the land");
                Assert.IsFalse(InPass(item.Tile) || item.Tile.X < 0 && Math.Abs(item.Tile.Y - Spawn.Y) <= (item.Model == "mountain" ? 3 : 1),
                    $"{item.Model} at {item.Tile} blocks the raiders' pass");
            }
        }

        [Test]
        public void PassTrack_DemoMap_RunsWestFromTheSpawnToTheHorizon()
        {
            // Act
            var track = WorldScenery.PassTrack(Spawn);

            // Assert
            Assert.AreEqual(new GridPosition(-1, Spawn.Y), track[0]);
            Assert.LessOrEqual(track.Last().X, -12);
            Assert.IsTrue(track.All(t => t.Y == Spawn.Y));
        }

        [Test]
        public void RockModel_BedrockAndBoulders_LookDifferent()
        {
            // Arrange
            var map = VillageMap.Demo;
            var bedrock = map.Rocks.First(map.IsBedrock);
            var boulder = map.Boulders.First();

            // Act & Assert — bare crags can't be moved; mossy field stones can.
            Assert.AreEqual("crag", BoardView.RockModel(map, bedrock));
            StringAssert.StartsWith("rock_", BoardView.RockModel(map, boulder));
        }
    }
}
