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
            Assert.IsTrue(frame.Pennies.IsDemo && frame.Comfort.IsDemo);
            Assert.IsFalse(frame.TensionIsDemo, "Tension is real since the raids slice");
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
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), T0);
            var after = state.Snapshot(T0);

            // Act & Assert
            Assert.IsTrue(VillagePresenter.IsNewer(after, before));
            Assert.IsFalse(VillagePresenter.IsNewer(before, after), "a slow poll from before a command must not roll the UI back");
            Assert.IsTrue(VillagePresenter.IsNewer(state.Snapshot(T0.AddSeconds(1)), after));
            Assert.IsTrue(VillagePresenter.IsNewer(after, null));
        }
    
        private static (BaseState State, int Id) WreckedWoodcutter()
        {
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { id }), T0);
            return (state, id);
        }

        [Test]
        public void Card_WreckedWoodcutter_OffersRepair()
        {
            // Arrange
            var (state, id) = WreckedWoodcutter();

            // Act
            var card = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, id);

            // Assert
            Assert.IsTrue(card.IsWrecked);
            Assert.AreEqual("Repair · 5 Timber", card.RepairLabel);
            Assert.IsTrue(card.CanRepair);
            Assert.AreEqual("Wrecked — repair first", card.UpgradeLabel);
            Assert.IsFalse(card.CanUpgrade);
        }

        [Test]
        public void BuildCards_Fence_IsAvailableAtHearthOne()
        {
            // Act
            var fence = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default).Single(c => c.Type == BuildingType.WattleFence);

            // Assert
            Assert.IsFalse(fence.Locked);
            Assert.AreEqual("15 Timber", fence.Cost);
            Assert.AreEqual("instant", fence.Time);
            Assert.AreEqual("b_wattle_fence", fence.Icon);
        }
    
        [Test]
        public void Card_WreckedTower_SaysItWontFire()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), T0);
            var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.SlurryChute).Id;
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { id }), T0);

            // Act
            var card = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, id);

            // Assert
            Assert.AreEqual("Wrecked — won't fire until repaired", card.Output);
        }
    
        [Test]
        public void TensionNote_States_ReadLikeTheSpec()
        {
            // Arrange
            var calm = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            var raided = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            raided.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);
            var punitive = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            punitive.Apply(new DebugRaiseTension(Guid.NewGuid(), 110), T0);
            var shielded = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            shielded.Apply(new BeginNight(Guid.NewGuid()), T0);
            shielded.Apply(new ResolveNight(Guid.NewGuid(), 1, Array.Empty<CultOfComfort.Simulation.Combat.PlayerInput>()), T0);

            // Act & Assert
            Assert.AreEqual("Calm", VillagePresenter.TensionNote(calm.Snapshot(T0)));
            Assert.AreEqual("Wulfric's men are coming · 7h 12m", VillagePresenter.TensionNote(raided.Snapshot(T0.AddMinutes(48))));
            Assert.AreEqual("Punitive raid · 8h 00m", VillagePresenter.TensionNote(punitive.Snapshot(T0)));
            Assert.AreEqual("Shielded · 11h 00m", VillagePresenter.TensionNote(shielded.Snapshot(T0.AddHours(1))));
            Assert.AreEqual("Face the raid · 7h 12m left", VillagePresenter.StartLabel(raided.Snapshot(T0.AddMinutes(48))));
            Assert.AreEqual("Light the Lamps (early)", VillagePresenter.StartLabel(calm.Snapshot(T0)));
            Assert.AreEqual("Shielded · 11h 00m", VillagePresenter.StartLabel(shielded.Snapshot(T0.AddHours(1))), "no early Night during the shield");
            var frame = VillagePresenter.Frame(raided.Snapshot(T0));
            Assert.IsFalse(frame.TensionIsDemo);
            Assert.AreEqual(75f / 150f, frame.Tension, 0.001f);
        }
    
        [Test]
        public void TerrainCard_WildTreeBoulderAndWall_ReadRight()
        {
            // Arrange
            var snapshot = Fresh();
            var tree = VillageMap.Demo.WildTrees.First(t => VillageMap.Demo.IsOpen(t, 1));

            // Act
            var treeCard = VillagePresenter.TerrainCard(snapshot, VillageMap.Demo, BuildingTable.Default, tree);
            var boulderCard = VillagePresenter.TerrainCard(snapshot, VillageMap.Demo, BuildingTable.Default, new GridPosition(8, 4));
            var wall = VillagePresenter.TerrainCard(snapshot, VillageMap.Demo, BuildingTable.Default, new GridPosition(5, 3));
            var nothing = VillagePresenter.TerrainCard(snapshot, VillageMap.Demo, BuildingTable.Default, new GridPosition(10, 10));

            // Assert
            Assert.AreEqual("Wild tree", treeCard.Title);
            Assert.AreEqual("Cut it down · +15 Timber", treeCard.UpgradeLabel);
            Assert.AreEqual("Boulder", boulderCard.Title);
            Assert.AreEqual("Clear it · +10 Scrap", boulderCard.UpgradeLabel);
            Assert.IsNull(wall, "bedrock has no card");
            Assert.IsNull(nothing);
        }

        [Test]
        public void Card_PlantedTree_SaplingThenReadyToCut()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Tree, new GridPosition(3, 9)), T0);
            var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Tree).Id;

            // Act
            var sapling = VillagePresenter.Card(state.Snapshot(T0.AddHours(1)), BuildingTable.Default, id);
            var grown = VillagePresenter.Card(state.Snapshot(T0.AddHours(5)), BuildingTable.Default, id);

            // Assert
            Assert.AreEqual("Tree · sapling", sapling.Title);
            Assert.AreEqual("Pull it up (nothing to show yet)", sapling.DemolishLabel);
            Assert.AreEqual("Tree · grown", grown.Title);
            Assert.AreEqual("Cut it down · +15 Timber", grown.DemolishLabel);
            Assert.IsFalse(grown.CanUpgrade);
        }

        [Test]
        public void BuildCards_Shapes_ShowTheirSize()
        {
            // Act
            var cards = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default);

            // Assert — a 1×1 says nothing; bigger ones say how much room they need.
            Assert.AreEqual("2×4", cards.Single(c => c.Type == BuildingType.PeatCut).Size);
            Assert.AreEqual("2×1", cards.Single(c => c.Type == BuildingType.Woodcutter).Size);
            Assert.AreEqual(string.Empty, cards.Single(c => c.Type == BuildingType.SlurryChute).Size);
        }

        [Test]
        public void BuildCards_WallAndGate_AvailableAtHearthOneWithTheirIcons()
        {
            // Act
            var cards = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default);

            // Assert
            var wall = cards.Single(c => c.Type == BuildingType.WoodenWall);
            var gate = cards.Single(c => c.Type == BuildingType.Gate);
            Assert.IsFalse(wall.Locked);
            Assert.AreEqual("30 Timber", wall.Cost);
            Assert.AreEqual("b_wooden_wall", wall.Icon);
            Assert.AreEqual("b_gate", gate.Icon);
        }

        [Test]
        public void Card_UnconnectedBuilding_SaysToBuildARoad()
        {
            // Arrange — a lean-to out in the field, nowhere near a road.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(20, 14)), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;

            // Act
            var card = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, id);

            // Assert
            Assert.AreEqual("Not connected — build a road to it", card.Output);
        }

        [Test]
        public void BuildCards_Roads_TrackAtHearthOnePlanksAndCobblesLater()
        {
            // Act
            var cards = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default);

            // Assert
            var track = cards.Single(c => c.Type == BuildingType.DirtTrack);
            Assert.IsFalse(track.Locked);
            Assert.AreEqual("b_dirt_track", track.Icon);
            Assert.AreEqual("Hearth 2", cards.Single(c => c.Type == BuildingType.PlankWalk).LockText);
            Assert.AreEqual("Hearth 3", cards.Single(c => c.Type == BuildingType.CobbledRoad).LockText);
        }

        [Test]
        public void Card_ProducerWithoutACrew_SaysWhoIsMissing()
        {
            // Arrange — a peat trench (crew 2) built first takes both villagers; the lean-to beside the road gets none.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(12, 3), true), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            var snapshot = state.Snapshot(T0);

            // Act
            var lean = VillagePresenter.Card(snapshot, BuildingTable.Default, snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id);
            var peat = VillagePresenter.Card(snapshot, BuildingTable.Default, snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut).Id);

            // Assert
            Assert.AreEqual("Needs 1 villager — build a Cottage", lean.Output);
            StringAssert.Contains("Crew 2/2", peat.Ready);
        }

        [Test]
        public void Frame_Villagers_ShowPeopleAndBedsAndTheCold()
        {
            // Arrange — 2 Peat for 2 villagers: cold after an hour.
            var table = new BuildingTable { StartingStock = new ResourceBag(500, 2, 380, 0) };
            var state = BaseState.CreateNew(table, VillageMap.Demo, T0);

            // Act
            var warm = VillagePresenter.Frame(state.Snapshot(T0));
            var cold = VillagePresenter.Frame(state.Snapshot(T0.AddHours(2)));

            // Assert
            Assert.AreEqual(2, warm.People.Value);
            Assert.AreEqual(2, warm.People.Cap);
            Assert.IsFalse(warm.Cold);
            Assert.IsTrue(cold.Cold);
        }

        [Test]
        public void BuildCards_Cottage_HasItsIconAndBeds()
        {
            // Act
            var cottage = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default).Single(c => c.Type == BuildingType.Cottage);

            // Assert
            Assert.AreEqual("b_cottage", cottage.Icon);
            Assert.AreEqual("2×2", cottage.Size);
        }

        [Test]
        public void Card_WoodenWall_OffersStoneAndThenIsAStoneWall()
        {
            // Arrange — Hearth 2, a wooden wall.
            var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(2000, 900, 900, 0) }, VillageMap.Demo, T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WoodenWall, new GridPosition(13, 4)), T0);
            var wall = state.Snapshot(T0).BuildingAt(new GridPosition(13, 4)).Id;

            // Act
            var wooden = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, wall);
            state.Apply(new UpgradeBuilding(Guid.NewGuid(), wall), T0);
            var stone = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, wall);

            // Assert
            Assert.AreEqual("Rebuild in stone · 10 Timber · 20 Scrap", wooden.UpgradeLabel);
            Assert.IsTrue(wooden.CanUpgrade);
            Assert.AreEqual("Stone Wall", stone.Title);
            Assert.AreEqual("Max level", stone.UpgradeLabel);
        }

        [Test]
        public void RepairAllLabel_WreckedBuildings_SumsTheirRepairs()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(13, 6)), T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(14, 6)), T0);
            var ids = state.Snapshot(T0).Buildings.Where(b => b.Type == BuildingType.WattleFence).Select(b => b.Id).ToList();
            var before = VillagePresenter.RepairAllLabel(state.Snapshot(T0), BuildingTable.Default);
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, ids), T0);

            // Act
            var label = VillagePresenter.RepairAllLabel(state.Snapshot(T0), BuildingTable.Default);

            // Assert
            Assert.IsNull(before, "nothing to repair, no button");
            Assert.AreEqual("Repair all (2) · 4 Timber", label);
        }

        [Test]
        public void Card_Gate_NamedByItsLevel()
        {
            // Arrange — Hearth 2 and a gate.
            var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(2000, 900, 900, 0) }, VillageMap.Demo, T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, new GridPosition(10, 5)), T0);
            var gate = state.Snapshot(T0).BuildingAt(new GridPosition(10, 5)).Id;

            // Act
            var wooden = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, gate);
            state.Apply(new UpgradeBuilding(Guid.NewGuid(), gate), T0);
            var iron = VillagePresenter.Card(state.Snapshot(T0), BuildingTable.Default, gate);

            // Assert
            Assert.AreEqual("Wooden Gate", wooden.Title);
            Assert.AreEqual("Bind it with iron · 120 Timber · 60 Scrap", wooden.UpgradeLabel);
            Assert.AreEqual("Iron-bound Gate", iron.Title);
            Assert.IsTrue(iron.CanMove);
        }

        [Test]
        public void Card_Cold_PeatCutStillWorksOthersDont()
        {
            // Arrange — 3 villagers, 1 Peat: cold within the hour.
            var table = new BuildingTable { StartingStock = new ResourceBag(2000, 1, 900, 0), HearthBeds = 3 };
            var state = BaseState.CreateNew(table, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(12, 3), true), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
            var snapshot = state.Snapshot(T0.AddHours(2));

            // Act
            var peat = VillagePresenter.Card(snapshot, table, snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut).Id);
            var lean = VillagePresenter.Card(snapshot, table, snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id);

            // Assert
            Assert.AreEqual("Cold — no Peat, nobody works but the peat cutters", lean.Output);
            StringAssert.StartsWith("+", peat.Output);
        }
    }
}
