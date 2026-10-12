using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class RoadTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    // A track from (20,14) round to the Hearth at (17,9): west to x 18, then north to the tile beside the Hearth.
    private static readonly GridPosition[] TrackToHearth =
    {
        new(19, 14), new(18, 14), new(18, 13), new(18, 12), new(18, 11), new(18, 10), new(18, 9),
    };

    private static BuildingTable Table() => new() { StartingStock = new ResourceBag(5000, 900, 1000, 300) };

    private static BaseState RichBase() => BaseState.CreateNew(Table(), Map, T0);

    private static CommandOutcome Place(BaseState state, BuildingType type, int x, int y, bool rotated = false) =>
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y), rotated), T0);

    private static CommandOutcome Paint(BaseState state, BuildingType type, params GridPosition[] tiles) =>
        state.Apply(new PlaceRoad(Guid.NewGuid(), type, tiles), T0);

    private static BuildingView Woodcutter(BaseState state, DateTimeOffset at) =>
        state.Snapshot(at).Buildings.Single(b => b.Type == BuildingType.Woodcutter);

    private static BaseState WithFinishedWoodcutterAt(int x, int y)
    {
        var state = RichBase();
        Place(state, BuildingType.Woodcutter, x, y).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        return state;
    }

    [Fact]
    public void Connected_BesideTheRaidersRoad_ItWorks()
    {
        // Arrange — the lean-to's tile (16,5) touches the raiders' road at (17,5): the old track is a road too.
        var state = WithFinishedWoodcutterAt(15, 5);

        // Act
        var lean = Woodcutter(state, T0.AddHours(1));

        // Assert
        lean.Connected.Should().BeTrue();
        lean.Ready.Should().Be(100);
    }

    [Fact]
    public void Unconnected_FarFromAnyRoad_ProducesNothing()
    {
        // Arrange
        var state = WithFinishedWoodcutterAt(20, 14);

        // Act
        var lean = Woodcutter(state, T0.AddHours(1));

        // Assert
        lean.Connected.Should().BeFalse();
        lean.Ready.Should().Be(0);
    }

    [Fact]
    public void PaintATrack_ToTheHearth_ConnectsItAndChargesPerTile()
    {
        // Arrange
        var state = WithFinishedWoodcutterAt(20, 14);
        var timber = state.Snapshot(T0).Stock.Timber;

        // Act
        var outcome = Paint(state, BuildingType.DirtTrack, TrackToHearth);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Stock.Timber.Should().Be(timber - 2 * TrackToHearth.Length);
        Woodcutter(state, T0.AddHours(1)).Connected.Should().BeTrue();
        Woodcutter(state, T0.AddHours(1)).Ready.Should().Be(100, "it produces from the moment it was connected");
    }

    [Fact]
    public void PaintATrack_OverRocksTheRoadAndBuildings_PlacesOnlyTheGoodTiles()
    {
        // Arrange — (11,5) is bedrock, (17,5) the raiders' road; (13,6) and (14,6) are open.
        var state = RichBase();

        // Act
        var some = Paint(state, BuildingType.DirtTrack, new(11, 5), new(17, 5), new(13, 6), new(14, 6));
        var none = Paint(state, BuildingType.DirtTrack, new(11, 5), new(17, 5));

        // Assert
        some.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Buildings.Count(b => b.Type == BuildingType.DirtTrack).Should().Be(2);
        none.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void PaintAPlankWalk_OverATrack_ReplacesItAndBoostsTheNeighbours()
    {
        // Arrange — Hearth 2 for planks; a lean-to connected by a track.
        var state = WithFinishedWoodcutterAt(20, 14);
        Paint(state, BuildingType.DirtTrack, TrackToHearth);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var at = T0.AddSeconds(1);

        // Act — planks over the tile beside the lean-to; the rest stays dirt.
        var outcome = state.Apply(new PlaceRoad(Guid.NewGuid(), BuildingType.PlankWalk, new[] { new GridPosition(19, 14) }), at);
        var again = state.Apply(new PlaceRoad(Guid.NewGuid(), BuildingType.DirtTrack, new[] { new GridPosition(19, 14) }), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        again.Status.Should().Be(CommandStatus.Rejected, "a worse road never paints over a better one");
        var snapshot = state.Snapshot(at);
        snapshot.BuildingAt(new GridPosition(19, 14))!.Type.Should().Be(BuildingType.PlankWalk);
        snapshot.Buildings.Count(b => b.Type is BuildingType.DirtTrack or BuildingType.PlankWalk).Should().Be(TrackToHearth.Length);
        Woodcutter(state, at).PerHour.Should().Be(110);
    }

    [Fact]
    public void Network_WallsCutRoadsGatesCarryThem()
    {
        // Arrange — Hearth (17,9) → track (18,9) → gate (19,9) → track (20,9) → lean-to (21,9)–(22,9).
        var state = WithFinishedWoodcutterAt(21, 9);
        Paint(state, BuildingType.DirtTrack, new(18, 9), new(20, 9));
        Place(state, BuildingType.Gate, 19, 9).Status.Should().Be(CommandStatus.Accepted);
        var throughTheGate = Woodcutter(state, T0).Connected;

        // Act — swap the gate for a wall.
        var gate = state.Snapshot(T0).BuildingAt(new GridPosition(19, 9))!.Id;
        state.Apply(new DemolishBuilding(Guid.NewGuid(), gate), T0);
        Place(state, BuildingType.WoodenWall, 19, 9).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        throughTheGate.Should().BeTrue();
        Woodcutter(state, T0).Connected.Should().BeFalse();
    }

    [Fact]
    public void Caps_UnconnectedStorageHut_AddsNothing()
    {
        // Arrange
        var state = RichBase();
        var before = state.Snapshot(T0).Caps.Timber;
        Place(state, BuildingType.StorageHut, 20, 11);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Act
        var caps = state.Snapshot(T0).Caps.Timber;

        // Assert
        caps.Should().Be(before);
    }

    [Fact]
    public void ToBattleLayout_Roads_AreNotInTheWayAndNotSmashable()
    {
        // Arrange
        var state = RichBase();
        Paint(state, BuildingType.DirtTrack, new GridPosition(13, 6));

        // Act
        var layout = state.ToBattleLayout();

        // Assert
        layout.IsBlocked(new GridPosition(13, 6)).Should().BeFalse();
        layout.Structures.Should().BeEmpty();
    }

    [Fact]
    public void Place_OnARoad_Rejected()
    {
        // Arrange
        var state = RichBase();
        Paint(state, BuildingType.DirtTrack, new GridPosition(13, 6));

        // Act
        var outcome = Place(state, BuildingType.WattleFence, 13, 6);

        // Assert
        outcome.Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Restore_SaveFromBeforeRoads_LaysATrackToEveryBuilding()
    {
        // Arrange — a layout-version-1 save with a lean-to out in the field.
        var data = WithFinishedWoodcutterAt(20, 14).ToData() with { LayoutVersion = 1 };

        // Act
        var restored = BaseState.Restore(Table(), Map, data);

        // Assert
        Woodcutter(restored, T0).Connected.Should().BeTrue("old villages keep working");
        restored.Snapshot(T0).Buildings.Should().Contain(b => b.Type == BuildingType.DirtTrack);
        restored.ToData().LayoutVersion.Should().Be(BaseState.CurrentLayoutVersion);
    }

    [Fact]
    public void Restore_LayoutVersionOneSave_KeepsEveryBuildingsShape()
    {
        // Arrange — regression: a version-1 save already has shapes; loading it must not shrink them to 1×1
        // (roads were laid across the lean-to's second tile, and the next load threw the village away).
        var data = WithFinishedWoodcutterAt(15, 7).ToData() with { LayoutVersion = 1 };

        // Act
        var restored = BaseState.Restore(Table(), Map, data);
        var again = BaseState.Restore(Table(), Map, restored.ToData());

        // Assert
        Woodcutter(restored, T0).Tiles.Should().HaveCount(2);
        again.Snapshot(T0).Buildings.SelectMany(b => b.Tiles).Should().OnlyHaveUniqueItems("a road never runs through a building");
    }
}
