using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateDamageTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Field = new(3, 9);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    private static (BaseState State, int Id) Built(BuildingType type, GridPosition? at = null)
    {
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, at ?? Field), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        return (state, state.Snapshot(T0).Buildings.Single(b => b.Type == type).Id);
    }

    private static void Wreck(BaseState state, int id, DateTimeOffset at, int night = 1) =>
        state.Apply(new RecordNightResult(Guid.NewGuid(), night, 0, new[] { id }), at).Status.Should().Be(CommandStatus.Accepted);

    [Fact]
    public void Default_Fence_IsCheapInstantUnlimited()
    {
        // Act
        var fence = BuildingTable.Default.Spec(BuildingType.WattleFence);

        // Assert
        fence.Levels[0].Cost.Should().Be(new ResourceBag(15, 0, 0, 0));
        fence.Levels[0].BuildSeconds.Should().Be(0);
        fence.Levels[0].Health.Should().Be(50);
        fence.MaxCount(1).Should().Be(int.MaxValue);
        fence.IsTower.Should().BeFalse();
        BuildingTable.Default.Version.Should().Be("village-2026.10.2");
    }

    [Fact]
    public void Place_Fence_IsInstantWhileTheBuilderIsBusyAndBlocksTheRoad()
    {
        // Arrange — the builder is busy with a Woodcutter.
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(4, 6)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.WattleFence).Level.Should().Be(1);
        state.ToBattleLayout().Obstacles.Should().Contain(new GridPosition(4, 6));
    }

    [Fact]
    public void ToBattleLayout_Buildings_AreStructuresWithLevelHealth()
    {
        // Arrange — a finished Woodcutter, a tower, and a Storage Hut still being built.
        var (state, woodcutter) = Built(BuildingType.Woodcutter);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(4, 9)), T0);

        // Act
        var structures = state.ToBattleLayout().Structures;

        // Assert
        structures.Should().HaveCount(3, "every building but the Hearth");
        structures.Single(s => s.Id == woodcutter).Should().Be(new PlacedStructure(woodcutter, Field, 60, 60));
        structures.Single(s => s.Position == new GridPosition(4, 6)).MaxHealth.Should().Be(80);
        structures.Single(s => s.Position == new GridPosition(4, 9)).MaxHealth.Should().Be(30, "still being built");
        VillageMap.Demo.ToBattleLayout(state.Snapshot(T0), BuildingTable.Default).Structures.Should().BeEquivalentTo(structures);
    }

    [Fact]
    public void RecordNightResult_WreckedIds_MarkThoseBuildingsWrecked()
    {
        // Arrange
        var (state, id) = Built(BuildingType.Woodcutter);

        // Act
        Wreck(state, id, T0.AddMinutes(1));

        // Assert
        state.Snapshot(T0.AddMinutes(1)).Building(id).Wrecked.Should().BeTrue();
        state.ToBattleLayout().Structures.Single(s => s.Id == id).Health.Should().Be(0);
    }

    [Fact]
    public void Wrecked_Woodcutter_ProducesNothing()
    {
        // Arrange — an hour of output, then wrecked.
        var (state, id) = Built(BuildingType.Woodcutter);
        var at = T0.AddHours(1);
        Wreck(state, id, at);

        // Act
        var later = state.Snapshot(at.AddHours(3)).Building(id);

        // Assert
        later.Ready.Should().Be(100, "what was made before the raid is still there; nothing since");
        later.PerHour.Should().Be(0);
    }

    [Fact]
    public void Wrecked_StorageHut_LosesItsCapBonusButKeepsStock()
    {
        // Arrange
        var (state, id) = Built(BuildingType.StorageHut);
        state.Snapshot(T0).Caps.Timber.Should().Be(1500);

        // Act
        Wreck(state, id, T0.AddMinutes(1));

        // Assert
        var snapshot = state.Snapshot(T0.AddMinutes(1));
        snapshot.Caps.Timber.Should().Be(1000);
        snapshot.Stock.Timber.Should().Be(400, "stock is never taken away by a lower cap");
    }

    [Fact]
    public void Repair_WreckedWoodcutter_Costs10PercentAndWorksAgain()
    {
        // Arrange
        var (state, id) = Built(BuildingType.Woodcutter);
        Wreck(state, id, T0.AddMinutes(1));
        var at = T0.AddMinutes(2);

        // Act
        var outcome = state.Apply(new RepairBuilding(Guid.NewGuid(), id), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var snapshot = state.Snapshot(at.AddHours(1));
        snapshot.Building(id).Wrecked.Should().BeFalse();
        snapshot.Stock.Timber.Should().Be(450 - 5);
        snapshot.Building(id).Ready.Should().Be(1 + 100, "1 unit made before the raid, then an hour from the repair");
    }

    [Fact]
    public void Repair_NotWrecked_Rejected()
    {
        // Arrange
        var (state, id) = Built(BuildingType.Woodcutter);

        // Act
        var outcome = state.Apply(new RepairBuilding(Guid.NewGuid(), id), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Repair_NotEnough_RejectedAndNothingCharged()
    {
        // Arrange — a Woodcutter wrecked in a village with no Timber left.
        var (state, id) = Built(BuildingType.Woodcutter);
        Wreck(state, id, T0.AddMinutes(1));
        var broke = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData() with { Stock = new ResourceBag(0, 60, 380, 0) });

        // Act
        var outcome = broke.Apply(new RepairBuilding(Guid.NewGuid(), id), T0.AddMinutes(2));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        broke.Snapshot(T0.AddMinutes(2)).Stock.Should().Be(new ResourceBag(0, 60, 380, 0));
    }

    [Fact]
    public void Restore_SaveWithWreckedBuilding_RoundTrips()
    {
        // Arrange
        var (state, id) = Built(BuildingType.Woodcutter);
        Wreck(state, id, T0.AddMinutes(1));

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(T0.AddMinutes(1)).Building(id).Wrecked.Should().BeTrue();
    }

    [Fact]
    public void RepairCost_TowerAndHut_TenPercentRoundedUp()
    {
        BuildingTable.Default.RepairCost(BuildingType.SlurryChute, 1).Should().Be(new ResourceBag(0, 0, 5, 0));
        BuildingTable.Default.RepairCost(BuildingType.PeatCut, 1).Should().Be(new ResourceBag(8, 0, 1, 0));
        BuildingTable.Default.RepairCost(BuildingType.WattleFence, 1).Should().Be(new ResourceBag(2, 0, 0, 0));
    }
}
