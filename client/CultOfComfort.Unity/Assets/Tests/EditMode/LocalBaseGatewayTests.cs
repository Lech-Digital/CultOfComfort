using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class LocalBaseGatewayTests
    {
        internal sealed class FakeClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        }

        internal sealed class MemoryStore : IBaseStore
        {
            public BaseData Saved;
            public int Saves;
            public bool ThrowOnLoad;
            public bool ThrowIo;
            public int Loads;
            public TaskCompletionSource<BaseData> Pending;
            public Task<BaseData> LoadAsync(CancellationToken ct)
            {
                Loads++;
                if (ThrowIo) throw new System.IO.IOException("disk on fire");
                if (ThrowOnLoad) throw new InvalidBaseDataException("corrupt");
                return Pending != null ? Pending.Task : Task.FromResult(Saved);
            }
            public Task SaveAsync(BaseData data, CancellationToken ct) { Saved = data; Saves++; return Task.CompletedTask; }
        }

        private static LocalBaseGateway Gateway(MemoryStore store, FakeClock clock, Action<string> warn = null) =>
            new LocalBaseGateway(store, clock, BuildingTable.Default, VillageMap.Demo, warn ?? (_ => { }));

        [Test]
        public async Task LoadAsync_NoSave_StartsAFreshVillage()
        {
            // Act
            var snapshot = await Gateway(new MemoryStore(), new FakeClock()).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.AreEqual(500, snapshot.Stock.Timber);
        }

        [Test]
        public async Task SendAsync_Accepted_SavesAndReturnsTheNewSnapshot()
        {
            // Arrange
            var store = new MemoryStore();
            var gateway = Gateway(store, new FakeClock());

            // Act
            var result = await gateway.SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), CancellationToken.None);

            // Assert
            Assert.AreEqual(CommandStatus.Accepted, result.Outcome.Status);
            Assert.AreEqual(330, result.Snapshot.Stock.Scrap);
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(1, VillageMap.Demo.ToBattleLayout(result.Snapshot, BuildingTable.Default).Towers.Count);
        }

        [Test]
        public async Task SendAsync_Rejected_DoesNotSave()
        {
            // Arrange
            var store = new MemoryStore();

            // Act
            await Gateway(store, new FakeClock()).SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(0, 1)), CancellationToken.None);

            // Assert
            Assert.AreEqual(0, store.Saves);
        }

        [Test]
        public async Task LoadAsync_SavedVillage_ContinuesFromTheSave()
        {
            // Arrange
            var store = new MemoryStore();
            var clock = new FakeClock();
            await Gateway(store, clock).SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddHours(1).AddSeconds(30);

            // Act — a new gateway (next app launch) reads the save.
            var snapshot = await Gateway(store, clock).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(100, snapshot.BuildingAt(new GridPosition(3, 9)).Ready);
        }

        [Test]
        public async Task LoadAsync_CorruptSave_StartsFreshAndWarns()
        {
            // Arrange
            var warnings = 0;

            // Act
            var snapshot = await Gateway(new MemoryStore { ThrowOnLoad = true }, new FakeClock(), _ => warnings++).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.AreEqual(1, warnings);
        }
    
        [Test]
        public async Task LoadAsync_TwoFirstLoadsAtOnce_ReadTheStoreOnce()
        {
            // Arrange — the store answers slowly, as a disk (or a server) would.
            var store = new MemoryStore { Pending = new TaskCompletionSource<BaseData>() };
            var gateway = Gateway(store, new FakeClock());

            // Act
            var first = gateway.LoadAsync(CancellationToken.None);
            var second = gateway.LoadAsync(CancellationToken.None);
            store.Pending.SetResult(null);
            await Task.WhenAll(first, second);

            // Assert
            Assert.AreEqual(1, store.Loads);
        }

        [Test]
        public async Task LoadAsync_StoreThrowsIOException_StartsFreshAndWarns()
        {
            // Arrange
            var warnings = 0;

            // Act
            var snapshot = await Gateway(new MemoryStore { ThrowIo = true }, new FakeClock(), _ => warnings++).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.AreEqual(1, warnings);
        }
    }
}
