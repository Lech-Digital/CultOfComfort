using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateEconomyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Field = new(3, 9);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    private static CommandOutcome Send(BaseState state, BaseCommand command, DateTimeOffset at) => state.Apply(command, at);

    /// <summary>Places a Woodcutter at T0 and lets its 30 s construction finish.</summary>
    private static (BaseState State, int Id) BaseWithWoodcutter()
    {
        var state = NewBase();
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        return (state, id);
    }

    [Fact]
    public void CreateNew_FreshVillage_HasTheHearthAndStartingStock()
    {
        // Act
        var snapshot = NewBase().Snapshot(T0);

        // Assert
        snapshot.HearthLevel.Should().Be(1);
        snapshot.Stock.Should().Be(new ResourceBag(500, 60, 380, 0));
        snapshot.Caps.Should().Be(new ResourceBag(1000, 1000, 1000, 1000));
        snapshot.Buildings.Should().ContainSingle(b => b.Type == BuildingType.Hearth && b.Position == VillageMap.Demo.Hearth);
        snapshot.Version.Should().Be(0);
    }

    [Fact]
    public void Place_Woodcutter_ChargesTimberAndStartsConstruction()
    {
        // Act
        var (state, id) = BaseWithWoodcutter();
        var snapshot = state.Snapshot(T0.AddSeconds(10));

        // Assert
        snapshot.Stock.Timber.Should().Be(450);
        snapshot.Building(id).Level.Should().Be(0);
        snapshot.Building(id).UpgradeEndsAtUtc.Should().Be(T0.AddSeconds(30));
        snapshot.ConstructionEndsAtUtc.Should().Be(T0.AddSeconds(30));
        snapshot.Version.Should().Be(1);
    }

    [Fact]
    public void Snapshot_AfterConstructionAndOneHour_HasProducedFromTheEndTime()
    {
        // Arrange — built at T0+30s, producing 100 Timber/h from then.
        var (state, id) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddSeconds(30).AddHours(1));

        // Assert
        snapshot.Building(id).Level.Should().Be(1);
        snapshot.Building(id).Ready.Should().Be(100);
        snapshot.Building(id).Capacity.Should().Be(800, "8 hours of output");
        snapshot.ConstructionEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void Snapshot_LongAbsence_BufferStopsAtCapacity()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddDays(3));

        // Assert
        snapshot.Building(id).Ready.Should().Be(800);
    }

    [Fact]
    public void Snapshot_ClockBehindAsOf_NothingIsLost()
    {
        // Arrange — an hour of production, then the device clock jumps back a day.
        var (state, id) = BaseWithWoodcutter();
        var later = T0.AddSeconds(30).AddHours(1);
        state.Snapshot(later);

        // Act
        var snapshot = state.Snapshot(later.AddDays(-1));

        // Assert
        snapshot.Building(id).Ready.Should().BeGreaterThanOrEqualTo(0);
        state.Snapshot(later).Building(id).Ready.Should().Be(100);
    }

    [Fact]
    public void Collect_OneBuilding_MovesItsBufferIntoStock()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();
        var at = T0.AddSeconds(30).AddHours(2);

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Should().Be(new ResourceBag(200, 0, 0, 0));
        var snapshot = state.Snapshot(at);
        snapshot.Stock.Timber.Should().Be(650);
        snapshot.Building(id).Ready.Should().Be(0);
    }

    [Fact]
    public void Collect_StorageNearlyFull_MovesOnlyTheSpaceAndKeepsTheRest()
    {
        // Arrange — 450 Timber after building; 8 h = 800 ready; cap 1000 leaves room for 550.
        var (state, id) = BaseWithWoodcutter();
        var at = T0.AddDays(1);

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), at);

        // Assert
        outcome.Collected.Timber.Should().Be(550);
        var snapshot = state.Snapshot(at);
        snapshot.Stock.Timber.Should().Be(1000);
        snapshot.Building(id).Ready.Should().Be(250);
    }

    [Fact]
    public void Collect_NothingReady_IsRejected()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), T0.AddSeconds(31));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Version.Should().Be(1);
    }

    [Fact]
    public void Snapshot_UpgradeFinishedWhileAway_ProducesFromItsEndTime()
    {
        // Arrange — the Hearth must be level 2 before a building can reach level 2; start with 900 Timber to afford it.
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 60, 380, 0) }, VillageMap.Demo, T0);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        var built = T0.AddSeconds(30);
        Send(state, new UpgradeHearth(Guid.NewGuid()), built).Status.Should().Be(CommandStatus.Accepted);
        var hearthDone = built.AddMinutes(10);
        Send(state, new Collect(Guid.NewGuid(), id), hearthDone); // empty the buffer at a known time
        Send(state, new UpgradeBuilding(Guid.NewGuid(), id), hearthDone).Status.Should().Be(CommandStatus.Accepted);
        var upgradeDone = hearthDone.AddMinutes(5);

        // Act — the app was closed for an hour after the upgrade finished.
        var snapshot = state.Snapshot(upgradeDone.AddHours(1));

        // Assert — 0.666 left after collecting 16, + 5 min at 100/h while upgrading (8.333) + 1 h at 170/h = 178.999.
        snapshot.Building(id).Level.Should().Be(2);
        snapshot.Building(id).Ready.Should().Be(8 + 170);
    }

    [Fact]
    public void Place_WhileTheBuilderIsBusy_IsRejected()
    {
        // Arrange
        var (state, _) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(4, 9)), T0.AddSeconds(5));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("builder");
    }

    [Fact]
    public void Place_TowerWhileTheBuilderIsBusy_IsInstantAndAllowed()
    {
        // Arrange
        var (state, _) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0.AddSeconds(5));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var chute = state.Snapshot(T0.AddSeconds(5)).Buildings.Single(b => b.Type == BuildingType.SlurryChute);
        chute.Level.Should().Be(1);
        state.Snapshot(T0.AddSeconds(5)).Stock.Scrap.Should().Be(330);
    }

    [Fact]
    public void Place_NotEnoughResources_IsRejectedWithWhatIsMissing()
    {
        // Arrange — starting Timber 500; a Storage Hut costs 100, so place five... use a cheap path: spend down first.
        var state = NewBase();
        var at = T0;
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(3, 9)), at);
        at = at.AddMinutes(1);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.CultistHut, new GridPosition(4, 9)), at);
        at = at.AddMinutes(2);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(6, 9)), at);
        at = at.AddMinutes(1); // Timber left: 500 − 100 − 120 − 80 = 200; Scrap 370
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(7, 9)), at);
        at = at.AddSeconds(30); // 150 Timber left
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(8, 9)), at);
        at = at.AddSeconds(30); // 100 left

        // Act — upgrading the Hearth needs 600 Timber and 50 Scrap.
        var outcome = Send(state, new UpgradeHearth(Guid.NewGuid()), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("500 Timber");
    }

    [Fact]
    public void FinishConstructionNow_DuringAnUpgrade_CompletesIt()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new FinishConstructionNow(Guid.NewGuid()), T0.AddSeconds(2));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0.AddSeconds(2)).Building(id).Level.Should().Be(1);
    }
}
