using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Against the real server (needs the internet): JsonUtility on this side, System.Text.Json on that side, one
    /// contract. Creates a throwaway guest. Run explicitly: -testPlatform PlayMode -testFilter LiveServerTests.
    /// </summary>
    [Explicit, Category("Live")]
    public class LiveServerTests
    {
        private sealed class MemoryCredentials : ICredentialStore
        {
            public string Token { get; private set; }

            public void Save(string token) => Token = token;

            public void Clear() => Token = null;
        }

        private sealed class MemoryCache : IRemoteCache
        {
            public RemoteCacheDto Load() => null;

            public void Save(RemoteCacheDto cache)
            {
            }
        }

        [UnityTest]
        public IEnumerator RemoteGateway_LiveServer_GuestBuildsCollectsAndLightsTheLamps() => Run(async () =>
        {
            // Arrange
            var gateway = new RemoteBaseGateway(new HttpVillageApi(VillageComposition.ServerUrl), new MemoryCredentials(), new MemoryCache(),
                new SystemClock(), BuildingTable.Default, VillageMap.Demo, m => UnityEngine.Debug.LogWarning(m));

            // Act
            Assert.IsTrue(await gateway.SyncNowAsync(CancellationToken.None), "guest + village");
            var placed = await gateway.SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), CancellationToken.None);
            Assert.IsTrue(await gateway.SyncNowAsync(CancellationToken.None), "the queue went through");
            var night = await gateway.SendAsync(new BeginNight(Guid.NewGuid()), CancellationToken.None);

            // Assert
            Assert.AreEqual(CommandStatus.Accepted, placed.Outcome.Status);
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.AreEqual(CommandStatus.Accepted, night.Outcome.Status, night.Outcome.Reason);
            Assert.IsNotNull(night.Outcome.Plan);
            Assert.AreEqual(1, night.Outcome.Plan.Towers.Count(), "the server saw the chute");
        });

        private static IEnumerator Run(Func<Task> body)
        {
            var task = body();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            task.GetAwaiter().GetResult();
        }
    }
}
