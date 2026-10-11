using System;
using System.Linq;
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
            Assert.GreaterOrEqual(layout.Count, 60, "no bare edges when zoomed out");
            foreach (var item in layout)
            {
                Assert.IsFalse(item.Tile.X >= -3 && item.Tile.Y >= -3 && item.Tile.X < Width + 3 && item.Tile.Y < Height + 3, $"{item.Model} at {item.Tile} crowds the village");
                Assert.IsFalse(river.Contains(item.Tile), $"{item.Model} at {item.Tile} stands in the river");
            }
        }
    }
}
