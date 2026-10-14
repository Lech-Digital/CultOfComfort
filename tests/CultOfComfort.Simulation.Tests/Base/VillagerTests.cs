using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class VillagerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BuildingTable Table(int peat = 900, int hearthBeds = 2) =>
        new() { StartingStock = new ResourceBag(500, peat, 1000, 300), HearthBeds = hearthBeds, StarterStockMinutes = 0 }; // Timber under the cap, so production shows // staffing maths, no starter pile

    private static BaseState NewBase(BuildingTable? table = null) => BaseState.CreateNew(table ?? Table(), Map, T0);

    private static void Build(BaseState state, BuildingType type, int x, int y, bool rotated = false, DateTimeOffset? at = null)
    {
        var when = at ?? T0;
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y), rotated), when).Status.Should().Be(CommandStatus.Accepted, $"{type} at ({x},{y})");
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), when);
    }

    [Fact]
    public void Fresh_TheHearthHousesFour()
    {
        // Act
        var snapshot = NewBase(new BuildingTable()).Snapshot(T0);

        // Assert
        (snapshot.Population, snapshot.Beds).Should().Be((4, 4));
    }

    [Fact]
    public void Cottage_Finished_OneVillagerMovesInEveryTenMinutes()
    {
        // Arrange — a cottage beside the raiders' road (connected).
        var state = NewBase();
        Build(state, BuildingType.Cottage, 12, 3);

        // Act & Assert
        state.Snapshot(T0.AddMinutes(9)).Population.Should().Be(2);
        state.Snapshot(T0.AddMinutes(10)).Population.Should().Be(3);
        state.Snapshot(T0.AddMinutes(40)).Population.Should().Be(6);
        state.Snapshot(T0.AddDays(2)).Population.Should().Be(6, "never more than there are beds");
        state.Snapshot(T0.AddDays(2)).Beds.Should().Be(6);
    }

    [Fact]
    public void Cottage_Unconnected_HasNoBeds()
    {
        // Arrange
        var state = NewBase();
        Build(state, BuildingType.Cottage, 20, 14);

        // Act
        var snapshot = state.Snapshot(T0.AddHours(1));

        // Assert
        (snapshot.Population, snapshot.Beds).Should().Be((2, 2));
    }

    [Fact]
    public void Cottage_Demolished_TheExtraVillagersLeave()
    {
        // Arrange
        var state = NewBase();
        Build(state, BuildingType.Cottage, 12, 3);
        var later = T0.AddHours(1);
        var cottage = state.Snapshot(later).Buildings.Single(b => b.Type == BuildingType.Cottage).Id;

        // Act
        state.Apply(new DemolishBuilding(Guid.NewGuid(), cottage), later);

        // Assert
        state.Snapshot(later).Population.Should().Be(2);
    }

    [Fact]
    public void Staffing_TooFewVillagers_TheOlderBuildingGetsTheCrew()
    {
        // Arrange — a peat trench (needs 2) built before a lean-to (needs 1); only 2 villagers.
        var state = NewBase();
        Build(state, BuildingType.PeatCut, 12, 3, rotated: true);
        Build(state, BuildingType.Woodcutter, 15, 5);

        // Act
        var snapshot = state.Snapshot(T0.AddHours(1));

        // Assert
        var peat = snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut);
        var lean = snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter);
        (peat.Crew, peat.CrewNeeded).Should().Be((2, 2));
        (lean.Crew, lean.CrewNeeded).Should().Be((0, 1));
        lean.PerHour.Should().Be(0, "nobody to swing the axe");
        peat.PerHour.Should().Be(25);
    }

    [Fact]
    public void Upkeep_EachVillager_BurnsAPeatAnHour()
    {
        // Arrange
        var state = NewBase(Table(peat: 60));

        // Act
        var stock = state.Snapshot(T0.AddHours(10)).Stock.Peat;

        // Assert
        stock.Should().Be(40);
    }

    [Fact]
    public void Upkeep_PeatRunsOut_EveryoneDownsTools()
    {
        // Arrange — 3 Peat for 2 villagers: cold after 90 minutes.
        var state = NewBase(Table(peat: 3));
        Build(state, BuildingType.Woodcutter, 15, 5);

        // Act
        var snapshot = state.Snapshot(T0.AddHours(3));

        // Assert
        snapshot.Cold.Should().BeTrue();
        snapshot.Stock.Peat.Should().Be(0);
        snapshot.Stock.Timber.Should().Be(500 - 50 + 150, "it worked until the fire went out, 90 minutes in");
        snapshot.Population.Should().Be(2, "nobody leaves");
    }

    [Fact]
    public void Upkeep_PeatBought_WarmsUpAndWorkResumes()
    {
        // Arrange — three villagers and a lean-to; 3 Peat in store: cold after an hour.
        var state = NewBase(Table(peat: 3, hearthBeds: 3));
        Build(state, BuildingType.Woodcutter, 15, 5);
        var cold = T0.AddHours(2);
        state.Snapshot(cold).Cold.Should().BeTrue();
        var timberWhileCold = state.Snapshot(cold).Stock.Timber;

        // Act — 10 Peat from the trolley market (more than an hour's burn).
        state.Apply(new Trade(Guid.NewGuid(), ResourceType.Timber, ResourceType.Peat, 10), cold).Status.Should().Be(CommandStatus.Accepted);

        // Assert — an hour of work after warming up.
        var warm = state.Snapshot(cold.AddHours(1));
        warm.Cold.Should().BeFalse();
        warm.Stock.Timber.Should().Be(timberWhileCold - 50 + 100);
    }

    [Fact]
    public void Upkeep_PeatCutKeepsUp_TheVillageNeverGoesCold()
    {
        // Arrange — two villagers burn 2 an hour; the trench cuts 25 straight into the store.
        var state = NewBase(Table(peat: 1));
        Build(state, BuildingType.PeatCut, 12, 3, rotated: true);

        // Act
        var snapshot = state.Snapshot(T0.AddHours(4));

        // Assert
        snapshot.Cold.Should().BeFalse();
        snapshot.Stock.Peat.Should().Be(1 + 4 * (25 - 2));
    }

    [Fact]
    public void Upkeep_TooManyMouths_GoesColdThenRelightsOnceAnHoursPeatIsStacked()
    {
        // Arrange — 30 villagers burn 30 an hour; the trench cuts 25: 10 Peat lasts 2 hours.
        var state = NewBase(Table(peat: 10, hearthBeds: 30));
        Build(state, BuildingType.PeatCut, 12, 3, rotated: true);

        // Act — while cold the cutters keep cutting (25/h) and nothing burns: 30 stacked after 72 minutes.
        var stillCold = state.Snapshot(T0.AddHours(2).AddMinutes(71));
        var relit = state.Snapshot(T0.AddHours(2).AddMinutes(73));

        // Assert
        stillCold.Cold.Should().BeTrue();
        relit.Cold.Should().BeFalse();
        relit.Stock.Peat.Should().BeInRange(29, 30);
    }
}
