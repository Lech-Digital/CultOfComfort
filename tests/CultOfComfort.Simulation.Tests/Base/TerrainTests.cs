using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class TerrainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, Map, T0);

    private static GridPosition FirstOpenWildTree() => Map.WildTrees.First(t => Map.IsOpen(t, 1));

    [Fact]
    public void Demo_WildTrees_ScatteredButNeverOnTheRoadTheEntranceOrTheHearth()
    {
        // Arrange
        var road = Map.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>()).ResolveEnemyPath();

        // Act
        var trees = Map.WildTrees;

        // Assert
        trees.Count.Should().BeInRange(70, 200);
        trees.Should().NotIntersectWith(road);
        trees.Should().NotIntersectWith(Map.Rocks);
        trees.Should().OnlyContain(t => !Map.IsInSpawnClearance(t) && Math.Max(Math.Abs(t.X - Map.Hearth.X), Math.Abs(t.Y - Map.Hearth.Y)) > 1);
    }

    [Fact]
    public void Demo_Boulders_AreLooseRocksAndTheWallsAreBedrock()
    {
        Map.Boulders.Should().HaveCount(16);
        Map.IsBedrock(new GridPosition(5, 3)).Should().BeTrue();
        Map.IsBedrock(new GridPosition(8, 4)).Should().BeFalse();
    }

    [Fact]
    public void ToBattleLayout_WildTrees_DoNotChangeTheRoad()
    {
        // Act
        var layout = NewBase().ToBattleLayout();

        // Assert
        layout.ResolveEnemyPath().Count.Should().Be(Map.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>()).ResolveEnemyPath().Count);
        layout.IsBlocked(FirstOpenWildTree()).Should().BeFalse("wild trees only stop building; raiders walk through the undergrowth, so an old save can never be sealed by them");
    }

    [Fact]
    public void Place_NearTheEntrance_Rejected()
    {
        // Act
        var outcome = NewBase().Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(2, 3)), T0);

        // Assert
        outcome.Reason.Should().Be("Too close to where the raiders come in.");
    }

    [Fact]
    public void Place_OnAWildTree_RejectedUntilItIsCut()
    {
        // Arrange
        var state = NewBase();
        var tree = FirstOpenWildTree();

        // Act
        var before = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, tree), T0);
        var cut = state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);
        var after = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, tree), T0);

        // Assert
        before.Reason.Should().Be("Cut the tree down first.");
        cut.Status.Should().Be(CommandStatus.Accepted);
        cut.Collected.Should().Be(new ResourceBag(15, 0, 0, 0));
        after.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).ClearedTerrain.Should().Contain(tree);
    }

    [Fact]
    public void ClearTerrain_Boulder_IsFreeAndGivesItsStone()
    {
        // Arrange
        var state = NewBase();

        // Act
        var outcome = state.Apply(new ClearTerrain(Guid.NewGuid(), new GridPosition(8, 4)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Should().Be(new ResourceBag(0, 0, 10, 0));
        state.Snapshot(T0).Stock.Should().Be(new ResourceBag(500, 60, 390, 0), "stone gives exactly what a placed stone costs");
        state.ToBattleLayout().IsBlocked(new GridPosition(8, 4)).Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 3, "That's bedrock. It was here before the Romans and it will be here after the Prophet.")]
    [InlineData(34, 8, "That land opens at Hearth 2.")]
    [InlineData(10, 10, "Nothing to clear there.")]
    public void ClearTerrain_WallLockedOrEmpty_Rejected(int x, int y, string reason)
    {
        // Act
        var outcome = NewBase().Apply(new ClearTerrain(Guid.NewGuid(), new GridPosition(x, y)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be(reason);
    }

    [Fact]
    public void Place_StoneAndSapling_BlockingNotSmashableAndNoBuilderNeeded()
    {
        // Arrange — the builder is busy with a Woodcutter.
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(6, 9)), T0);

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Stone, new GridPosition(4, 6)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Tree, new GridPosition(3, 9)), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Stock.Should().Be(new ResourceBag(500 - 50 - 5, 60, 370, 0));
        var layout = state.ToBattleLayout();
        layout.IsBlocked(new GridPosition(4, 6)).Should().BeTrue();
        layout.IsBlocked(new GridPosition(3, 9)).Should().BeTrue();
        layout.Structures.Should().OnlyContain(st => st.Position == new GridPosition(6, 9), "raiders don't smash stones or trees");
    }

    [Fact]
    public void Demolish_Stone_GivesBackExactlyWhatItCost()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Stone, new GridPosition(4, 6)), T0);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Stone).Id;

        // Act
        state.Apply(new DemolishBuilding(Guid.NewGuid(), id), T0);

        // Assert
        state.Snapshot(T0).Stock.Scrap.Should().Be(380);
    }

    [Fact]
    public void PlantedTree_GrowsForFourHoursThenCutsForTimber()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Tree, new GridPosition(3, 9)), T0);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Tree).Id;

        // Act
        var sapling = state.Snapshot(T0.AddHours(2)).Building(id);
        var grown = state.Snapshot(T0.AddHours(4)).Building(id);
        var cut = state.Apply(new DemolishBuilding(Guid.NewGuid(), id), T0.AddHours(4));

        // Assert
        sapling.Level.Should().Be(0, "still a sapling");
        grown.Level.Should().Be(1);
        cut.Collected.Should().Be(new ResourceBag(15, 0, 0, 0), "a tree farm: plant 5, cut 15, wait 4 h");
        state.Snapshot(T0.AddHours(4)).Stock.Timber.Should().Be(500 - 5 + 15);
    }

    [Fact]
    public void PlantedSapling_CutEarly_GivesNothing()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Tree, new GridPosition(3, 9)), T0);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Tree).Id;

        // Act
        var cut = state.Apply(new DemolishBuilding(Guid.NewGuid(), id), T0.AddHours(1));

        // Assert
        cut.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0.AddHours(1)).Stock.Timber.Should().Be(495);
    }

    [Fact]
    public void WildTree_CutDown_GrowsBackAfterEightHours()
    {
        // Arrange
        var state = NewBase();
        var tree = FirstOpenWildTree();
        state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);

        // Act
        var stillGone = state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0.AddHours(7));
        var regrown = state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0.AddHours(8));

        // Assert
        stillGone.Status.Should().Be(CommandStatus.Rejected);
        regrown.Collected.Should().Be(new ResourceBag(15, 0, 0, 0));
    }

    [Fact]
    public void WildTree_CutAndBuiltOver_NeverGrowsBack()
    {
        // Arrange
        var state = NewBase();
        var tree = FirstOpenWildTree();
        state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, tree), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var later = state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0.AddDays(2));

        // Assert
        later.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Restore_ClearedTerrain_RoundTrips()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new ClearTerrain(Guid.NewGuid(), new GridPosition(8, 4)), T0);
        var tree = FirstOpenWildTree();
        state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, Map, state.ToData());
        restored.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0.AddHours(8)).Status.Should().Be(CommandStatus.Accepted, "the regrow clock survives a save");

        // Assert
        restored.Snapshot(T0).ClearedTerrain.Should().Contain(new GridPosition(8, 4));
        restored.ToBattleLayout().IsBlocked(new GridPosition(8, 4)).Should().BeFalse();
    }

    [Fact]
    public void Restore_OldSaveWithABuildingWhereAWildTreeWouldGrow_LoadsAndTheTreeStepsAside()
    {
        // Arrange — a fence placed before wild trees existed, on what is now a wild-tree tile.
        var tree = FirstOpenWildTree();
        var data = NewBase().ToData();
        var old = data with { Buildings = data.Buildings.Append(new BuildingData(1, BuildingType.WattleFence, 1, tree.X, tree.Y, null, 0, T0.UtcTicks)).ToList(), NextId = 2 };

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, Map, old);

        // Assert
        restored.Snapshot(T0).BuildingAt(tree).Should().NotBeNull();
        restored.ToBattleLayout().Validate();
    }
}
