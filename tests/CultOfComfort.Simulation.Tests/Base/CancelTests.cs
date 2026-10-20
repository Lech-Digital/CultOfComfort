using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Cancelling a build or a study (owner 2026-10-16: "we have no way to cancel the build or research").</summary>
public class CancelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static (BaseState State, DateTimeOffset Now) HearthTwo()
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        return (state, T0);
    }

    [Fact]
    public void CancelConstruction_NewBuilding_TakenDownAndPaidBack()
    {
        // Arrange
        var (state, now) = HearthTwo();
        var before = state.Snapshot(now).Stock;
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), now).Status.Should().Be(CommandStatus.Accepted);
        var leanTo = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Woodcutter);

        // Act
        var outcome = state.Apply(new CancelConstruction(Guid.NewGuid(), leanTo.Id), now.AddSeconds(10));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Should().Be(BuildingTable.Default.Spec(BuildingType.Woodcutter).Levels[0].Cost);
        var after = state.Snapshot(now.AddSeconds(10));
        after.Buildings.Should().NotContain(b => b.Type == BuildingType.Woodcutter);
        after.Stock.Timber.Should().Be(before.Timber);
        after.ConstructionEndsAtUtc.Should().BeNull("the builder is free again");
    }

    [Fact]
    public void CancelConstruction_Upgrade_StaysAtItsLevelAndPaidBack()
    {
        // Arrange — the Hearth on its way to level 3.
        var (state, now) = HearthTwo();
        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
        var hearth = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Hearth);

        // Act
        var outcome = state.Apply(new CancelConstruction(Guid.NewGuid(), hearth.Id), now.AddMinutes(5));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(now.AddHours(2));
        after.HearthLevel.Should().Be(2, "cancelled: it never finishes");
        after.Buildings.Single(b => b.Type == BuildingType.Hearth).IsUpgrading.Should().BeFalse();
    }

    [Fact]
    public void CancelConstruction_NothingBeingBuilt_Rejected()
    {
        // Arrange
        var (state, now) = HearthTwo();
        var hearth = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Hearth);

        // Act
        var outcome = state.Apply(new CancelConstruction(Guid.NewGuid(), hearth.Id), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Apply(new CancelConstruction(Guid.NewGuid(), 999), now).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void CancelConstruction_FullStores_RefundStopsAtTheCap()
    {
        // Arrange — started, then the stores filled up meanwhile.
        var (state, now) = HearthTwo();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), now);
        var leanTo = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Woodcutter);
        state.Apply(new DebugFillStores(Guid.NewGuid()), now);

        // Act
        var outcome = state.Apply(new CancelConstruction(Guid.NewGuid(), leanTo.Id), now);

        // Assert
        outcome.Collected.IsZero.Should().BeTrue();
        state.Snapshot(now).Stock.Timber.Should().Be(state.Snapshot(now).Caps.Timber);
    }

    [Fact]
    public void CancelResearch_Studying_StopsAndPaysBack()
    {
        // Arrange
        var (state, now) = HearthTwo();
        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        state.Apply(new StartResearch(Guid.NewGuid(), "finders-keepers"), now).Status.Should().Be(CommandStatus.Accepted);
        var paid = state.Snapshot(now).Stock;

        // Act
        var outcome = state.Apply(new CancelResearch(Guid.NewGuid()), now.AddMinutes(1));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var tech = state.Snapshot(now.AddHours(2)).Research!.Single(t => t.Tech.Id == "finders-keepers");
        tech.State.Should().Be(TechState.Available, "never finished");
        state.Snapshot(now.AddMinutes(1)).Stock.Devotion.Should().BeGreaterThan(paid.Devotion);
    }

    [Fact]
    public void CancelResearch_NothingStudied_Rejected()
    {
        // Arrange
        var (state, now) = HearthTwo();

        // Act & Assert
        state.Apply(new CancelResearch(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Rejected);
    }
}

/// <summary>The ghost's road check is the village's own rule (found on the Pixel: a green ✓, then "Not connected").</summary>
public class WouldConnectTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(15, 5)]
    [InlineData(20, 14)]
    [InlineData(3, 9)]
    public void WouldConnect_AnySpot_MatchesTheVillageAfterPlacing(int x, int y)
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        var spec = BuildingTable.Default.Spec(BuildingType.Woodcutter);
        var tiles = Footprint.Tiles(spec, new GridPosition(x, y), false);
        var predicted = VillageMap.Demo.WouldConnect(state.Snapshot(T0), tiles);

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(x, y)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Assert
        state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Connected.Should().Be(predicted);
    }
}

/// <summary>The first Night teaches (found on the Pixel 2026-10-16: lost at 1★ even with the Chute and a Gate).</summary>
public class FirstNightTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BeginNight_TheFirst_IsSmall()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Budget.Should().Be(BaseState.FirstNightBudget);
        plan.Waves.Should().Be(BaseState.FirstNightWaves);
    }

    [Fact]
    public void BeginNight_TheSecond_IsTheUsualSize()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);
        state.Apply(new DebugEndShield(Guid.NewGuid()), T0);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Night.Should().Be(2);
        plan.Waves.Should().Be(4);
        plan.Budget.Should().Be(TensionTable.Default.BasePower(1) / 2);
    }

    [Fact]
    public void FirstNight_AChuteBesideTheRoad_WinsWithNobodyPlaying()
    {
        // Arrange — the errand's Chute beside the raiders' road, nothing else, nobody touching the screen.
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(10, 9)), T0).Status.Should().Be(CommandStatus.Accepted);
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Act
        var result = new CultOfComfort.Simulation.Combat.Battle(plan.ToBattleSetup(VillageMap.Demo, Array.Empty<CultOfComfort.Simulation.Combat.PlayerInput>())).RunToCompletion();

        // Assert
        result.Stars.Should().BeGreaterThanOrEqualTo(2);
    }
}
