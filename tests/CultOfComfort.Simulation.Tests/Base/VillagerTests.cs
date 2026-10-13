using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class VillagerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BuildingTable Table(int peat = 900, int hearthBeds = 2) =>
        new() { StartingStock = new ResourceBag(5000, peat, 1000, 300), HearthBeds = hearthBeds, StarterStockMinutes = 0 }; // staffing maths, no starter pile

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
        lean.Ready.Should().Be(0, "nobody to swing the axe");
        peat.Ready.Should().Be(25);
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
        snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Ready.Should().Be(150, "it worked until the fire went out");
        snapshot.Population.Should().Be(2, "nobody leaves");
    }

    [Fact]
    public void Upkeep_PeatBackInStorage_WorkResumes()
    {
        // Arrange — three villagers, a peat trench and a lean-to; the store starts with 3 Peat (cold after an hour).
        var state = NewBase(Table(peat: 3, hearthBeds: 3));
        Build(state, BuildingType.PeatCut, 12, 3, rotated: true);
        Build(state, BuildingType.Woodcutter, 15, 5);
        var cold = T0.AddHours(2);
        state.Snapshot(cold).Cold.Should().BeTrue();
        var peat = state.Snapshot(cold).Buildings.Single(b => b.Type == BuildingType.PeatCut).Id;
        var lean = state.Snapshot(cold).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        var readyWhileCold = state.Snapshot(cold).Buildings.Single(b => b.Id == lean).Ready;

        // Act — collect the peat the trench cut before the cold.
        state.Apply(new Collect(Guid.NewGuid(), peat), cold);

        // Assert — an hour of work after warming up.
        var warm = state.Snapshot(cold.AddHours(1));
        warm.Cold.Should().BeFalse();
        warm.Buildings.Single(b => b.Id == lean).Ready.Should().Be(readyWhileCold + 100);
    }

    [Fact]
    public void Restore_SaveFromBeforeVillagers_FillsTheBeds()
    {
        // Arrange
        var data = NewBase(new BuildingTable()).ToData() with { LayoutVersion = 2, Population = 0 };

        // Act
        var restored = BaseState.Restore(new BuildingTable(), Map, data);

        // Assert
        restored.Snapshot(T0).Population.Should().Be(4);
    }

    [Fact]
    public void Restore_Villagers_RoundTrip()
    {
        // Arrange
        var state = NewBase(Table(peat: 3));
        Build(state, BuildingType.Cottage, 12, 3);
        var later = T0.AddHours(3);
        var before = state.Snapshot(later);

        // Act
        var restored = BaseState.Restore(Table(peat: 3), Map, state.ToData());
        var after = restored.Snapshot(later);

        // Assert
        (after.Population, after.Beds, after.Cold, after.Stock.Peat).Should().Be((before.Population, before.Beds, before.Cold, before.Stock.Peat));
    }

    [Fact]
    public void Upkeep_Cold_PeatCuttersKeepCutting()
    {
        // Arrange — regression from the device: cold stopped the peat cutters too, so the village could never warm up.
        var state = NewBase(Table(peat: 1));
        Build(state, BuildingType.PeatCut, 12, 3, rotated: true);
        Build(state, BuildingType.Woodcutter, 15, 5);

        // Act — cold after half an hour; then four hours later.
        var snapshot = state.Snapshot(T0.AddHours(4));

        // Assert
        snapshot.Cold.Should().BeTrue();
        snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut).Ready.Should().Be(100, "they cut peat to get warm");
        snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Ready.Should().Be(0, "nobody else works in the cold");
    }
}
