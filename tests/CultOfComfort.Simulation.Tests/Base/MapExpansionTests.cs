using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class MapExpansionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseState RichBase() =>
        BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(5000, 600, 1000, 300) }, VillageMap.Demo, T0);

    [Fact]
    public void Demo_FullMap_IsRoomyAndGrowsWithTheHearth()
    {
        // Act — building the village is the game: 30×20 open at once, 38×26 at Hearth 2, 48×32 at Hearth 3.
        var map = VillageMap.Demo;

        // Assert
        (map.Width, map.Height).Should().Be((48, 32));
        map.IsOpen(new GridPosition(29, 19), 1).Should().BeTrue();
        map.IsOpen(new GridPosition(5, 22), 1).Should().BeFalse();
        map.IsOpen(new GridPosition(5, 22), 2).Should().BeTrue();
        map.IsOpen(new GridPosition(33, 3), 2).Should().BeTrue();
        map.IsOpen(new GridPosition(40, 3), 2).Should().BeFalse();
        map.IsOpen(new GridPosition(40, 3), 3).Should().BeTrue();
        map.IsOpen(new GridPosition(5, 30), 3).Should().BeTrue();
        Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new GridPosition(x, y)))
            .Count(t => map.IsOpen(t, 1) && !map.Rocks.Contains(t)).Should().BeGreaterThan(540, "lots of room from the start");
    }

    [Fact]
    public void Place_OnLandThatOpensAtHearthTwo_RejectedUntilThen()
    {
        // Arrange
        var state = RichBase();

        // Act
        var early = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(5, 22)), T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        var later = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(5, 22)), T0.AddMinutes(10));

        // Assert
        early.Reason.Should().Be("That land opens at Hearth 2.");
        later.Status.Should().Be(CommandStatus.Accepted);
    }

    [Fact]
    public void ToBattleLayout_EveryHearthLevel_TheRoadIsTheSame()
    {
        // Arrange
        var map = VillageMap.Demo;

        // Act
        var paths = new[] { 1, 2, 3 }.Select(level => map.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>(), null, level).ResolveEnemyPath()).ToList();

        // Assert
        paths[1].Should().Equal(paths[0]);
        paths[2].Should().Equal(paths[0]);
    }

    [Fact]
    public void ToBattleLayout_HearthOne_LockedLandBlocksRaiders()
    {
        // Act
        var layout = RichBase().ToBattleLayout();

        // Assert
        layout.IsBlocked(new GridPosition(5, 22)).Should().BeTrue();
        layout.IsBlocked(new GridPosition(33, 3)).Should().BeTrue();
    }

    [Fact]
    public void Restore_BuildingOnLockedLand_IsInvalid()
    {
        // Arrange
        var data = RichBase().ToData();
        var edited = data with { Buildings = data.Buildings.Append(new BuildingData(5, BuildingType.WattleFence, 1, 5, 22, null, 0, T0.UtcTicks)).ToList(), NextId = 6 };

        // Act
        var act = () => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, edited);

        // Assert
        act.Should().Throw<InvalidBaseDataException>();
    }
}
