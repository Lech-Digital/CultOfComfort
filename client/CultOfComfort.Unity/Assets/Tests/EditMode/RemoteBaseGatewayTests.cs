using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class RemoteBaseGatewayTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        /// <summary>The server, in memory: the same rules as VillageService (apply at its clock, version check, no dev commands).</summary>
        private sealed class FakeServer : IVillageApi
        {
            private readonly LocalBaseGatewayTests.FakeClock _clock;
            public readonly Dictionary<string, BaseData> Villages = new Dictionary<string, BaseData>();
            public bool Down;
            public int Guests;
            public int Syncs;
            public TimeSpan Ahead = TimeSpan.Zero;

            public FakeServer(LocalBaseGatewayTests.FakeClock clock) => _clock = clock;

            private DateTimeOffset Now => _clock.UtcNow + Ahead;

            public Task<GuestDto> StartGuestAsync(CancellationToken ct)
            {
                if (Down)
                {
                    return Task.FromResult<GuestDto>(null);
                }

                Guests++;
                var token = "token-" + Guests;
                Villages[token] = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, Now).ToData();
                return Task.FromResult(new GuestDto { playerId = Guid.NewGuid().ToString(), token = token });
            }

            public Task<ApiReply> GetAsync(string token, CancellationToken ct)
            {
                if (Down)
                {
                    return Task.FromResult(ApiReply.Offline);
                }

                return Task.FromResult(Villages.TryGetValue(token, out var data) ? new ApiReply(200, Response(data, new List<OutcomeDto>())) : new ApiReply(401, null));
            }

            public Task<ApiReply> SyncAsync(string token, SyncRequestDto request, CancellationToken ct)
            {
                if (Down)
                {
                    return Task.FromResult(ApiReply.Offline);
                }

                if (!Villages.TryGetValue(token, out var stored))
                {
                    return Task.FromResult(new ApiReply(401, null));
                }

                Syncs++;
                if (stored.Version != request.expectedVersion)
                {
                    return Task.FromResult(new ApiReply(409, Response(stored, new List<OutcomeDto>())));
                }

                var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, stored);
                var outcomes = new List<OutcomeDto>();
                foreach (var dto in request.commands)
                {
                    var command = CommandMapping.ToCommand(dto);
                    var outcome = CommandMapping.IsDevelopmentOnly(command) ? new CommandOutcome(CommandStatus.Rejected, state.Version, "Not on a real server.") : state.Apply(command, Now);
                    outcomes.Add(CommandMapping.ToDto(command.CommandId, outcome));
                }

                Villages[token] = state.ToData();
                return Task.FromResult(new ApiReply(200, Response(Villages[token], outcomes)));
            }

            /// <summary>Someone else (another device) changes the village behind the client's back.</summary>
            public void ChangeBehindTheClientsBack(string token, BaseCommand command)
            {
                var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, Villages[token]);
                Assert.AreEqual(CommandStatus.Accepted, state.Apply(command, Now).Status);
                Villages[token] = state.ToData();
            }

            private VillageResponseDto Response(BaseData data, List<OutcomeDto> outcomes) => new VillageResponseDto
            {
                version = data.Version, serverTimeTicks = Now.UtcTicks, village = VillageSaveMapping.ToDto(data), outcomes = outcomes,
            };
        }

        private sealed class MemoryCredentials : ICredentialStore
        {
            public string Token { get; private set; }

            public void Save(string token) => Token = token;

            public void Clear() => Token = null;
        }

        private sealed class MemoryCache : IRemoteCache
        {
            public RemoteCacheDto Saved;

            public RemoteCacheDto Load() => Saved == null ? null : UnityEngine.JsonUtility.FromJson<RemoteCacheDto>(UnityEngine.JsonUtility.ToJson(Saved));

            public void Save(RemoteCacheDto cache) => Saved = cache;
        }

        private LocalBaseGatewayTests.FakeClock _clock;
        private FakeServer _server;
        private MemoryCredentials _credentials;
        private MemoryCache _cache;

        [SetUp]
        public void SetUp()
        {
            _clock = new LocalBaseGatewayTests.FakeClock();
            _server = new FakeServer(_clock);
            _credentials = new MemoryCredentials();
            _cache = new MemoryCache();
        }

        private RemoteBaseGateway Gateway(List<string> warnings = null) =>
            new RemoteBaseGateway(_server, _credentials, _cache, _clock, BuildingTable.Default, VillageMap.Demo, m => warnings?.Add(m));

        private static PlaceBuilding Chute(int x = 7, int y = 6) => new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(x, y));

        private BaseState ServerVillage() => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, _server.Villages[_credentials.Token]);

        [Test]
        public async Task SyncNowAsync_NewDevice_StartsAGuestAndAdoptsTheServersVillage()
        {
            // Arrange
            var gateway = Gateway();

            // Act
            var synced = await gateway.SyncNowAsync(None);
            var snapshot = await gateway.LoadAsync(None);

            // Assert
            Assert.IsTrue(synced);
            Assert.IsTrue(gateway.Online);
            Assert.AreEqual("token-1", _credentials.Token);
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.IsTrue(_cache.Saved.hasVillage, "the server's village is cached for offline starts");
        }

        [Test]
        public async Task SendAsync_Online_AppliesAtOnceThenTheServerHasIt()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);

            // Act
            var result = await gateway.SendAsync(Chute(), None);
            var shownBeforeSync = result.Snapshot.Buildings.Count(b => b.Type == BuildingType.SlurryChute);
            await gateway.SyncNowAsync(None);

            // Assert
            Assert.AreEqual(CommandStatus.Accepted, result.Outcome.Status);
            Assert.AreEqual(1, shownBeforeSync, "no waiting on the network");
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.AreEqual(1, ServerVillage().Snapshot(_clock.UtcNow).Buildings.Count(b => b.Type == BuildingType.SlurryChute));
        }

        [Test]
        public async Task SendAsync_Offline_KeepsPlayingAndCatchesUpLater()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            _server.Down = true;

            // Act
            await gateway.SendAsync(Chute(7, 6), None);
            var offline = await gateway.SyncNowAsync(None);
            _server.Down = false;
            var back = await gateway.SyncNowAsync(None);

            // Assert
            Assert.IsFalse(offline);
            Assert.IsTrue(back);
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.AreEqual(1, ServerVillage().Snapshot(_clock.UtcNow).Buildings.Count(b => b.Type == BuildingType.SlurryChute));
        }

        [Test]
        public async Task SyncNowAsync_StaleVersion_AdoptsTheServerAndReplaysTheQueue()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            _server.ChangeBehindTheClientsBack(_credentials.Token, Chute(7, 6));
            await gateway.SendAsync(Chute(9, 6), None);

            // Act: the first exchange hits the conflict, the second goes through.
            await gateway.SyncNowAsync(None);
            await gateway.SyncNowAsync(None);
            var snapshot = await gateway.LoadAsync(None);

            // Assert
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.AreEqual(2, ServerVillage().Snapshot(_clock.UtcNow).Buildings.Count(b => b.Type == BuildingType.SlurryChute), "both survive");
            Assert.AreEqual(2, snapshot.Buildings.Count(b => b.Type == BuildingType.SlurryChute));
        }

        [Test]
        public async Task SendAsync_DevelopmentCommand_RefusedOnTheDevice()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);

            // Act
            var result = await gateway.SendAsync(new DebugRaiseTension(Guid.NewGuid(), 50), None);

            // Assert
            Assert.AreEqual(CommandStatus.Rejected, result.Outcome.Status);
            Assert.AreEqual(0, gateway.PendingCount);
        }

        [Test]
        public async Task SendAsync_BeginNightOffline_RejectedWithAReason()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            _server.Down = true;

            // Act
            var result = await gateway.SendAsync(new BeginNight(Guid.NewGuid()), None);

            // Assert
            Assert.AreEqual(CommandStatus.Rejected, result.Outcome.Status);
            StringAssert.Contains("connection", result.Outcome.Reason);
        }

        [Test]
        public async Task SendAsync_BeginNightOnline_TheServerDealsThePlan()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            await gateway.SendAsync(Chute(), None); // queued: goes first

            // Act
            var result = await gateway.SendAsync(new BeginNight(Guid.NewGuid()), None);

            // Assert
            Assert.AreEqual(CommandStatus.Accepted, result.Outcome.Status);
            Assert.IsNotNull(result.Outcome.Plan);
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.IsNotNull(ServerVillage().ToData().NightInProgress, "the server knows a Night is on");
            Assert.AreEqual(1, result.Outcome.Plan.Towers.Count, "the queued chute stands in tonight's plan");
        }

        [Test]
        public async Task LoadAsync_OfflineRestart_ComesBackFromTheCacheWithItsQueue()
        {
            // Arrange
            var first = Gateway();
            await first.SyncNowAsync(None);
            _server.Down = true;
            await first.SendAsync(Chute(), None);

            // Act
            var second = Gateway();
            var snapshot = await second.LoadAsync(None);

            // Assert
            Assert.AreEqual(1, second.PendingCount);
            Assert.AreEqual(1, snapshot.Buildings.Count(b => b.Type == BuildingType.SlurryChute));
        }

        [Test]
        public async Task SyncNowAsync_FirstLaunchOffline_PlaysLocallyThenMovesOntoANewServerVillage()
        {
            // Arrange
            _server.Down = true;
            var gateway = Gateway();
            await gateway.SendAsync(Chute(), None);
            Assert.IsFalse(await gateway.SyncNowAsync(None));

            // Act
            _server.Down = false;
            await gateway.SyncNowAsync(None); // guest + fresh server village
            await gateway.SyncNowAsync(None); // the queue

            // Assert
            Assert.AreEqual(0, gateway.PendingCount);
            Assert.AreEqual(1, ServerVillage().Snapshot(_clock.UtcNow).Buildings.Count(b => b.Type == BuildingType.SlurryChute));
        }

        [Test]
        public async Task LoadAsync_ServerClockAhead_VillageRunsOnServerTime()
        {
            // Arrange
            _server.Ahead = TimeSpan.FromMinutes(10);
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);

            // Act
            var snapshot = await gateway.LoadAsync(None);

            // Assert
            Assert.AreEqual(_clock.UtcNow + _server.Ahead, snapshot.AsOfUtc);
        }

        [Test]
        public async Task SyncNowAsync_ServerForgotTheDevice_DropsTheToken()
        {
            // Arrange
            var warnings = new List<string>();
            var gateway = Gateway(warnings);
            await gateway.SyncNowAsync(None);
            _server.Villages.Clear();

            // Act
            var synced = await gateway.SyncNowAsync(None);

            // Assert
            Assert.IsFalse(synced);
            Assert.IsNull(_credentials.Token);
            Assert.IsTrue(warnings.Any());
        }
    }
}
