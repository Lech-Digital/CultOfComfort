using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class RoadGateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BaseState NewBase(int hearth = 1)
    {
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) }, Map, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        return state;
    }

    private static CommandOutcome Gate(BaseState state, int x, int y) =>
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, new GridPosition(x, y)), T0);

    [Fact]
    public void Gate_OnTheRaidersRoad_AllowedOneAtHearthOne()
    {
        // Arrange
        var state = NewBase();

        // Act
        var first = Gate(state, 10, 5);
        var second = Gate(state, 17, 5);
        var fence = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(17, 6)), T0);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        second.Reason.Should().Be("No room for another gate on the road until the Hearth grows.");
        fence.Reason.Should().Be("That's the raiders' road. Build beside it.", "only gates go on the road");
    }

    [Fact]
    public void Gate_OnTheRoad_DoesNotMoveTheRoad()
    {
        // Arrange
        var state = NewBase();
        Gate(state, 10, 5);

        // Act
        var layout = state.ToBattleLayout();

        // Assert
        layout.ResolveEnemyPath().Should().Equal(Map.Road);
        layout.Structures.Single().Should().Match<PlacedStructure>(s => s.Barrier && s.Gate && s.MaxHealth == 200);
    }

    [Fact]
    public void Gate_AtTheEntranceOrOnTheHearth_Refused()
    {
        // Act & Assert
        Gate(NewBase(), 2, 1).Reason.Should().Be("Too close to where the raiders come in.");
        Gate(NewBase(), 17, 9).Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Gate_Levels_IronBoundAtHearthTwoGatehouseAtThree()
    {
        // Arrange
        var state = NewBase(hearth: 3);
        Gate(state, 10, 5);
        var gate = state.Snapshot(T0).BuildingAt(new GridPosition(10, 5))!.Id;

        // Act
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), gate), T0).Status.Should().Be(CommandStatus.Accepted);
        var iron = state.ToBattleLayout().Structures.Single(s => s.Id == gate).MaxHealth;
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), gate), T0).Status.Should().Be(CommandStatus.Accepted);
        var house = state.ToBattleLayout().Structures.Single(s => s.Id == gate).MaxHealth;

        // Assert
        (iron, house).Should().Be((450, 900));
        Gate(state, 17, 5).Status.Should().Be(CommandStatus.Accepted, "three road gates at Hearth 3");
    }

    [Fact]
    public void Night_WithARoadGate_TheRaidHacksAtItOnTheFixedRoad()
    {
        // Arrange — regression: the Night plan built its battle without the fixed road, so raiders walked round the gate.
        // Hearth 1: one road, so tonight's is the one with the gate.
        var state = NewBase(hearth: 1);
        Gate(state, 10, 5);
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Act
        var battle = new CultOfComfort.Simulation.Combat.Battle(plan.ToBattleSetup(Map, Array.Empty<CultOfComfort.Simulation.Combat.PlayerInput>()));
        var hits = 0;
        while (!battle.IsFinished)
        {
            battle.Step();
            hits += battle.LastTickEvents.OfType<CultOfComfort.Simulation.Combat.StructureHit>().Count();
        }

        // Assert
        battle.Path.Should().Equal(Map.Road);
        hits.Should().BeGreaterThan(0, "they hack at the gate across the road");
    }

    [Fact]
    public void Place_GateAloneInAField_Refused()
    {
        // Arrange
        var t0 = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, t0);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, new GridPosition(14, 4)), t0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be(VillageMap.GateMisplaced);
        VillageMap.Demo.PlacementProblem(state.Snapshot(t0), BuildingTable.Default.Spec(BuildingType.Gate), new GridPosition(14, 4), false).Should().Be(VillageMap.GateMisplaced, "the ghost says so too");
    }

    [Fact]
    public void Place_GateBesideAWall_Accepted()
    {
        // Arrange
        var t0 = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, t0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WoodenWall, new GridPosition(13, 4)), t0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, new GridPosition(14, 4)), t0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
    }
}
