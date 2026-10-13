using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BuildToolsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BuildingTable Table() => new() { StartingStock = new ResourceBag(2000, 900, 900, 300), HearthBeds = 6 };

    private static BaseState NewBase() => BaseState.CreateNew(Table(), Map, T0);

    private static int Place(BaseState state, BuildingType type, int x, int y, bool rotated = false)
    {
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y), rotated), T0).Status.Should().Be(CommandStatus.Accepted);
        return state.Snapshot(T0).BuildingAt(new GridPosition(x, y))!.Id;
    }

    [Fact]
    public void Move_ALeanTo_KeepsItsLevelAndItsOutput()
    {
        // Arrange — a finished lean-to by the road with an hour of timber waiting.
        var state = NewBase();
        var lean = Place(state, BuildingType.Woodcutter, 15, 5);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var later = T0.AddHours(1);

        // Act — to the other side of the road, turned.
        var outcome = state.Apply(new MoveBuilding(Guid.NewGuid(), lean, new GridPosition(18, 4), true), later);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var moved = state.Snapshot(later).Building(lean);
        moved.Tiles.Should().BeEquivalentTo(new[] { new GridPosition(18, 4), new GridPosition(18, 5) });
        (moved.Level, moved.Ready, moved.Rotated).Should().Be((1, 125, true)); // an hour's 100 + its 25 starter pile
        state.Snapshot(later).BuildingAt(new GridPosition(15, 5)).Should().BeNull();
    }

    [Fact]
    public void Move_OntoItsOwnTiles_IsFineButOntoAnotherIsNot()
    {
        // Arrange
        var state = NewBase();
        var lean = Place(state, BuildingType.Woodcutter, 15, 5);
        Place(state, BuildingType.WattleFence, 13, 6);

        // Act
        var shuffle = state.Apply(new MoveBuilding(Guid.NewGuid(), lean, new GridPosition(14, 5), false), T0); // overlaps itself
        var clash = state.Apply(new MoveBuilding(Guid.NewGuid(), lean, new GridPosition(13, 6), false), T0);

        // Assert
        shuffle.Status.Should().Be(CommandStatus.Accepted);
        clash.Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Move_TheHearthOrARoad_Refused()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, new[] { new GridPosition(13, 6) }), T0);
        var track = state.Snapshot(T0).BuildingAt(new GridPosition(13, 6))!.Id;

        // Act & Assert
        state.Apply(new MoveBuilding(Guid.NewGuid(), 0, new GridPosition(20, 11), false), T0).Status.Should().Be(CommandStatus.Rejected);
        state.Apply(new MoveBuilding(Guid.NewGuid(), track, new GridPosition(14, 6), false), T0).Reason.Should().Be("Roads are repainted, not moved.");
    }

    [Fact]
    public void RepairAll_EnoughForEverything_RepairsEverything()
    {
        // Arrange — two wrecked fences.
        var state = NewBase();
        var a = Place(state, BuildingType.WattleFence, 13, 6);
        var b = Place(state, BuildingType.WattleFence, 14, 6);
        state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { a, b }), T0);
        var before = state.Snapshot(T0).Stock;

        // Act
        var outcome = state.Apply(new RepairAll(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Buildings.Should().NotContain(x => x.Wrecked);
        state.Snapshot(T0).Stock.Should().Be(before - new ResourceBag(4, 0, 0, 0), "2 Timber each (10 % of 15, rounded up)");
    }

    [Fact]
    public void RepairAll_NotEnough_RepairsNothingAndSaysWhatIsMissing()
    {
        // Arrange — wrecked buildings, then the Timber spent.
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(31, 900, 900, 0) }, Map, T0);
        var a = Place(state, BuildingType.WattleFence, 13, 6);
        var b = Place(state, BuildingType.WattleFence, 14, 6);
        state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { a, b }), T0);
        state.Snapshot(T0).Stock.Timber.Should().Be(1);

        // Act
        var outcome = state.Apply(new RepairAll(Guid.NewGuid()), T0);

        // Assert
        outcome.Reason.Should().Be("Need 3 Timber more.");
        state.Snapshot(T0).Buildings.Count(x => x.Wrecked).Should().Be(2);
    }

    [Fact]
    public void PaintTiles_AWallLine_PlacesEveryFreeTile()
    {
        // Arrange
        var state = NewBase();

        // Act
        var outcome = state.Apply(new PaintTiles(Guid.NewGuid(), BuildingType.WoodenWall,
            new[] { new GridPosition(12, 4), new GridPosition(13, 4), new GridPosition(14, 4), new GridPosition(16, 4) }), T0); // (16,4) is a boulder

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Buildings.Count(b => b.Type == BuildingType.WoodenWall).Should().Be(3);
    }

    [Fact]
    public void PaintTiles_ABuilding_Refused()
    {
        // Act
        var outcome = NewBase().Apply(new PaintTiles(Guid.NewGuid(), BuildingType.Cottage, new[] { new GridPosition(20, 11) }), T0);

        // Assert
        outcome.Reason.Should().Be("That isn't painted on.");
    }
}
