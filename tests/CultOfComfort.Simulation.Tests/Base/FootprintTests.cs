using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class FootprintTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BaseState RichBase() =>
        BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) }, Map, T0);

    private static CommandOutcome Place(BaseState state, BuildingType type, int x, int y, bool rotated = false) =>
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y), rotated), T0);

    [Fact]
    public void Default_Sizes_AreVaried()
    {
        // Act
        var table = BuildingTable.Default;

        // Assert
        (table.Spec(BuildingType.Woodcutter).Width, table.Spec(BuildingType.Woodcutter).Depth).Should().Be((2, 1));
        (table.Spec(BuildingType.PeatCut).Width, table.Spec(BuildingType.PeatCut).Depth).Should().Be((2, 4));
        (table.Spec(BuildingType.StorageHut).Width, table.Spec(BuildingType.StorageHut).Depth).Should().Be((2, 2));
        (table.Spec(BuildingType.CultistHut).Width, table.Spec(BuildingType.CultistHut).Depth).Should().Be((3, 2));
        (table.Spec(BuildingType.SlurryChute).Width, table.Spec(BuildingType.SlurryChute).Depth).Should().Be((1, 1));
    }

    [Fact]
    public void Place_PeatCut_OccupiesItsWholeTrench()
    {
        // Arrange
        var state = RichBase();

        // Act
        var outcome = Place(state, BuildingType.PeatCut, 20, 12);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var snapshot = state.Snapshot(T0);
        var peat = snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut);
        peat.Tiles.Should().HaveCount(8);
        snapshot.BuildingAt(new GridPosition(21, 15))!.Id.Should().Be(peat.Id);
        snapshot.BuildingAt(new GridPosition(22, 12)).Should().BeNull();
        snapshot.BuildingAt(new GridPosition(20, 16)).Should().BeNull();
    }

    [Fact]
    public void Place_Rotated_SwapsWidthAndDepth()
    {
        // Arrange
        var state = RichBase();

        // Act
        Place(state, BuildingType.Woodcutter, 13, 5, rotated: true).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        var snapshot = state.Snapshot(T0);
        var lean = snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter);
        lean.Rotated.Should().BeTrue();
        lean.Tiles.Should().BeEquivalentTo(new[] { new GridPosition(13, 5), new GridPosition(13, 6) });
    }

    [Fact]
    public void Place_OverlappingAnotherBuilding_Rejected()
    {
        // Arrange
        var state = RichBase();
        Place(state, BuildingType.WattleFence, 21, 14).Status.Should().Be(CommandStatus.Accepted);

        // Act — the trench would cover the fence.
        var outcome = Place(state, BuildingType.PeatCut, 20, 12);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Place_AnyTileOnTheRoad_Rejected()
    {
        // Arrange — (17,5) is on the road, (16,5) beside it.
        var state = RichBase();
        Map.Road.Should().Contain(new GridPosition(17, 5));

        // Act
        var onTheRoad = Place(state, BuildingType.WattleFence, 17, 5);
        var halfOnTheRoad = Place(state, BuildingType.Woodcutter, 16, 5);

        // Assert
        onTheRoad.Reason.Should().Be("That's the raiders' road. Build beside it.");
        halfOnTheRoad.Reason.Should().Be("That's the raiders' road. Build beside it.");
    }

    [Fact]
    public void Road_BuildingAndClearing_NeverMoveIt()
    {
        // Arrange — (8,4) is a boulder beside the road; clearing it would have opened a shortcut.
        var state = RichBase();
        state.Apply(new ClearTerrain(Guid.NewGuid(), new GridPosition(8, 4)), T0).Status.Should().Be(CommandStatus.Accepted);
        Place(state, BuildingType.SlurryChute, 9, 5).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var path = state.ToBattleLayout().ResolveEnemyPath();

        // Assert
        path.Should().Equal(Map.Road);
        Map.Road.First().Should().Be(Map.Spawn);
        Map.Road.Last().Should().Be(Map.Hearth);
    }

    [Fact]
    public void ToBattleLayout_BigBuilding_EveryTileIsInTheWayAndSmashable()
    {
        // Arrange
        var state = RichBase();
        Place(state, BuildingType.PeatCut, 20, 12);

        // Act
        var layout = state.ToBattleLayout();

        // Assert
        layout.IsBlocked(new GridPosition(21, 15)).Should().BeTrue();
        layout.Structures.Single().Tiles.Should().HaveCount(8);
    }

    [Fact]
    public void Battle_StructureBesideTheRoadByItsFarTile_GetsSmashed()
    {
        // Arrange — a straight road along row 0; the structure's position (4,2) is two rows down, its other tile (4,1) beside the road.
        var road = Enumerable.Range(0, 10).Select(x => new GridPosition(x, 0)).ToList();
        var structure = new PlacedStructure(7, new GridPosition(4, 2), 200, 200) { Tiles = new[] { new GridPosition(4, 1), new GridPosition(4, 2) } };
        var oneTile = structure with { Tiles = new[] { new GridPosition(4, 2) } };
        List<BattleEvent> Run(PlacedStructure s)
        {
            var battle = new Battle(new BattleSetup(
                new BattleLayout(10, 4, new GridPosition(0, 0), new GridPosition(9, 0), Array.Empty<PlacedTower>(), Array.Empty<GridPosition>(), null, new[] { s }, road),
                5, 20, 1, []));
            var events = new List<BattleEvent>();
            while (!battle.IsFinished)
            {
                battle.Step();
                events.AddRange(battle.LastTickEvents);
            }

            return events;
        }

        // Act
        var near = Run(structure);
        var far = Run(oneTile);

        // Assert
        near.OfType<StructureHit>().Should().NotBeEmpty();
        far.OfType<StructureHit>().Should().BeEmpty();
    }

    [Fact]
    public void Restore_RotatedFence_RoundTrips()
    {
        // Arrange
        var state = RichBase();
        Place(state, BuildingType.WattleFence, 13, 4, rotated: true);

        // Act
        var data = state.ToData();
        var restored = BaseState.Restore(new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) }, Map, data);

        // Assert
        data.LayoutVersion.Should().Be(BaseState.CurrentLayoutVersion);
        restored.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.WattleFence).Rotated.Should().BeTrue();
    }

    [Fact]
    public void Restore_OldSaveWhereTheNewShapeDoesNotFit_MovesItOrRefunds()
    {
        // Arrange — a version-0 save: a Peat Cut whose new 2×4 trench would cross the road at x = 17, and a fence on the road.
        var data = RichBase().ToData() with
        {
            LayoutVersion = 0,
            NextId = 4,
        };
        data = data with
        {
            Buildings = data.Buildings.Concat(new[]
            {
                new BuildingData(2, BuildingType.PeatCut, 1, 16, 3, null, 0, T0.UtcTicks),
                new BuildingData(3, BuildingType.WattleFence, 1, 2, 1, null, 0, T0.UtcTicks),
            }).ToList(),
        };

        // Act
        var restored = BaseState.Restore(new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) }, Map, data);

        // Assert
        var snapshot = restored.Snapshot(T0);
        var road = Map.Road.ToHashSet();
        var tiles = snapshot.Buildings.SelectMany(b => b.Tiles).ToList();
        tiles.Should().OnlyHaveUniqueItems();
        snapshot.Buildings.Where(b => b.Type != BuildingType.Hearth).SelectMany(b => b.Tiles).Should().NotContain(t => road.Contains(t));
        snapshot.Buildings.Should().Contain(b => b.Type == BuildingType.PeatCut, "there is room nearby for the trench");
        restored.ToData().LayoutVersion.Should().Be(BaseState.CurrentLayoutVersion);
    }

    [Fact]
    public void Restore_OldSaveInACrampedCorner_FindsRoomOrRefunds()
    {
        // Arrange — a Cultist Hut (3×2) squeezed between the entrance, the road and a boulder.
        var data = RichBase().ToData() with { LayoutVersion = 0, NextId = 3 };
        var stockBefore = data.Stock;
        data = data with
        {
            Buildings = data.Buildings.Append(new BuildingData(2, BuildingType.CultistHut, 1, 0, 6, null, 0, T0.UtcTicks)).ToList(),
        };
        var table = new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) };
        var hutCost = table.Spec(BuildingType.CultistHut).Levels[0].Cost;

        // Act
        var restored = BaseState.Restore(table, Map, data);

        // Assert — either it found a home nearby, or the player got every penny back.
        var snapshot = restored.Snapshot(T0);
        var hut = snapshot.Buildings.SingleOrDefault(b => b.Type == BuildingType.CultistHut);
        if (hut == null)
        {
            snapshot.Stock.Timber.Should().BeGreaterThanOrEqualTo(Math.Min(stockBefore.Timber + hutCost.Timber, snapshot.Caps.Timber));
        }
        else
        {
            hut.Tiles.Should().NotContain(t => Map.Road.Contains(t) || Map.Rocks.Contains(t) || Map.IsInSpawnClearance(t));
        }
    }
}
