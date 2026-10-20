using CultOfComfort.Server.Application;
using CultOfComfort.Server.Application.World;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CultOfComfort.Server.Tests;

public class WorldServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<Guid, BaseData> _stored = new();
    private readonly List<WorldRecord> _worlds = new();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IWorldRepository _worldRepo = Substitute.For<IWorldRepository>();
    private readonly MovableClock _clock = new(T0);

    public WorldServiceTests()
    {
        _villages.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci => _stored.TryGetValue(ci.Arg<Guid>(), out var v) ? v : null);
        _villages.TryReplaceAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<BaseData>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var id = ci.ArgAt<Guid>(0);
            if (!_stored.TryGetValue(id, out var current) || current.Version != ci.ArgAt<long>(1))
            {
                return false;
            }

            _stored[id] = ci.ArgAt<BaseData>(2);
            return true;
        });
        _worldRepo.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _worlds.ToList());
        _worldRepo.When(r => r.AddAsync(Arg.Any<WorldRecord>(), Arg.Any<CancellationToken>())).Do(ci => _worlds.Add(ci.Arg<WorldRecord>()));
        _worldRepo.TryReplaceAsync(Arg.Any<WorldRecord>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var next = ci.ArgAt<WorldRecord>(0);
            var index = _worlds.FindIndex(w => w.Id == next.Id && w.Version == ci.ArgAt<long>(1));
            if (index < 0)
            {
                return false;
            }

            _worlds[index] = next;
            return true;
        });
    }

    private WorldService World() =>
        new(_worldRepo, _villages, BuildingTable.Default, VillageMap.Demo, _clock, new WorldOptions(), NullLogger<WorldService>.Instance);

    private VillageService Villages(IWorldService world) =>
        new(Substitute.For<IPlayerRepository>(), _villages, BuildingTable.Default, VillageMap.Demo, _clock, new VillageOptions(), world);

    /// <summary>A Hearth 2 village with full stores, stored for the player.</summary>
    private Guid HearthTwoVillage()
    {
        var player = Guid.NewGuid();
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.GetUtcNow().AddHours(-3));
        var at = _clock.GetUtcNow().AddHours(-3);
        state.Apply(new DebugFillStores(Guid.NewGuid()), at);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), at).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new DebugFillStores(Guid.NewGuid()), _clock.GetUtcNow());
        _stored[player] = state.ToData();
        return player;
    }

    [Fact]
    public async Task JoinAsync_NewPlayer_OpensWorldOneAndPutsTheVillageOnIt()
    {
        // Arrange
        var player = HearthTwoVillage();

        // Act
        var cult = await World().JoinAsync(player, CancellationToken.None);

        // Assert
        cult.Should().NotBeNull();
        cult!.Name.Should().StartWith("Cult of ");
        _worlds.Should().ContainSingle().Which.Seed.Should().Be(new WorldOptions().Seed);
        _worlds.Single().Ledger.Cults.Count(c => c.IsBot).Should().Be(new WorldOptions().Bots, "a new world opens with its bots");
        _stored[player].Domain!.World.Should().Be(new WorldLink(cult.Q, cult.R, 0));
    }

    [Fact]
    public async Task JoinAsync_Twice_StaysWhereItIs()
    {
        // Arrange
        var player = HearthTwoVillage();
        var world = World();
        var first = await world.JoinAsync(player, CancellationToken.None);

        // Act
        var second = await world.JoinAsync(player, CancellationToken.None);

        // Assert
        second.Should().Be(first);
        _worlds.Single().Ledger.Cults.Count(c => !c.IsBot).Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_NoVillage_Null()
    {
        // Act
        var cult = await World().JoinAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        cult.Should().BeNull();
        _worlds.Should().BeEmpty();
    }

    [Fact]
    public async Task Sermon_SentThroughSyncThenTicked_LandsOnTheWorldAndRaisesTheVillagesTension()
    {
        // Arrange — on World 1, a missionary to the nearest place.
        var player = HearthTwoVillage();
        var world = World();
        var cult = (await world.JoinAsync(player, CancellationToken.None))!;
        var map = WorldGenerator.Generate(new WorldOptions().Seed);
        var place = map.Near(cult.Q, cult.R, 3).Where(n => n.Type is NodeType.Hamlet or NodeType.Village).OrderBy(n => n.DistanceTo(cult.Q, cult.R)).First();
        var send = new SendWorldMissionary(Guid.NewGuid(), place.Id, 4, place.DistanceTo(cult.Q, cult.R));
        var sync = await Villages(world).SyncAsync(player, _stored[player].Version, new BaseCommand[] { send }, CancellationToken.None);

        // Act — past the walk and the sermon, then a tick.
        _clock.Now = _clock.Now.AddHours(6);
        await world.TickAsync(CancellationToken.None);
        var window = await world.WindowAsync(player, null, null, 4, CancellationToken.None);

        // Assert
        sync!.Outcomes.Single().Outcome.Status.Should().Be(CommandStatus.Accepted);
        window!.Holdings.Should().Contain(h => h.NodeId == place.Id && h.CultId == cult.Id && h.Milli > 0);
        window.TensionPerHourMilli.Should().BeGreaterThan(0);
        _stored[player].Domain!.World!.TensionPerHourMilli.Should().Be(window.TensionPerHourMilli);
    }

    /// <summary>Preaches at the nearest hamlet, a sermon and a tick at a time, until the player's cult is its Patron.</summary>
    private async Task<bool> PreachUntilPatron(WorldService world, Guid player, WorldCult cult)
    {
        var map = WorldGenerator.Generate(new WorldOptions().Seed);
        var place = map.Near(cult.Q, cult.R, 3).Where(n => n.Type is NodeType.Hamlet).OrderBy(n => n.DistanceTo(cult.Q, cult.R)).First();
        for (var trip = 0; trip < 12; trip++)
        {
            var send = new SendWorldMissionary(Guid.NewGuid(), place.Id, 4, place.DistanceTo(cult.Q, cult.R));
            await Villages(world).SyncAsync(player, _stored[player].Version, new BaseCommand[] { new DebugFillStores(Guid.NewGuid()), send }, CancellationToken.None);
            _clock.Now = _clock.Now.AddHours(6);
            await world.TickAsync(CancellationToken.None);
            var window = (await world.WindowAsync(player, null, null, 4, CancellationToken.None))!;
            if (window.Holdings.Any(h => h.NodeId == place.Id && h.CultId == cult.Id && h.Patron))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public async Task Sermons_UntilPatronThenTicked_TheVillageKnowsAndThePatronErrandIsDone()
    {
        // Arrange — on World 1, on the Patron errand.
        var player = HearthTwoVillage();
        _stored[player] = _stored[player] with { ErrandId = "patron" };
        var world = World();
        var cult = (await world.JoinAsync(player, CancellationToken.None))!;

        // Act
        var patron = await PreachUntilPatron(world, player, cult);

        // Assert
        patron.Should().BeTrue("enough sermons make a Patron");
        _stored[player].Domain!.World!.Patronages.Should().Be(1);
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, _stored[player]).Snapshot(_clock.Now).Errand!.Done.Should().BeTrue();
    }

    [Fact]
    public async Task WindowAsync_VillageBehindTheLedger_CatchesUp()
    {
        // Arrange — a Patron whose village was never told (it became one before villages kept the count).
        var player = HearthTwoVillage();
        var world = World();
        var cult = (await world.JoinAsync(player, CancellationToken.None))!;
        (await PreachUntilPatron(world, player, cult)).Should().BeTrue();
        var stale = _stored[player];
        _stored[player] = stale with { Domain = stale.Domain! with { World = stale.Domain.World! with { Patronages = 0 } } };

        // Act
        var window = await world.WindowAsync(player, null, null, 4, CancellationToken.None);

        // Assert
        window!.Patronages.Should().Be(1);
        _stored[player].Domain!.World!.Patronages.Should().Be(1);
    }

    [Fact]
    public async Task WindowAsync_VillageAlreadyKnows_NothingApplied()
    {
        // Arrange
        var player = HearthTwoVillage();
        var world = World();
        await world.JoinAsync(player, CancellationToken.None);
        var version = _stored[player].Version;

        // Act
        await world.WindowAsync(player, null, null, 4, CancellationToken.None);

        // Assert
        _stored[player].Version.Should().Be(version);
    }

    [Fact]
    public async Task Sync_WorldMissionaryWithAWrongDistance_RejectedAndNotRecorded()
    {
        // Arrange
        var player = HearthTwoVillage();
        var world = World();
        var cult = (await world.JoinAsync(player, CancellationToken.None))!;
        var place = WorldGenerator.Generate(new WorldOptions().Seed).Near(cult.Q, cult.R, 3).First(n => n.DistanceTo(cult.Q, cult.R) >= 2);
        var lie = new SendWorldMissionary(Guid.NewGuid(), place.Id, 1, 1);

        // Act
        var sync = await Villages(world).SyncAsync(player, _stored[player].Version, new BaseCommand[] { lie }, CancellationToken.None);

        // Assert
        sync!.Outcomes.Single().Outcome.Status.Should().Be(CommandStatus.Rejected);
        _worlds.Single().Ledger.Missionaries.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_FullHamletForTwoHours_MovesThePurseIntoTheStores()
    {
        // Arrange — the player holds all of a hamlet, and the Timber store is empty.
        var player = HearthTwoVillage();
        var world = World();
        var cult = (await world.JoinAsync(player, CancellationToken.None))!;
        var hamlet = WorldGenerator.Generate(new WorldOptions().Seed).Nodes.First(n => n.Type == NodeType.Hamlet);
        var ledgerData = _worlds.Single().Ledger;
        _worlds[0] = _worlds[0] with { Ledger = ledgerData with { Holdings = new[] { new HoldingData(hamlet.Id, cult.Id, WorldLedger.Full) } } };
        world = World(); // reload with the holding
        _stored[player] = _stored[player] with { Stock = _stored[player].Stock with { Timber = 0 } };
        _clock.Now = _clock.Now.AddHours(2);

        // Act
        var collect = await world.CollectAsync(player, CancellationToken.None);

        // Assert
        collect!.Taken.Timber.Should().BeGreaterThan(0);
        collect.Village!.Village.Stock.Timber.Should().BeGreaterThanOrEqualTo(collect.Taken.Timber);
        (await world.WindowAsync(player, null, null, 2, CancellationToken.None))!.Purse.Timber.Should().BeLessThan(collect.Taken.Timber);
    }

    [Fact]
    public async Task Restart_NewServiceOverTheSameStore_RemembersTheCults()
    {
        // Arrange
        var player = HearthTwoVillage();
        var cult = await World().JoinAsync(player, CancellationToken.None);

        // Act
        var window = await World().WindowAsync(player, null, null, 3, CancellationToken.None);

        // Assert
        window!.Me.Should().Be(cult);
    }

    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
