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

            public readonly Dictionary<string, string> Codes = new Dictionary<string, string>();

            public Task<string> RecoveryCodeAsync(string token, CancellationToken ct)
            {
                if (Down || !Villages.ContainsKey(token))
                {
                    return Task.FromResult<string>(null);
                }

                var code = "CODE-" + Codes.Count;
                Codes[code] = token;
                return Task.FromResult(code);
            }

            public Task<WorldDto> JoinWorldAsync(string token, CancellationToken ct)
            {
                if (Down || !Villages.TryGetValue(token, out var data))
                {
                    return Task.FromResult<WorldDto>(null);
                }

                var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
                state.Apply(new JoinWorld(Guid.NewGuid(), 5, -3), Now);
                Villages[token] = state.ToData();
                return Task.FromResult(new WorldDto { worldId = 1, seed = 20261014, cultId = token, q = 5, r = -3, viewQ = 5, viewR = -3, viewRadius = 8 });
            }

            public Task<WorldDto> GetWorldAsync(string token, int q, int r, int radius, CancellationToken ct) =>
                Task.FromResult(Down ? null : new WorldDto { worldId = 1, seed = 20261014, cultId = token, q = 5, r = -3, viewQ = q, viewR = r, viewRadius = radius });

            public Task<WorldCollectDto> CollectWorldAsync(string token, CancellationToken ct) =>
                Task.FromResult(Down ? null : new WorldCollectDto { reason = "No offerings yet." });

            /// <summary>Google subjects the fake knows: an ID token "google:x" signs in as subject x.</summary>
            public readonly Dictionary<string, string> GoogleVillages = new Dictionary<string, string>();

            public Task<GuestDto> GoogleSignInAsync(string token, string idToken, CancellationToken ct)
            {
                if (Down || idToken == null || !idToken.StartsWith("google:"))
                {
                    return Task.FromResult<GuestDto>(null);
                }

                var subject = idToken.Substring(7);
                if (!GoogleVillages.TryGetValue(subject, out var owner))
                {
                    GoogleVillages[subject] = owner = token; // links this guest village
                }

                return Task.FromResult(new GuestDto { playerId = owner, token = owner });
            }

            public Task<GuestDto> RecoverAsync(string code, CancellationToken ct)
            {
                if (Down || !Codes.TryGetValue(code, out var oldToken))
                {
                    return Task.FromResult<GuestDto>(null);
                }

                var newToken = oldToken + "-moved";
                Villages[newToken] = Villages[oldToken];
                Villages.Remove(oldToken);
                return Task.FromResult(new GuestDto { playerId = Guid.NewGuid().ToString(), token = newToken });
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
        public async Task RecoverAsync_CodeFromAnotherPhone_ThisPhoneTakesOverThatVillage()
        {
            // Arrange — phone A builds a chute and shows its code; phone B has its own fresh village and a queued tap.
            var phoneA = Gateway();
            await phoneA.SyncNowAsync(None);
            await phoneA.SendAsync(Chute(), None);
            await phoneA.SyncNowAsync(None);
            var code = await phoneA.RecoveryCodeAsync(None);
            var phoneBCredentials = new MemoryCredentials();
            var phoneB = new RemoteBaseGateway(_server, phoneBCredentials, new MemoryCache(), _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            await phoneB.SyncNowAsync(None);
            _server.Down = true;
            await phoneB.SendAsync(Chute(9, 6), None);
            _server.Down = false;

            // Act
            var moved = await phoneB.RecoverAsync(code, None);
            var snapshot = await phoneB.LoadAsync(None);

            // Assert
            Assert.IsTrue(moved);
            Assert.AreEqual(0, phoneB.PendingCount, "phone B's own tap belonged to its old village");
            Assert.AreEqual(1, snapshot.Buildings.Count(b => b.Type == BuildingType.SlurryChute));
            Assert.AreEqual(new GridPosition(7, 6), snapshot.Buildings.Single(b => b.Type == BuildingType.SlurryChute).Position, "phone A's chute");
            StringAssert.EndsWith("-moved", phoneBCredentials.Token);
        }

        [Test]
        public async Task WorldAsync_VillageNotOnAWorld_JoinsAndTheMirrorLearnsWhere()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);

            // Act
            var world = await gateway.WorldAsync(null, null, 4, None);
            var snapshot = await gateway.LoadAsync(None);

            // Assert
            Assert.IsNotNull(world);
            Assert.AreEqual((5, -3), (world.viewQ, world.viewR), "the window opens around the village");
            Assert.AreEqual(4, world.viewRadius);
            Assert.AreEqual(new WorldLink(5, -3, 0), snapshot.Domain.World);
        }

        [Test]
        public async Task WorldAsync_Offline_Null()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            _server.Down = true;

            // Act
            var world = await gateway.WorldAsync(null, null, 4, None);

            // Assert
            Assert.IsNull(world);
        }

        [Test]
        public async Task SignInWithGoogleAsync_FirstTime_LinksThisVillageAndKeepsItsQueuedTaps()
        {
            // Arrange — a tap made offline, waiting to be sent.
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            _server.Down = true;
            await gateway.SendAsync(Chute(), None);
            _server.Down = false;

            // Act
            var ok = await gateway.SignInWithGoogleAsync("google:andrzej", None);
            var snapshot = await gateway.LoadAsync(None);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(1, snapshot.Buildings.Count(b => b.Type == BuildingType.SlurryChute), "the tap reached the village before the switch");
        }

        [Test]
        public async Task SignInWithGoogleAsync_OnAnotherPhone_ThatPhoneGetsTheAccountsVillage()
        {
            // Arrange — phone A signs in and builds a chute; phone B has its own fresh village.
            var phoneA = Gateway();
            await phoneA.SyncNowAsync(None);
            await phoneA.SendAsync(Chute(), None);
            await phoneA.SignInWithGoogleAsync("google:andrzej", None);
            var phoneB = new RemoteBaseGateway(_server, new MemoryCredentials(), new MemoryCache(), _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            await phoneB.SyncNowAsync(None);

            // Act
            var ok = await phoneB.SignInWithGoogleAsync("google:andrzej", None);
            var snapshot = await phoneB.LoadAsync(None);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(new GridPosition(7, 6), snapshot.Buildings.Single(b => b.Type == BuildingType.SlurryChute).Position, "phone A's chute");
        }

        [Test]
        public async Task SignInWithGoogleAsync_Refused_KeepsThisVillage()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            var token = _credentials.Token;

            // Act
            var ok = await gateway.SignInWithGoogleAsync("not-a-google-token", None);

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual(token, _credentials.Token);
        }

        [Test]
        public async Task RecoverAsync_UnknownCode_KeepsThisVillage()
        {
            // Arrange
            var gateway = Gateway();
            await gateway.SyncNowAsync(None);
            var token = _credentials.Token;

            // Act
            var moved = await gateway.RecoverAsync("NOPE-NOPE-NOPE", None);

            // Assert
            Assert.IsFalse(moved);
            Assert.AreEqual(token, _credentials.Token);
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
