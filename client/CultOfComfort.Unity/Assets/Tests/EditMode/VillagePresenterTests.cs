using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class VillagePresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        private static BaseSnapshot Fresh() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Snapshot(T0);

        [Test]
        public void BuildCards_FreshVillage_FlingerAndCisternLockedAtHearthTwo()
        {
            // Act
            var cards = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default);

            // Assert
            Assert.IsFalse(cards.Any(c => c.Type == BuildingType.Hearth));
            var flinger = cards.Single(c => c.Type == BuildingType.SaplingFlinger);
            Assert.IsTrue(flinger.Locked);
            Assert.AreEqual("Hearth 2", flinger.LockText);
            var woodcutter = cards.Single(c => c.Type == BuildingType.Woodcutter);
            Assert.IsFalse(woodcutter.Locked);
            Assert.AreEqual("50 Timber", woodcutter.Cost);
            Assert.AreEqual("30s", woodcutter.Time);
            Assert.AreEqual("b_woodcutter", woodcutter.Icon);
        }

        [Test]
        public void Card_HearthAtLevelOne_OffersTheLevelTwoUpgrade()
        {
            // Act
            var card = VillagePresenter.Card(Fresh(), BuildingTable.Default, 0);

            // Assert
            Assert.IsTrue(card.IsHearth);
            Assert.AreEqual("Hearth · level 1", card.Title);
            Assert.AreEqual("Upgrade · 600 Timber · 50 Scrap · 10m", card.UpgradeLabel);
            Assert.IsFalse(card.CanDemolish);
        }

        [Test]
        public void Frame_RealResources_AreNotDemo()
        {
            // Act
            var frame = VillagePresenter.Frame(Fresh());

            // Assert
            Assert.AreEqual(500, frame.Timber.Value);
            Assert.AreEqual(1000, frame.Timber.Cap);
            Assert.IsFalse(frame.Timber.IsDemo || frame.Peat.IsDemo || frame.Scrap.IsDemo || frame.Devotion.IsDemo);
            Assert.IsTrue(frame.Pennies.IsDemo && frame.Comfort.IsDemo && frame.TensionIsDemo);
        }

        [Test]
        public void Duration_Values_ReadLikeAClock()
        {
            Assert.AreEqual("30s", VillagePresenter.Duration(30));
            Assert.AreEqual("5m", VillagePresenter.Duration(300));
            Assert.AreEqual("1h 05m", VillagePresenter.Duration(3900));
            Assert.AreEqual("0s", VillagePresenter.Duration(-4));
        }
    
        [Test]
        public void Card_BuildingUnderConstruction_SaysBeingBuiltInsteadOfAnUpgrade()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
            var snapshot = state.Snapshot(T0.AddSeconds(5));
            var id = snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;

            // Act
            var card = VillagePresenter.Card(snapshot, BuildingTable.Default, id);

            // Assert
            Assert.AreEqual("Being built", card.UpgradeLabel);
            Assert.AreEqual(string.Empty, card.Output);
            Assert.AreEqual(string.Empty, card.Ready);
        }

        [Test]
        public void IsNewer_OlderVersionOrTime_IsIgnored()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            var before = state.Snapshot(T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
            var after = state.Snapshot(T0);

            // Act & Assert
            Assert.IsTrue(VillagePresenter.IsNewer(after, before));
            Assert.IsFalse(VillagePresenter.IsNewer(before, after), "a slow poll from before a command must not roll the UI back");
            Assert.IsTrue(VillagePresenter.IsNewer(state.Snapshot(T0.AddSeconds(1)), after));
            Assert.IsTrue(VillagePresenter.IsNewer(after, null));
        }
    }
}
