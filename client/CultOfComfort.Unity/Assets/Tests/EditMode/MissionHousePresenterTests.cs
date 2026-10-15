using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class MissionHousePresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

        private static BuildingTable Rich() => new BuildingTable
        {
            StartingStock = new ResourceBag(8000, 2000, 2000, 1000), HearthStorageCaps = new[] { 9000, 9000, 9000 }, HearthBeds = 12,
        };

        private static BaseState Village(int hearth)
        {
            var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
            for (var level = 2; level <= hearth; level++)
            {
                state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
                state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            }

            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            return state;
        }

        private static string Name(int id, bool world) => DomainMap.Marchlands.Node(id)?.Name ?? "?";

        [Test]
        public void Build_HearthThree_ACardPerPreacherReadyAtHome()
        {
            // Act
            var model = MissionHousePresenter.Build(Village(3).Snapshot(T0), Rich(), Name, T0);

            // Assert
            Assert.AreEqual(2, model.Preachers.Count);
            Assert.AreEqual("Brother Damp", model.Preachers[0].Name);
            Assert.AreEqual("Tireless", model.Preachers[0].Quirk);
            Assert.IsTrue(model.Preachers.All(p => p.AtHome));
            StringAssert.Contains("ready", model.Preachers[0].Status);
        }

        [Test]
        public void Build_APreacherOut_SaysWhereAndCanBeFound()
        {
            // Arrange
            var state = Village(2);
            state.Apply(new SendMissionary(Guid.NewGuid(), 1, 4), T0);

            // Act
            var model = MissionHousePresenter.Build(state.Snapshot(T0.AddMinutes(5)), Rich(), Name, T0.AddMinutes(5));

            // Assert
            var damp = model.Preachers.Single();
            Assert.IsFalse(damp.AtHome);
            Assert.AreEqual(1, damp.NodeId);
            StringAssert.Contains("Walking to Muddlecombe", damp.Status);
        }

        [Test]
        public void Build_HearthOne_TheHouseIsShut()
        {
            // Act
            var model = MissionHousePresenter.Build(Village(1).Snapshot(T0), Rich(), Name, T0);

            // Assert
            Assert.IsFalse(model.Unlocked);
            Assert.IsEmpty(model.Preachers);
        }

        [Test]
        public void Report_AGoat_SaysWhoWhereAndWhat()
        {
            // Act
            var line = MissionHousePresenter.Report(new RoadReport(1, 1, false, 4, RoadFind.Goat, new ResourceBag(32, 0, 0, 0), 0), "Muddlecombe");

            // Assert
            StringAssert.Contains("Brother Damp", line);
            StringAssert.Contains("Muddlecombe", line);
            StringAssert.Contains("goat", line);
        }

        [Test]
        public void SetDemand_AnOpenDemand_ALineWithTheDeadlineAndTheStakes()
        {
            // Arrange — the first demand comes ten hours after Hearth 2.
            var state = Village(2);
            var at = T0.AddHours(11);
            var snapshot = state.Snapshot(at);
            var model = new WorldMapModel { Lord = "Lord Wulfric" };

            // Act
            WorldMapPresenter.SetDemand(model, snapshot, at);

            // Assert
            Assert.AreEqual(snapshot.Domain.Demand.Id, model.DemandId);
            StringAssert.StartsWith("Lord Wulfric demands", model.DemandLine);
            StringAssert.Contains("left", model.DemandLine);
            Assert.IsTrue(model.DemandAffordable);
        }

        [Test]
        public void Card_TwoPreachers_PicksTheOneAsked()
        {
            // Arrange
            var state = Village(3);

            // Act
            var card = WorldMapPresenter.Card(state.Snapshot(T0), Rich(), 1, preacherId: 2);

            // Assert
            Assert.AreEqual(2, card.SelectedPreacher);
            Assert.AreEqual(2, card.Preachers.Count);
            Assert.IsTrue(card.Preachers.Single(p => p.Id == 2).Selected);
            StringAssert.Contains("Sister Drizzle", card.Note);
        }
    }
}
