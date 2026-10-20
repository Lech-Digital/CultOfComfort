using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 notables: named villagers who join at Hearth levels and work one building each.</summary>
public class NotableTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static (BaseState State, int Lean) Village()
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        return (state, state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id);
    }

    [Fact]
    public void Snapshot_HearthTwo_TwoHaveJoined()
    {
        // Act
        var notables = Village().State.Snapshot(T0).Notables!;

        // Assert
        notables.Select(n => n.Notable.Id).Should().BeEquivalentTo(new[] { "agnes", "ember" });
        notables.Should().OnlyContain(n => n.BuildingId == null, "idle until asked");
    }

    [Fact]
    public void AssignNotable_AgnesAtTheLeanTo_FortyPercentMoreTimber()
    {
        // Arrange
        var (state, lean) = Village();
        var before = state.Snapshot(T0).Building(lean).PerHour;

        // Act
        state.Apply(new AssignNotable(Guid.NewGuid(), "agnes", lean), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Building(lean).PerHour.Should().Be(before * 140 / 100);
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData()).Snapshot(T0).Notables!.Single(n => n.Notable.Id == "agnes").BuildingId.Should().Be(lean);
    }

    [Fact]
    public void AssignNotable_TheWrongTrade_Rejected()
    {
        // Arrange
        var (state, lean) = Village();

        // Act & Assert — Sister Ember works towers, not lean-tos.
        state.Apply(new AssignNotable(Guid.NewGuid(), "ember", lean), T0).Reason.Should().Contain("isn't much use there");
    }

    [Fact]
    public void AssignNotable_NotJoinedYet_Rejected()
    {
        // Arrange
        var (state, lean) = Village();

        // Act & Assert — Brother Hammer joins at Hearth 3.
        state.Apply(new AssignNotable(Guid.NewGuid(), "hammer", lean), T0).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void PlanNight_EmberOnATower_ItsManned()
    {
        // Arrange
        var (state, _) = Village();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(3, 10)), T0).Status.Should().Be(CommandStatus.Accepted);
        var chute = state.Snapshot(T0).BuildingAt(new GridPosition(3, 10))!.Id;
        state.Apply(new AssignNotable(Guid.NewGuid(), "ember", chute), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Towers.Single(t => t.Position == new GridPosition(3, 10)).Manned.Should().BeTrue();
    }
}
