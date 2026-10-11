using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateHardeningTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseData WithWoodcutter()
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        return state.ToData();
    }

    private static BaseData Edit(BaseData data, Func<BuildingData, BuildingData> edit, BuildingType type) =>
        data with { Buildings = data.Buildings.Select(b => b.Type == type ? edit(b) : b).ToList() };

    public static IEnumerable<object[]> BrokenSaves()
    {
        var good = WithWoodcutter();
        yield return new object[] { "hearth level 0", Edit(good, b => b with { Level = 0 }, BuildingType.Hearth) };
        yield return new object[] { "max level with a timer", Edit(good, b => b with { Level = 3, UpgradeEndsAtUtcTicks = T0.UtcTicks }, BuildingType.Hearth) };
        yield return new object[] { "level 0 without a timer", Edit(good, b => b with { Level = 0, UpgradeEndsAtUtcTicks = null }, BuildingType.Woodcutter) };
        yield return new object[] { "on the spawn tile", Edit(good, b => b with { X = 0, Y = 1 }, BuildingType.Woodcutter) };
        yield return new object[] { "on a rock", Edit(good, b => b with { X = 5, Y = 3 }, BuildingType.Woodcutter) };
        yield return new object[] { "off the map", Edit(good, b => b with { X = 40, Y = 1 }, BuildingType.Woodcutter) };
        yield return new object[] { "hearth moved", Edit(good, b => b with { X = 3, Y = 3 }, BuildingType.Hearth) };
        yield return new object[] { "duplicate ids", Edit(good, b => b with { Id = 0 }, BuildingType.Woodcutter) };
        yield return new object[] { "next id not past the ids", good with { NextId = 1 } };
        yield return new object[] { "negative stock", good with { Stock = new ResourceBag(-5, 0, 0, 0) } };
        yield return new object[] { "negative buffer", Edit(good, b => b with { BufferMilli = -1 }, BuildingType.Woodcutter) };
    }

    [Theory]
    [MemberData(nameof(BrokenSaves))]
    public void Restore_ImpossibleSave_ThrowsInvalidBaseData(string because, BaseData data)
    {
        // Act
        var act = () => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(T0.AddHours(1));

        // Assert
        act.Should().Throw<InvalidBaseDataException>(because);
    }

    [Fact]
    public void Restore_TwoConstructionsPending_ThrowsInvalidBaseData()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        var data = state.ToData();
        var hearthUpgrading = Edit(data, b => b with { UpgradeEndsAtUtcTicks = T0.AddHours(1).UtcTicks }, BuildingType.Hearth);

        // Act
        var act = () => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, hearthUpgrading);

        // Assert
        act.Should().Throw<InvalidBaseDataException>();
    }

    [Fact]
    public void Upgrade_StartedAfterTheClockWentBack_DoesNotCountProductionTwice()
    {
        // Arrange — 900 Timber so the Hearth can grow; Woodcutter built, then collected at t1.
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 60, 380, 0) }, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0.AddSeconds(30));
        var t1 = T0.AddHours(7);
        state.Apply(new Collect(Guid.NewGuid(), id), t1);

        // Act — the clock jumps back six hours and the player upgrades the Woodcutter (5 min).
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), t1.AddHours(-6)).Status.Should().Be(CommandStatus.Accepted);
        var ready = state.Snapshot(t1).Building(id).Ready;

        // Assert — nothing can have been produced between the collect and t1.
        ready.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void VillageLayout_FromSnapshot_MatchesTheAggregatesLayout()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);

        // Act
        var fromSnapshot = VillageMap.Demo.ToBattleLayout(state.Snapshot(T0), BuildingTable.Default);

        // Assert
        fromSnapshot.Towers.Should().Equal(state.ToBattleLayout().Towers);
        fromSnapshot.Obstacles.Should().BeEquivalentTo(state.ToBattleLayout().Obstacles);
    }
}
