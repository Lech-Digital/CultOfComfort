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

        [Test]
        public void Build_APreacherHome_TheirTaleWaitsWithTwoPricedEndings()
        {
            // Arrange — Brother Damp preaches 2 hours at Muddlecombe (1 hex: 20 min each way).
            var state = Village(2);
            state.Apply(new SendMissionary(Guid.NewGuid(), 1, 2), T0);
            var at = T0.AddHours(3);

            // Act
            var model = MissionHousePresenter.Build(state.Snapshot(at), Rich(), Name, at, null, WorldMapPresenter.Places(state.Snapshot(at), Rich()));

            // Assert
            var tale = model.Tales.Single();
            Assert.AreEqual("Brother Damp", tale.Teller);
            Assert.AreEqual(2, tale.Choices.Count);
            Assert.IsTrue(tale.Choices.All(c => c.Label.Contains(" · ")), "each ending says what it gives");
        }

        [Test]
        public void Places_HearthTwo_TheNearestFirstAndTheFarOnesLast()
        {
            // Act
            var places = WorldMapPresenter.Places(Village(2).Snapshot(T0), Rich());

            // Assert
            Assert.IsNotEmpty(places);
            Assert.IsTrue(places.First().Reachable);
            Assert.AreEqual(places.Where(p => p.Reachable).OrderBy(p => p.Distance).Select(p => p.Id), places.Where(p => p.Reachable).Select(p => p.Id));
            Assert.IsTrue(places.SkipWhile(p => p.Reachable).All(p => !p.Reachable), "the far places come after every reachable one");
        }

        [Test]
        public void Outcome_AGambleWithACost_SaysTheOddsAndBothSides()
        {
            // Act
            var line = MissionHousePresenter.Outcome(new TaleOption(new ResourceBag(0, 0, 14, 0), new ResourceBag(8, 0, 0, 0), 2, 0, 60));

            // Assert
            Assert.AreEqual("60%: −8 Timber, +14 Scrap, +2 XP", line);
        }

        [Test]
        public void Report_ATaleThatEnded_SaysHowItEnded()
        {
            // Act
            var line = MissionHousePresenter.Report(new RoadReport(1, 1, false, 2, RoadFind.Nothing, new ResourceBag(16, 0, 0, 0), 0, RoadTale.Goat, 0), "Muddlecombe");

            // Assert
            Assert.AreEqual("Brother Damp, back from Muddlecombe: sold a lost goat (+16 Timber).", line);
        }

        [Test]
        public void Ruler_ABishop_NamesHimShortAndFull()
        {
            try
            {
                // Act
                Ruler.Set("the Bishop of Sumpwell");

                // Assert
                Assert.AreEqual("the Bishop of Sumpwell", Ruler.Full);
                Assert.AreEqual("The Bishop", Ruler.Cap(Ruler.Short));
                var raided = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
                raided.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);
                StringAssert.StartsWith("The Bishop's men are coming", VillagePresenter.TensionNote(raided.Snapshot(T0.AddMinutes(48))));
            }
            finally
            {
                Ruler.Set("Lord Wulfric");
            }
        }

        [Test]
        public void Ruler_ALordWithASuffix_KeepsItInTheShortName()
        {
            try
            {
                // Act
                Ruler.Set("Lord Thorkel the Younger");

                // Assert
                Assert.AreEqual("Thorkel the Younger", Ruler.Short);
            }
            finally
            {
                Ruler.Set("Lord Wulfric");
            }
        }

        [Test]
        public void Ruler_Empty_KeepsWhoItHad()
        {
            // Act
            Ruler.Set(string.Empty);

            // Assert
            Assert.AreEqual("Lord Wulfric", Ruler.Full);
        }

        [Test]
        public void Hall_AnOpenDemand_ALetterDeliverAndThreePleas()
        {
            // Arrange
            var state = Village(2);
            var at = T0.AddHours(11);

            // Act
            var model = LordHallPresenter.Build(state.Snapshot(at), Rich(), "Lord Wulfric", "Wulfric's Keep", at);

            // Assert
            StringAssert.StartsWith("By order of Lord Wulfric:", model.Letter);
            Assert.IsTrue(model.Deliver.Enabled, "the stores are full");
            Assert.AreEqual(new[] { "haggle", "flatter", "stall" }, model.Pleas.Select(p => p.Id).ToArray());
            Assert.IsTrue(model.Pleas.All(p => p.Enabled));
        }

        [Test]
        public void Hall_NoDemandYet_SaysWhenTheNextLetterComes()
        {
            // Act
            var model = LordHallPresenter.Build(Village(2).Snapshot(T0), Rich(), "Lord Wulfric", "Wulfric's Keep", T0);

            // Assert
            Assert.IsNull(model.Letter);
            StringAssert.Contains("next letter comes in about 10h", model.NoDemand);
        }

        [Test]
        public void Hall_AfterAStall_StallIsSpent()
        {
            // Arrange
            var state = Village(2);
            var at = T0.AddHours(11);
            state.Apply(new PleadWithLord(Guid.NewGuid(), state.Snapshot(at).Domain.Demand.Id, Plea.Stall), at);

            // Act
            var model = LordHallPresenter.Build(state.Snapshot(at), Rich(), "Lord Wulfric", "Wulfric's Keep", at);

            // Assert
            var stall = model.Pleas.Single(p => p.Id == "stall");
            Assert.IsFalse(stall.Enabled);
            StringAssert.Contains("won't wait again", stall.Label);
        }
    }
}
