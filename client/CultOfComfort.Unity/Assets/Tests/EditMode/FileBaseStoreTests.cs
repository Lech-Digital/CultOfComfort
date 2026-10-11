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
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(4, 6)), T0);
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
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
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
    }
}
