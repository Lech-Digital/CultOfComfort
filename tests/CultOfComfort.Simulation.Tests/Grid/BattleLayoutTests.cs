using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Grid;

public class BattleLayoutTests
{
    [Fact]
    public void ResolveEnemyPath_StraightCorridor_ReturnsShortestPath()
    {
        // Arrange
        var layout = TestLayouts.Corridor();

        // Act
        var path = layout.ResolveEnemyPath();

        // Assert
        path.Should().HaveCount(10);
        path[0].Should().Be(layout.Spawn);
        path[^1].Should().Be(layout.Hearth);
    }

    [Fact]
    public void ResolveEnemyPath_TowerBlockingCorridor_RoutesAroundIt()
    {
        // Arrange
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(4, 1)));

        // Act
        var path = layout.ResolveEnemyPath();

        // Assert
        path.Should().NotContain(new GridPosition(4, 1));
        path.Should().HaveCount(12);
    }

    [Fact]
    public void Validate_WallSealingTheHearth_ThrowsInvalidLayoutException()
    {
        // Arrange
        var wall = Enumerable.Range(0, 3).Select(y => new GridPosition(5, y));
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], wall);

        // Act
        Action act = () => layout.Validate();

        // Assert
        act.Should().Throw<InvalidLayoutException>().WithMessage("*no path*");
    }

    [Fact]
    public void Validate_TowerOnObstacle_ThrowsInvalidLayoutException()
    {
        // Arrange
        var rock = new GridPosition(3, 0);
        var layout = new BattleLayout(
            10, 3, new GridPosition(0, 1), new GridPosition(9, 1),
            [new PlacedTower(TowerType.SlurryChute, rock)],
            [rock]);

        // Act
        Action act = () => layout.Validate();

        // Assert
        act.Should().Throw<InvalidLayoutException>().WithMessage("*occupied twice*");
    }

    [Fact]
    public void Validate_TowerOutsideGrid_ThrowsInvalidLayoutException()
    {
        // Arrange
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SlurryChute, new GridPosition(20, 0)));

        // Act
        Action act = () => layout.Validate();

        // Assert
        act.Should().Throw<InvalidLayoutException>().WithMessage("*outside*");
    }

    [Fact]
    public void ComputeHash_TowerMoved_ProducesDifferentHash()
    {
        // Arrange
        var original = TestLayouts.Corridor(new PlacedTower(TowerType.SlurryChute, new GridPosition(3, 0)));
        var identical = TestLayouts.Corridor(new PlacedTower(TowerType.SlurryChute, new GridPosition(3, 0)));
        var moved = TestLayouts.Corridor(new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 0)));

        // Act & Assert
        identical.ComputeHash().Should().Be(original.ComputeHash());
        moved.ComputeHash().Should().NotBe(original.ComputeHash());
    }
}
