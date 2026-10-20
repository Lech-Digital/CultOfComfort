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
    public void Snapshot_AfterConstructionAndOneHour_TheTimberIsAlreadyInTheStores()
    {
        // Arrange — built at T0+30s, producing 100 Timber/h from then, straight into the stores.
        var (state, id) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddSeconds(30).AddHours(1));

        // Assert
        snapshot.Building(id).Level.Should().Be(1);
        snapshot.Stock.Timber.Should().Be(450 + 25 + 100, "after building: the 25 starter pile and an hour's output");
        snapshot.Building(id).Ready.Should().Be(0, "nothing waits in the lean-to to be tapped");
        snapshot.ConstructionEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void Snapshot_LongAbsence_TheStoresStopAtTheirCap()
    {
        // Arrange
        var (state, _) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddDays(3));

        // Assert
        snapshot.Stock.Timber.Should().Be(1000);
    }

    [Fact]
    public void Snapshot_StoresFullThenSpent_ProductionPicksUpAgain()
    {
        // Arrange — full of Timber by T0 + 6 h; 100 spent at T0 + 8 h.
        var (state, _) = BaseWithWoodcutter();
        var spent = T0.AddHours(8);
        state.Snapshot(spent).Stock.Timber.Should().Be(1000);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(20, 11)), spent).Status.Should().Be(CommandStatus.Accepted);

        // Act — the hut (60 s) adds 500 room; half an hour later.
        var snapshot = state.Snapshot(spent.AddMinutes(30).AddSeconds(60));

        // Assert
        snapshot.Stock.Timber.Should().BeInRange(950, 951, "nothing was made while full, then 30 minutes (and a minute's building) at 100/h");
    }

    [Fact]
    public void Snapshot_ClockBehindAsOf_NothingIsLost()
    {
        // Arrange — an hour of production, then the device clock jumps back a day.
        var (state, _) = BaseWithWoodcutter();
        var later = T0.AddSeconds(30).AddHours(1);
        state.Snapshot(later);

        // Act
        var back = state.Snapshot(later.AddDays(-1));

        // Assert
        back.Stock.Timber.Should().Be(575);
        state.Snapshot(later).Stock.Timber.Should().Be(575);
    }

    [Fact]
    public void Snapshot_UpgradeFinishedWhileAway_ProducesAtTheNewRateFromItsEndTime()
    {
        // Arrange — the Hearth must be level 2 before a building can reach level 2; start with 900 Timber to afford it.
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 600, 380, 0) }, VillageMap.Demo, T0);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        var built = T0.AddSeconds(30);
        Send(state, new UpgradeHearth(Guid.NewGuid()), built).Status.Should().Be(CommandStatus.Accepted);
        var hearthDone = built.AddMinutes(10);
        Send(state, new UpgradeBuilding(Guid.NewGuid(), id), hearthDone).Status.Should().Be(CommandStatus.Accepted);
        var upgradeDone = hearthDone.AddMinutes(30); // level 2 takes half an hour (2026-10-20 long builds)

        // Act — the app was closed for an hour after the upgrade finished.
        var atEnd = state.Snapshot(upgradeDone).Stock.Timber;
        var anHourLater = state.Snapshot(upgradeDone.AddHours(1));

        // Assert — level 2, both its slots crewed (four villagers, no other work): the full 170.
        anHourLater.Building(id).Level.Should().Be(2);
        (anHourLater.Stock.Timber - atEnd).Should().BeInRange(169, 170);
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
        var outcome = Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), T0.AddSeconds(5));

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
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(20, 11)), at);
        at = at.AddMinutes(1);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.CultistHut, new GridPosition(23, 11)), at);
        at = at.AddMinutes(2);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(27, 13)), at);
        at = at.AddMinutes(1); // Timber left: 500 − 100 − 120 − 80 = 200; Scrap 370
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(20, 14)), at);
        at = at.AddSeconds(30); // 150 Timber left
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(23, 14)), at);
        at = at.AddSeconds(30); // 100 left

        // Act — upgrading the Hearth needs 450 Timber and 50 Scrap.
        var outcome = Send(state, new UpgradeHearth(Guid.NewGuid()), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("300 Timber", "the two lean-tos' starter piles (25 each) went straight into the stores");
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

    [Fact]
    public void Construction_NewProducerFinished_StartsWithAQuarterHourInStore()
    {
        // Arrange — the crew finds some deadwood on day one: it goes straight into the stores.
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        var timber = state.Snapshot(T0).Stock.Timber;

        // Act
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Assert
        state.Snapshot(T0).Stock.Timber.Should().Be(timber + 25, "a quarter of the Lean-to's 100/h");
    }

    [Fact]
    public void Construction_UpgradeFinished_AddsNoStarterStock()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new Collect(Guid.NewGuid(), null), T0);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;

        // Act
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Assert
        state.Apply(new Collect(Guid.NewGuid(), null), T0).Status.Should().Be(CommandStatus.Rejected, "nothing made yet");
    }

    [Fact]
    public void CreateNew_Default_StartsWithFourVillagersForTheFirstFourJobs()
    {
        // Act
        var snapshot = NewBase().Snapshot(T0);

        // Assert — Woodcutter 1 + Peat Cut 2 + Cultist Hut 1.
        snapshot.Population.Should().Be(4);
        snapshot.Beds.Should().Be(4);
    }
}
