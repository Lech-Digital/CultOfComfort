using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 roads and facings: paving the raiders' road.</summary>
public class RaidersRoadTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static BaseState Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    [Fact]
    public void Pave_HearthTwo_GravelForItsCostAndALittleComfort()
    {
        // Arrange
        var state = Village(2);
        var before = state.Snapshot(T0);
        var gravel = BuildingTable.Default.RaidersRoadPavings[0];

        // Act
        var outcome = state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(T0);
        after.RaidersRoad.Should().Be(1);
        (before.Stock - after.Stock).Should().Be(gravel.Cost);
        (after.Comfort - before.Comfort).Should().Be(gravel.Comfort);
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData()).Snapshot(T0).RaidersRoad.Should().Be(1, "kept in the save");
    }

    [Fact]
    public void Pave_CobblesBeforeHearthThree_Rejected()
    {
        // Arrange
        var state = Village(2);
        state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);

        // Act
        var outcome = state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("Hearth level 3");
        state.Snapshot(T0).RaidersRoad.Should().Be(1);
    }

    [Fact]
    public void Pave_AllLaid_Rejected()
    {
        // Arrange
        var state = Village(3);
        state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(T0).RaidersRoad.Should().Be(2);
    }

    [Fact]
    public void Pave_DuringANight_Rejected()
    {
        // Arrange
        var state = Village(2);
        state.Apply(new BeginNight(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Pave_Cobbled_TheNightPlanIsUnchanged()
    {
        // Arrange — two villages alike but for the road.
        var plain = Village(3);
        var paved = Village(3);
        paved.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);
        paved.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        paved.Apply(new PaveRaidersRoad(Guid.NewGuid()), T0);

        // Act
        var a = plain.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;
        var b = paved.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert — the look changes, the Night doesn't.
        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public void Restore_OldSave_TroddenMud()
    {
        // Arrange
        var data = Village(2).ToData() with { RaidersRoad = 0 };

        // Act & Assert
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(T0).RaidersRoad.Should().Be(0);
    }
}
