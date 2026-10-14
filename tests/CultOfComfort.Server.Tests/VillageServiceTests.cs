using CultOfComfort.Server.Application;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NSubstitute;

namespace CultOfComfort.Server.Tests;

public class VillageServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 18, 0, 0, TimeSpan.Zero);
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly Guid _player = Guid.NewGuid();

    private VillageService Service(bool dev = false) =>
        new(_players, _villages, BuildingTable.Default, VillageMap.Demo, new FixedClock(Now), new VillageOptions { AllowDevelopmentCommands = dev });

    private BaseData Stored(long? version = null)
    {
        var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, Now.AddMinutes(-5)).ToData();
        data = version == null ? data : data with { Version = version.Value };
        _villages.FindAsync(_player, Arg.Any<CancellationToken>()).Returns(data);
        _villages.TryReplaceAsync(_player, data.Version, Arg.Any<BaseData>(), Arg.Any<CancellationToken>()).Returns(true);
        return data;
    }

    [Fact]
    public async Task StartGuestAsync_NewPlayer_StoresOnlyTheTokenHashAndAFreshVillage()
    {
        // Act
        var guest = await Service().StartGuestAsync(CancellationToken.None);

        // Assert
        guest.Token.Should().HaveLength(43, "32 random bytes, base64url without padding");
        await _players.Received(1).AddAsync(Arg.Is<Player>(p => p.Id == guest.PlayerId && p.TokenHash != guest.Token && p.TokenHash.Length == 64), Arg.Any<CancellationToken>());
        await _villages.Received(1).AddAsync(guest.PlayerId, Arg.Is<BaseData>(v => v.Version == 0 && v.Stock == BuildingTable.Default.StartingStock), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_TokenFromStartGuest_FindsThePlayerByItsHash()
    {
        // Arrange
        string? storedHash = null;
        await _players.AddAsync(Arg.Do<Player>(p => storedHash = p.TokenHash), Arg.Any<CancellationToken>());
        var service = Service();
        var guest = await service.StartGuestAsync(CancellationToken.None);
        _players.FindByTokenHashAsync(storedHash!, Arg.Any<CancellationToken>()).Returns(guest.PlayerId);

        // Act
        var found = await service.AuthenticateAsync(guest.Token, CancellationToken.None);

        // Assert
        found.Should().Be(guest.PlayerId);
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyToken_IsNobody()
    {
        // Act
        var found = await Service().AuthenticateAsync("  ", CancellationToken.None);

        // Assert
        found.Should().BeNull();
        await _players.DidNotReceiveWithAnyArgs().FindByTokenHashAsync(default!, default);
    }

    [Fact]
    public async Task SyncAsync_CommandsAtTheCurrentVersion_AppliesThemAtServerTimeAndSaves()
    {
        // Arrange
        var stored = Stored();
        var place = new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9));

        // Act
        var result = await Service().SyncAsync(_player, stored.Version, new BaseCommand[] { place }, CancellationToken.None);

        // Assert
        result!.Conflict.Should().BeFalse();
        result.Outcomes.Should().ContainSingle().Which.Outcome.Status.Should().Be(CommandStatus.Accepted);
        result.View.Village.Version.Should().Be(stored.Version + 1);
        result.View.Village.Stock.Timber.Should().Be(450);
        await _villages.Received(1).TryReplaceAsync(_player, stored.Version, Arg.Is<BaseData>(v => v.Version == stored.Version + 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_StaleVersion_IsAConflictAndNothingIsApplied()
    {
        // Arrange
        var stored = Stored(version: 7);

        // Act
        var result = await Service().SyncAsync(_player, 6, new BaseCommand[] { new UpgradeHearth(Guid.NewGuid()) }, CancellationToken.None);

        // Assert
        result!.Conflict.Should().BeTrue();
        result.View.Village.Should().Be(stored);
        await _villages.DidNotReceiveWithAnyArgs().TryReplaceAsync(default, default, default!, default);
    }

    [Fact]
    public async Task SyncAsync_SomeoneWroteFirst_IsAConflictWithTheirVillage()
    {
        // Arrange — the version matched when read, but the conditional write lost the race.
        var stored = Stored();
        _villages.TryReplaceAsync(_player, stored.Version, Arg.Any<BaseData>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await Service().SyncAsync(_player, stored.Version, new BaseCommand[] { new ClaimErrand(Guid.NewGuid()) }, CancellationToken.None);

        // Assert
        result!.Conflict.Should().BeTrue();
        result.Outcomes.Should().BeEmpty("nothing of the losing batch counts");
    }

    [Fact]
    public async Task SyncAsync_DevelopmentCommandOnARealServer_IsRefused()
    {
        // Arrange
        var stored = Stored();

        // Act
        var result = await Service(dev: false).SyncAsync(_player, stored.Version, new BaseCommand[] { new DebugRaiseTension(Guid.NewGuid(), 100) }, CancellationToken.None);

        // Assert
        var outcome = result!.Outcomes.Single().Outcome;
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("Not on a real server.");
        result.View.Village.TensionMilli.Should().Be(stored.TensionMilli);
    }

    [Fact]
    public async Task SyncAsync_SameCommandTwice_IsAppliedOnce()
    {
        // Arrange — a retry after a lost response carries the same id.
        var stored = Stored();
        var place = new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9));

        // Act
        var result = await Service().SyncAsync(_player, stored.Version, new BaseCommand[] { place, place }, CancellationToken.None);

        // Assert
        result!.Outcomes.Select(o => o.Outcome.Status).Should().Equal(CommandStatus.Accepted, CommandStatus.Duplicate);
        result.View.Village.Stock.Timber.Should().Be(450);
    }

    [Fact]
    public async Task SyncAsync_UnknownPlayer_IsNotFound()
    {
        // Act
        var result = await Service().SyncAsync(Guid.NewGuid(), 0, Array.Empty<BaseCommand>(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public class VillageOptionsTests
{
    [Theory]
    [InlineData("", "anything", false)]
    [InlineData("too-short", "too-short", false)]
    [InlineData("long-enough-key-0123456789", "long-enough-key-0123456789", true)]
    [InlineData("long-enough-key-0123456789", "long-enough-key-012345678X", false)]
    [InlineData("long-enough-key-0123456789", null, false)]
    public void IsDevKey_Presented_OnlyTheConfiguredLongKey(string configured, string? presented, bool expected)
    {
        // Act
        var result = new VillageOptions { DevKey = configured }.IsDevKey(presented);

        // Assert
        result.Should().Be(expected);
    }
}
