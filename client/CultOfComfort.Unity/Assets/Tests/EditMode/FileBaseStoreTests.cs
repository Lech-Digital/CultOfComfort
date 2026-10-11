using System;
using System.IO;
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
    }
}
