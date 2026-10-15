using System.Linq;
using CultOfComfort.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class WorldLandTests
    {
        private static readonly WorldMap Map = WorldGenerator.Generate(42);

        private static WorldLand Land(ulong seed = 42) =>
            new WorldLand(seed, Map.Shape.Radius, Map.Nodes.Select(n => (n.Id, n.Type, n.Q, n.R)), (3, -2));

        [Test]
        public void ChunkColoursAndHeights_SameSeed_SameLand()
        {
            // Arrange
            var a = Land();
            var b = Land();

            // Act
            var colours = (a.ChunkColours(1, -2), b.ChunkColours(1, -2));
            var heights = (a.ChunkHeights(1, -2), b.ChunkHeights(1, -2));

            // Assert
            CollectionAssert.AreEqual(colours.Item1, colours.Item2);
            CollectionAssert.AreEqual(heights.Item1, heights.Item2);
        }

        [Test]
        public void Height_OtherSeed_OtherLand()
        {
            // Arrange
            var a = Land(42);
            var b = Land(43);

            // Act
            var differs = Enumerable.Range(0, 50).Count(i => Mathf.Abs(a.Height(new Vector2(i * 3.1f, i * -1.7f)) - b.Height(new Vector2(i * 3.1f, i * -1.7f))) > 0.01f);

            // Assert
            Assert.Greater(differs, 25);
        }

        [Test]
        public void Height_EveryPlace_StandsOnDryLevelGround()
        {
            // Arrange
            var land = Land();

            // Act / Assert — at the spot and half a unit round it: above water and nearly flat.
            foreach (var place in land.Places)
            {
                var h = land.Height(place.At);
                Assert.Greater(h, WorldLand.WaterLevel + 0.1f, $"place {place.Id} is dry");
                foreach (var d in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    Assert.AreEqual(h, land.Height(place.At + d * 0.5f), 0.08f, $"place {place.Id} is level");
                }
            }
        }

        [Test]
        public void Height_PastTheWorldsEdge_RisesIntoMountains()
        {
            // Arrange
            var land = Land();
            var edge = WorldLand.HexCentre(Map.Shape.Radius + 3, 0);

            // Act
            var height = land.Height(edge);

            // Assert
            Assert.Greater(height, 3f);
        }

        [Test]
        public void Roads_NeighbouringPlaces_AreJoined()
        {
            // Arrange
            var land = Land();
            var a = Map.Nodes.First(n => Map.NodeAt(n.Q + 1, n.R) != null);
            var b = Map.NodeAt(a.Q + 1, a.R);
            land.TryPlace(a.Id, out var pa);
            land.TryPlace(b.Id, out var pb);

            // Act
            var joined = land.Roads.Any(r => (r[0] == pa.At && r[r.Length - 1] == pb.At) || (r[0] == pb.At && r[r.Length - 1] == pa.At));

            // Assert
            Assert.IsTrue(joined);
        }

        [Test]
        public void Path_AlongTheTracks_StartsHomeAndEndsThere()
        {
            // Arrange — a place two steps from home along the tracks.
            var land = Land();
            land.TryPlace(WorldMapPresenter.HomeId, out var home);
            var target = land.Places.Where(p => p.Type != null).OrderBy(p => Vector2.Distance(p.At, home.At)).Skip(3).First();

            // Act
            var path = land.Path(WorldMapPresenter.HomeId, target.Id);

            // Assert
            Assert.GreaterOrEqual(path.Count, 2);
            Assert.AreEqual(home.At, path[0]);
            Assert.AreEqual(target.At, path[path.Count - 1]);
        }

        [Test]
        public void Path_UnknownPlace_IsEmpty()
        {
            // Act
            var path = Land().Path(WorldMapPresenter.HomeId, 999999);

            // Assert
            Assert.IsEmpty(path);
        }

        [Test]
        public void Along_HalfWay_IsTheMiddleOfTheWalk()
        {
            // Arrange
            var path = new[] { Vector2.zero, new Vector2(2f, 0f), new Vector2(2f, 2f) };

            // Act
            var middle = WorldLand.Along(path, 0.5f);

            // Assert
            Assert.AreEqual(2f, middle.x, 1e-4f);
            Assert.AreEqual(0f, middle.y, 1e-4f);
        }

        [Test]
        public void Props_NearAPlace_NothingGrowsOnIt()
        {
            // Arrange
            var land = Land();
            var place = land.Places.First(p => p.Type != null && Mathf.Abs(p.At.x) < 40f && Mathf.Abs(p.At.y) < 40f);
            var cx = Mathf.FloorToInt(place.At.x / WorldLand.ChunkSize);
            var cz = Mathf.FloorToInt(place.At.y / WorldLand.ChunkSize);

            // Act
            var props = land.Props(cx, cz);

            // Assert
            Assert.IsNotEmpty(props, "the land grows something");
            Assert.IsFalse(props.Any(p => Vector2.Distance(new Vector2(p.At.x, p.At.z), place.At) < 1.3f), "no tree on a village");
        }
    }
}
