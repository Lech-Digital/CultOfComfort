using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class WorldModeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    private static (BaseState State, DateTimeOffset Now) WorldVillage(int hearth = 2)
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        var now = T0;
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
        }

        state.Apply(new JoinWorld(Guid.NewGuid(), 12, -7), now).Status.Should().Be(CommandStatus.Accepted);
        return (state, now);
    }

    [Fact]
    public void SendWorldMissionary_WithinReach_ChargesAndComesHomeOnTime()
    {
        // Arrange — 3 hexes out (20 minutes each way), 2 hours of preaching.
        var (state, now) = WorldVillage();
        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        var before = state.Snapshot(now).Stock;

        // Act
        var outcome = state.Apply(new SendWorldMissionary(Guid.NewGuid(), 1234, 2, 3), now);
        var charged = before - state.Snapshot(now).Stock;
        var away = state.Snapshot(now.AddMinutes(60 + 120 + 59)).Domain!.Missionaries;
        var home = state.Snapshot(now.AddMinutes(60 + 120 + 60)).Domain!.Missionaries;

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        charged.Should().Be(new ResourceBag(50, 0, 0, 20));
        away.Should().ContainSingle().Which.Phase.Should().Be(MissionaryPhase.Returning);
        home.Should().BeEmpty();
    }

    [Fact]
    public void SendWorldMissionary_BeyondReach_Rejected()
    {
        // Arrange — Hearth 2 reaches 3 hexes.
        var (state, now) = WorldVillage();

        // Act
        var outcome = state.Apply(new SendWorldMissionary(Guid.NewGuid(), 1234, 2, 4), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("Too far");
    }

    [Fact]
    public void SendWorldMissionary_NotOnAWorld_Rejected()
    {
        // Arrange
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);

        // Act
        var outcome = state.Apply(new SendWorldMissionary(Guid.NewGuid(), 1, 1, 1), T0.AddHours(2));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void SendMissionary_OnAWorld_TheMarchlandsAreClosed()
    {
        // Arrange
        var (state, now) = WorldVillage();

        // Act
        var outcome = state.Apply(new SendMissionary(Guid.NewGuid(), 1, 1), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void WorldUpdate_HigherRate_TensionRisesFasterFromNowOn()
    {
        // Arrange
        var (plain, now) = WorldVillage();
        var (world, _) = WorldVillage();

        // Act — 10 Tension an hour from the world, plus a spike of 5.
        world.Apply(new WorldUpdate(Guid.NewGuid(), 10_000, 5), now).Status.Should().Be(CommandStatus.Accepted);
        var plainRise = plain.Snapshot(now.AddHours(1)).Tension - plain.Snapshot(now).Tension;
        var worldRise = world.Snapshot(now.AddHours(1)).Tension - world.Snapshot(now).Tension;
        var spiked = world.Snapshot(now).Tension - plain.Snapshot(now).Tension;

        // Assert
        (worldRise - plainRise).Should().Be(10);
        spiked.Should().Be(5);
    }

    [Fact]
    public void ReceiveOfferings_StoresNearlyFull_KeepsOnlyWhatFits()
    {
        // Arrange — a rich village, some room left for Timber.
        var (state, now) = WorldVillage();
        var stock = state.Snapshot(now).Stock;
        var room = state.Caps.Timber - stock.Timber;

        // Act
        var outcome = state.Apply(new ReceiveOfferings(Guid.NewGuid(), new ResourceBag(room + 40, 0, 0, 0)), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Timber.Should().Be(room);
        state.Snapshot(now).Stock.Timber.Should().Be(state.Caps.Timber);
    }

    [Fact]
    public void JoinWorld_Twice_Rejected()
    {
        // Arrange
        var (state, now) = WorldVillage();

        // Act
        var outcome = state.Apply(new JoinWorld(Guid.NewGuid(), 1, 1), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ToData_WorldVillageWithMissionaryOut_RestoresTheSame()
    {
        // Arrange
        var (state, now) = WorldVillage();
        state.Apply(new WorldUpdate(Guid.NewGuid(), 4_000, 0), now);
        state.Apply(new SendWorldMissionary(Guid.NewGuid(), 77, 1, 2), now);

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, state.ToData());
        var later = now.AddHours(3);

        // Assert
        restored.InWorld.Should().BeTrue();
        restored.Snapshot(later).Tension.Should().Be(state.Snapshot(later).Tension);
        restored.Snapshot(now).Domain!.Missionaries.Should().BeEquivalentTo(state.Snapshot(now).Domain!.Missionaries);
    }
}
