using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class WorldMapPresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        private static BuildingTable Rich() => new BuildingTable
        {
            StartingStock = new ResourceBag(8000, 2000, 2000, 1000), HearthStorageCaps = new[] { 9000, 9000, 9000 }, HearthBeds = 12,
        };

        private static (BaseState State, DateTimeOffset Now) Village(int hearth)
        {
            var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
            var now = T0;
            for (var level = 2; level <= hearth; level++)
            {
                state.Apply(new UpgradeHearth(Guid.NewGuid()), now);
                now = now.AddHours(2);
            }

            return (state, now);
        }

        [Test]
        public void Layout_EveryPlace_InsideTheMapWithTheVillageAtTheBottom()
        {
            // Act
            var layout = WorldMapPresenter.Layout(DomainMap.Marchlands.Nodes);

            // Assert
            Assert.IsTrue(layout.Values.All(p => p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f));
            Assert.AreEqual(layout.Values.Max(p => p.y), layout[WorldMapPresenter.HomeId].y, 1e-4f, "home is the lowest point");
            Assert.AreEqual(layout.Values.Min(p => p.y), layout[16].y, 1e-4f, "the Keep is at the top");
        }

        [Test]
        public void Build_HearthTwo_EveryPlaceHomeAndRoads()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var model = WorldMapPresenter.Build(state.Snapshot(now), Rich(), now);

            // Assert
            Assert.IsTrue(model.Unlocked);
            Assert.AreEqual(17, model.Nodes.Count, "16 places and home");
            Assert.IsTrue(model.Roads.Any(r => r.From == WorldMapPresenter.HomeId || r.To == WorldMapPresenter.HomeId), "roads lead home");
            Assert.IsFalse(model.Nodes.Single(n => n.Id == 16).Reachable, "the Keep is beyond Hearth 2's reach");
            StringAssert.Contains("1 ready", model.MissionaryLine);
        }

        [Test]
        public void Card_ReachableHamlet_OffersFourSessionsWithCosts()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var card = WorldMapPresenter.Card(state.Snapshot(now), Rich(), 1);

            // Assert
            Assert.AreEqual("Muddlecombe", card.Title);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 8 }, card.Options.Select(o => o.Hours).ToArray());
            Assert.IsTrue(card.Options.All(o => o.Enabled));
            StringAssert.Contains("Timber", card.Options.Last().Label);
        }

        [Test]
        public void Card_HearthOne_NoOptionsAndSaysWhen()
        {
            // Arrange
            var (state, now) = Village(1);

            // Act
            var card = WorldMapPresenter.Card(state.Snapshot(now), Rich(), 1);

            // Assert
            Assert.IsEmpty(card.Options);
            StringAssert.Contains("Hearth 2", card.Note);
        }

        [Test]
        public void Build_MissionaryOut_WalksTheRoadThenPreaches()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new SendMissionary(Guid.NewGuid(), 1, 2), now);

            // Act
            var walking = WorldMapPresenter.Build(state.Snapshot(now.AddMinutes(10)), Rich(), now.AddMinutes(10));
            var preaching = WorldMapPresenter.Build(state.Snapshot(now.AddHours(1)), Rich(), now.AddHours(1));

            // Assert
            StringAssert.StartsWith("Walking to Muddlecombe", walking.Missionaries.Single().Text);
            StringAssert.StartsWith("Preaching at Muddlecombe", preaching.Missionaries.Single().Text);
            var home = walking.Nodes.Single(n => n.Home).At;
            var there = walking.Nodes.Single(n => n.Id == 1).At;
            var at = walking.Missionaries.Single().At;
            Assert.Greater((at - home).magnitude, 0.001f, "set off");
            Assert.Greater((there - at).magnitude, 0.001f, "not there yet");
        }

        [Test]
        public void Tithes_DuringAWarning_OfferToBuyOffTheRaid()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), now);
            var snapshot = state.Snapshot(now);

            // Act
            var options = WorldMapPresenter.Tithes(snapshot, Rich());
            var buyOff = options.Single(o => o.Label.StartsWith("Buy off"));
            state.Apply(new PayTithe(Guid.NewGuid(), buyOff.Amount), now);

            // Assert
            Assert.IsNull(state.Snapshot(now).RaidWarningEndsAtUtc, "the suggested amount really calls it off");
        }
        [Test]
        public void Roster_OneWalkingTwoSlots_ShowsHerProgressThenAReadySlot()
        {
            // Arrange — half way through a 20-minute walk.
            var m = new MissionaryView(1, 3, 2, MissionaryPhase.Travelling, T0.AddMinutes(10));

            // Act
            var rows = WorldMapPresenter.Roster(new[] { (m, "Mudwick", 1200.0) }, 2, true, T0);

            // Assert
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Walking to Mudwick", rows[0].Title);
            Assert.AreEqual(0.5f, rows[0].Progress, 0.01f);
            Assert.AreEqual(3, rows[0].NodeId);
            Assert.AreEqual(-1, rows[1].NodeId, "the second is ready at home");
        }

        [Test]
        public void Roster_Preaching_ProgressIsTheSermon()
        {
            // Arrange — a 4-hour sermon with one hour left.
            var m = new MissionaryView(1, 3, 4, MissionaryPhase.Preaching, T0.AddHours(1));

            // Act
            var rows = WorldMapPresenter.Roster(new[] { (m, "Mudwick", 1200.0) }, 1, true, T0);

            // Assert
            Assert.AreEqual(0.75f, rows[0].Progress, 0.01f);
            StringAssert.Contains("preaching left", rows[0].Detail);
        }

        [Test]
        public void Chip_NobodyOut_SaysHowManyAreReady()
        {
            // Act
            var chip = WorldMapPresenter.Chip(WorldMapPresenter.Roster(Array.Empty<(MissionaryView, string, double)>(), 2, true, T0), 2);

            // Assert
            Assert.AreEqual("Missionaries: 2 ready", chip);
        }
    }
}
