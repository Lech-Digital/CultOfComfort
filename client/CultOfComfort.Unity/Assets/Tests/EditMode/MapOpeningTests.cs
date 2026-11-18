using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class MapOpeningTests
    {
        [Test]
        public void NearestReachable_SkipsFarCampsAndSeats_PicksTheClosest()
        {
            // Arrange
            var model = new WorldMapModel();
            model.Nodes.Add(new MapNodeModel { Id = 0, Home = true, Reachable = true, At = Vector2.zero });
            model.Nodes.Add(new MapNodeModel { Id = 1, Reachable = false, At = new Vector2(1, 0) });           // too far for now
            model.Nodes.Add(new MapNodeModel { Id = 2, Reachable = true, Camp = true, At = new Vector2(1, 1) }); // a rival camp
            model.Nodes.Add(new MapNodeModel { Id = 3, Reachable = true, Seat = true, At = new Vector2(2, 0) }); // the lord's seat
            model.Nodes.Add(new MapNodeModel { Id = 4, Reachable = true, At = new Vector2(5, 5) });
            model.Nodes.Add(new MapNodeModel { Id = 5, Reachable = true, At = new Vector2(3, 0) });

            // Act
            var id = MapOpening.NearestReachable(model);

            // Assert
            Assert.AreEqual(5, id);
        }

        [Test]
        public void NearestReachable_NothingInReach_MinusOne()
        {
            // Arrange
            var model = new WorldMapModel();
            model.Nodes.Add(new MapNodeModel { Id = 0, Home = true, Reachable = true });
            model.Nodes.Add(new MapNodeModel { Id = 1, Reachable = false, At = Vector2.one });

            // Act & Assert
            Assert.AreEqual(-1, MapOpening.NearestReachable(model));
        }
    }
}
