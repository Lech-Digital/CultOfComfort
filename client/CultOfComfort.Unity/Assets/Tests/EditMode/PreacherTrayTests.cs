using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class PreacherTrayTests
    {
        private static PlaceRowModel Place(int id, int distance, bool reachable = true, int mine = 0, int rival = 0, bool patron = false, string gives = "3 Timber/h at full faith") =>
            new PlaceRowModel { Id = id, Name = $"Place {id}", Kind = "Hamlet", Walk = $"{distance}h", Distance = distance, Reachable = reachable, Mine = mine, Rival = rival, Patron = patron, Gives = gives };

        [Test]
        public void Build_ManyPlaces_TheFewWorthAWalkNearestFirst()
        {
            // Arrange
            var house = new MissionHouseModel { Unlocked = true };
            house.Preachers.Add(new PreacherCardModel { Name = "Brother Aldo", Status = "preaching at Place 1", AtHome = false, NodeId = 1 });
            house.Preachers.Add(new PreacherCardModel { Name = "Sister Wen", AtHome = true });
            house.Places.Add(Place(1, 1));                        // Aldo is already there
            house.Places.Add(Place(2, 5, reachable: false));      // too far
            house.Places.Add(Place(3, 2, patron: true, mine: 100)); // won
            house.Places.Add(Place(4, 4));
            house.Places.Add(Place(5, 2, gives: "No offerings: preaching here only angers him."));
            house.Places.Add(Place(6, 3));
            house.Places.Add(Place(7, 6));

            // Act
            var tray = PreacherTray.Build(house);

            // Assert
            CollectionAssert.AreEqual(new[] { 6, 4, 7 }, tray.Places.ConvertAll(p => p.Id), "paying places first, nearest first, three of them");
            CollectionAssert.AreEqual(new[] { "Brother Aldo · preaching at Place 1", "1 preacher ready: tap a place to send them." }, tray.Preachers);
            Assert.IsNull(tray.TaleLine);
        }

        [Test]
        public void Build_Locked_Empty()
        {
            // Act
            var tray = PreacherTray.Build(new MissionHouseModel { Unlocked = false });

            // Assert
            Assert.AreEqual(0, tray.Places.Count);
            Assert.AreEqual(0, tray.Preachers.Count);
        }
    }
}
