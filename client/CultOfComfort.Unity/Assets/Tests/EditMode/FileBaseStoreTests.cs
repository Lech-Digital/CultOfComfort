using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class FileBaseStoreTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void Mapper_RoundTrip_KeepsTheVillage()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
            var data = state.ToData();

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(data));

            // Assert
            var at = T0.AddHours(3);
            var original = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(at);
            var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, back).Snapshot(at);
            Assert.AreEqual(original.Stock, restored.Stock);
            Assert.AreEqual(original.Buildings.Count, restored.Buildings.Count);
            Assert.AreEqual(original.Version, restored.Version);
            Assert.AreEqual(original.BuildingAt(new GridPosition(3, 9)).Ready, restored.BuildingAt(new GridPosition(3, 9)).Ready);
        }

        [Test]
        public void Mapper_UnknownBuildingType_Throws()
        {
            // Arrange
            var json = VillageSaveMapper.ToJson(BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData()).Replace("\"Hearth\"", "\"Spaceship\"");

            // Act & Assert
            Assert.Throws<InvalidBaseDataException>(() => VillageSaveMapper.FromJson(json));
        }

        [Test]
        public async Task FileStore_SaveThenLoad_ReadsWhatWasWritten()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), $"village-{Guid.NewGuid()}.json");
            var store = new FileBaseStore(path);
            var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData();

            // Act
            await store.SaveAsync(data, CancellationToken.None);
            var back = await store.LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(data.Stock, back.Stock);
            Assert.IsNull(await new FileBaseStore(path + ".missing").LoadAsync(CancellationToken.None));
        }
    
        [Test]
        public async Task FileStore_OnlyTheTempFileSurvived_LoadsIt()
        {
            // Arrange — the app died between removing the old save and moving the new one in.
            var path = Path.Combine(Path.GetTempPath(), $"village-{Guid.NewGuid()}.json");
            var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData();
            File.WriteAllText(path + ".tmp", VillageSaveMapper.ToJson(data));

            // Act
            var back = await new FileBaseStore(path).LoadAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(back);
            Assert.AreEqual(data.Stock, back.Stock);
        }

        [Test]
        public void Mapper_UnknownFormat_Throws()
        {
            // Arrange
            var json = VillageSaveMapper.ToJson(BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData()).Replace("\"format\":1", "\"format\":99");

            // Act & Assert
            Assert.Throws<InvalidBaseDataException>(() => VillageSaveMapper.FromJson(json));
        }
    
        [Test]
        public void Mapper_WreckedFlag_RoundTrips()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(7, 6)), T0);
            var fence = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.WattleFence).Id;
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { fence }), T0);

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(state.ToData()));

            // Assert
            Assert.IsTrue(BaseState.Restore(BuildingTable.Default, VillageMap.Demo, back).Snapshot(T0).Building(fence).Wrecked);
        }
    
        [Test]
        public void Mapper_RaidNightAndReport_RoundTrip()
        {
            // Arrange — a raid that played itself out (report), then a new Night begun.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), T0);
            state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);
            state.Snapshot(T0.AddDays(1));
            state.Apply(new BeginNight(Guid.NewGuid()), T0.AddDays(1));
            var at = T0.AddDays(1).AddMinutes(1);

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(state.ToData()));
            var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, back).Snapshot(at);
            var original = state.Snapshot(at);

            // Assert
            Assert.AreEqual(original.Tension, restored.Tension);
            Assert.AreEqual(original.ShieldEndsAtUtc, restored.ShieldEndsAtUtc);
            Assert.AreEqual(original.UnseenReport.StateHashHex, restored.UnseenReport.StateHashHex);
            Assert.AreEqual(original.NightInProgress.Seed, restored.NightInProgress.Seed);
            Assert.AreEqual(original.NightInProgress.Structures.Count, restored.NightInProgress.Structures.Count);
            Assert.AreEqual(original.NightInProgress.Towers[0], restored.NightInProgress.Towers[0]);
        }
    
        [Test]
        public void Mapper_ClearedTerrainAndTreeCuts_RoundTrip()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new ClearTerrain(Guid.NewGuid(), new GridPosition(8, 4)), T0);
            var tree = VillageMap.Demo.WildTrees.First(t => VillageMap.Demo.IsOpen(t, 1));
            state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);

            // Act
            var back = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(state.ToData())));

            // Assert
            CollectionAssert.Contains(back.Snapshot(T0.AddHours(1)).ClearedTerrain.ToList(), new GridPosition(8, 4));
            CollectionAssert.Contains(back.Snapshot(T0.AddHours(1)).ClearedTerrain.ToList(), tree);
            CollectionAssert.DoesNotContain(back.Snapshot(T0.AddHours(9)).ClearedTerrain.ToList(), tree, "it grew back");
        }

        [Test]
        public void Mapper_RotatedBuildingAndLayoutVersion_RoundTrip()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(13, 5), true), T0);

            // Act
            var data = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(state.ToData()));

            // Assert
            Assert.AreEqual(BaseState.CurrentLayoutVersion, data.LayoutVersion);
            var lean = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter);
            Assert.IsTrue(lean.Rotated);
            CollectionAssert.AreEquivalent(new[] { new GridPosition(13, 5), new GridPosition(13, 6) }, lean.Tiles.ToList());
        }

        [Test]
        public async Task FileStore_Quarantine_KeepsACopyOfTheUnreadableSave()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), $"village-{Guid.NewGuid()}.json");
            File.WriteAllText(path, "{ the old village }");
            var store = new FileBaseStore(path);

            // Act
            await store.QuarantineAsync(CancellationToken.None);
            await store.SaveAsync(BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData(), CancellationToken.None);

            // Assert
            var copies = Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".unreadable-*");
            Assert.AreEqual(1, copies.Length);
            Assert.AreEqual("{ the old village }", File.ReadAllText(copies[0]));
        }

        [Test]
        public void Mapper_MidErrand_KeepsTheErrandAndItsEvent()
        {
            // Arrange — the second errand (collect) is current and has happened.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            var data = state.ToData() with { ErrandIndex = 1, ErrandEventDone = true };

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(data));

            // Assert
            Assert.AreEqual(1, back.ErrandIndex);
            Assert.IsTrue(back.ErrandEventDone);
        }

        [Test]
        public void Mapper_NightInProgress_KeepsItsRoster()
        {
            // Arrange — a Night started at Hearth 1: the curious locals are coming.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new BeginNight(Guid.NewGuid()), T0);

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(state.ToData()));

            // Assert
            Assert.AreEqual(1, back.NightInProgress.Roster);
        }

        [Test]
        public void Mapper_NightInProgress_KeepsGateFootprintAndFlags()
        {
            // Arrange — a Night whose locked layout holds a two-tile gate.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new BeginNight(Guid.NewGuid()), T0);
            var data = state.ToData();
            var gate = new PlacedStructure(99, new GridPosition(5, 5), 40, 30)
            {
                Tiles = new[] { new GridPosition(5, 5), new GridPosition(6, 5) }, Barrier = true, Gate = true,
            };
            data = data with { NightInProgress = data.NightInProgress with { Structures = new[] { gate } } };

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(data)).NightInProgress.Structures.Single();

            // Assert
            Assert.AreEqual(30, back.Health);
            Assert.IsTrue(back.Barrier);
            Assert.IsTrue(back.Gate);
            CollectionAssert.AreEqual(gate.Tiles, back.Tiles);
        }
    }
}
