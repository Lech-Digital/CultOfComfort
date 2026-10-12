using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class StoneWallTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Spot = new(13, 4);

    private static (BaseState State, int Wall) WallAtHearth(int hearth)
    {
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(2000, 900, 900, 300) }, VillageMap.Demo, T0);
        if (hearth >= 2)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WoodenWall, Spot), T0).Status.Should().Be(CommandStatus.Accepted);
        return (state, state.Snapshot(T0).BuildingAt(Spot)!.Id);
    }

    [Fact]
    public void Upgrade_AtHearthOne_NeedsHearthTwo()
    {
        // Arrange
        var (state, wall) = WallAtHearth(1);

        // Act
        var outcome = state.Apply(new UpgradeBuilding(Guid.NewGuid(), wall), T0);

        // Assert
        outcome.Reason.Should().Be("Grow the Hearth to level 2 first.");
    }

    [Fact]
    public void Upgrade_AtHearthTwo_TurnsToStoneAtOnceEvenWithTheBuilderBusy()
    {
        // Arrange — the builder is busy with a cottage.
        var (state, wall) = WallAtHearth(2);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(20, 11)), T0).Status.Should().Be(CommandStatus.Accepted);
        var before = state.Snapshot(T0).Stock;

        // Act
        var outcome = state.Apply(new UpgradeBuilding(Guid.NewGuid(), wall), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Building(wall).Level.Should().Be(2);
        state.Snapshot(T0).Stock.Should().Be(before - new ResourceBag(10, 0, 20, 0));
        state.ToBattleLayout().Structures.Single(s => s.Id == wall).MaxHealth.Should().Be(600);
    }
}
