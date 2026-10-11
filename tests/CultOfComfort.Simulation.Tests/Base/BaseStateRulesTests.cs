using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    [Theory]
    [InlineData(0, 1)]   // spawn
    [InlineData(17, 9)]  // Hearth
    [InlineData(5, 3)]   // rock
    [InlineData(-1, 0)]  // outside
    [InlineData(22, 0)]  // outside (the full map is 22 wide)
    public void Apply_PlaceOnSpawnOrHearth_Rejected(int x, int y)
    {
        // Act
        var outcome = NewBase().Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(x, y)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Apply_PlaceThatSealsTheRoad_Rejected()
    {
        // Arrange — the rock wall at x = 5 leaves y = 8..10 open; fill (5,8) and (5,9), then try (5,10).
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 8)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 9)), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 10)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("seal the road");
    }

    [Fact]
    public void Apply_SameCommandIdTwice_AppliedOnce()
    {
        // Arrange
        var state = NewBase();
        var command = new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6));

        // Act
        var first = state.Apply(command, T0);
        var second = state.Apply(command, T0);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        second.Status.Should().Be(CommandStatus.Duplicate);
        second.Version.Should().Be(1);
        state.Snapshot(T0).Stock.Scrap.Should().Be(330, "charged once");
    }

    [Fact]
    public void Apply_RejectedCommand_DoesNotBumpTheVersion()
    {
        // Arrange
        var state = NewBase();

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0);

        // Assert
        state.Version.Should().Be(0);
    }

    [Fact]
    public void Apply_FlingerAtHearthOne_RejectedAsLocked()
    {
        // Act
        var outcome = NewBase().Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0);

        // Assert
        outcome.Reason.Should().Be("Sapling Flinger unlocks at Hearth 2.");
    }

    [Fact]
    public void Apply_ThirdWoodcutterAtHearthOne_RejectedByTheLimit()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(4, 9)), T0.AddSeconds(30));

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(6, 9)), T0.AddSeconds(60));

        // Assert
        outcome.Reason.Should().Contain("until the Hearth grows");
    }

    [Fact]
    public void Apply_HearthUpgradeFinished_RaisesCapsAndUnlocks()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Reason.Should().Contain("Need"); // 500 Timber is not enough
        var rich = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData() with { Stock = new ResourceBag(900, 60, 380, 0) });

        // Act
        rich.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        var snapshot = rich.Snapshot(T0.AddMinutes(10));

        // Assert
        snapshot.HearthLevel.Should().Be(2);
        snapshot.Caps.Timber.Should().Be(2500);
        rich.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0.AddMinutes(10)).Status.Should().Be(CommandStatus.Accepted);
    }

    [Fact]
    public void Apply_DemolishBuildingAndTower_RefundsHalfAndFull()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(3, 9)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        var at = T0.AddMinutes(1);
        var snapshot = state.Snapshot(at);
        var hut = snapshot.Buildings.Single(b => b.Type == BuildingType.StorageHut).Id;
        var chute = snapshot.Buildings.Single(b => b.Type == BuildingType.SlurryChute).Id;

        // Act
        state.Apply(new DemolishBuilding(Guid.NewGuid(), hut), at).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new DemolishBuilding(Guid.NewGuid(), chute), at).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(at).Stock.Should().Be(new ResourceBag(500 - 100 + 50, 60, 380, 0));
    }

    [Fact]
    public void Apply_DemolishTheHearth_Rejected()
    {
        NewBase().Apply(new DemolishBuilding(Guid.NewGuid(), 0), T0).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Apply_RecordNightResult_AddsSalvageOnceAndIsCappedByStorage()
    {
        // Arrange
        var state = NewBase();

        // Act
        var first = state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);
        var again = state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);

        // Assert
        first.Collected.Scrap.Should().Be(160);
        again.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(T0).Stock.Scrap.Should().Be(540);
        state.LastNightRecorded.Should().Be(1);
    }

    [Fact]
    public void ToBattleLayout_TowersAndBuildings_TowersFightAndBuildingsBlock()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);

        // Act
        var layout = state.ToBattleLayout();

        // Assert
        layout.Towers.Should().ContainSingle(t => t.Position == new GridPosition(4, 6));
        layout.Obstacles.Should().Contain(new GridPosition(3, 9));
        layout.Obstacles.Should().NotContain(VillageMap.Demo.Hearth);
        layout.ResolveEnemyPath().Should().NotContain(new GridPosition(3, 9));
    }

    [Fact]
    public void Restore_RoundTrip_KeepsEverything()
    {
        // Arrange
        var state = NewBase();
        var place = new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9));
        state.Apply(place, T0);
        var at = T0.AddHours(2);

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(at).Should().BeEquivalentTo(state.Snapshot(at));
        restored.Apply(place, at).Status.Should().Be(CommandStatus.Duplicate, "remembered command ids survive a save");
    }

    [Fact]
    public void Restore_DataWithoutAHearth_Throws()
    {
        // Arrange
        var data = NewBase().ToData();
        var broken = data with { Buildings = data.Buildings.Where(b => b.Type != BuildingType.Hearth).ToList() };

        // Act
        var act = () => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, broken);

        // Assert
        act.Should().Throw<InvalidBaseDataException>();
    }
}
