using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-16 hearth four: the Comfort Era.</summary>
public class HearthFourTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly BuildingTable Rich = new()
    {
        StartingStock = new ResourceBag(20000, 5000, 5000, 5000),
        HearthStorageCaps = new[] { 30000, 30000, 30000, 30000 },
        HearthBeds = 20,
    };

    private static (BaseState State, DateTimeOffset Now) Village(int hearth)
    {
        var state = BaseState.CreateNew(Rich, VillageMap.Demo, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted, $"to Hearth {level}");
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        return (state, T0);
    }

    [Fact]
    public void UpgradeHearth_FromThree_ReachesFourWithBiggerStores()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        var (rich, now) = Village(4);

        // Assert
        rich.Snapshot(now).HearthLevel.Should().Be(4);
        BuildingTable.Default.HearthStorageCaps[3].Should().Be(12000);
        state.Snapshot(T0).HearthLevel.Should().Be(1);
    }

    [Fact]
    public void Place_SaunaAtHearthThree_Rejected()
    {
        // Arrange
        var (state, now) = Village(3);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Sauna, new GridPosition(20, 14)), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("Hearth 4");
    }

    [Fact]
    public void Place_BeehiveAtHearthTwo_MakesDevotionWithNobodyWorkingItAndNoRoad()
    {
        // Arrange
        var (state, now) = Village(2);

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Beehive, new GridPosition(20, 14)), now).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);

        // Assert
        var hive = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Beehive);
        hive.PerHour.Should().BeGreaterThan(0);
        hive.CrewNeeded.Should().Be(0);
    }

    [Fact]
    public void BeginNight_PressureSluiceBesideTheRoad_IsATowerInThePlan()
    {
        // Arrange
        var (state, now) = Village(4);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.PressureSluice, new GridPosition(10, 9)), now).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), now).Plan!;

        // Assert
        plan.Towers.Should().ContainSingle(t => t.Type == TowerType.PressureSluice);
    }

    [Fact]
    public void Battle_PressureSluice_KnocksRaidersBackSoTheyArriveLater()
    {
        // Arrange — the same Night with a Flinger, then with the Flinger and a Pressure Sluice beside it.
        var map = VillageMap.Demo;
        BattleResult Fight(params PlacedTower[] towers)
        {
            var plan = new NightPlan(2, 77, 30, 3, 0, false, towers, Array.Empty<GridPosition>(), Array.Empty<PlacedStructure>(), 2);
            return new Battle(plan.ToBattleSetup(map, Array.Empty<PlayerInput>())).RunToCompletion();
        }

        // Act
        var flinger = Fight(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(9, 9)));
        var both = Fight(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(9, 9)), new PlacedTower(TowerType.PressureSluice, new GridPosition(11, 9)));

        // Assert — pushed back into the Flinger's reach again and again: more fall, fewer get through.
        both.EnemiesDefeated.Should().BeGreaterThan(flinger.EnemiesDefeated);
        both.TicksElapsed.Should().BeGreaterThan(flinger.TicksElapsed);
    }

    [Fact]
    public void Research_HearthFourStudies_LockedBelowFour()
    {
        // Arrange
        var (state, now) = Village(3);

        // Act
        var views = state.Snapshot(now).Research!;

        // Assert
        var four = ResearchBook.Default.Techs.Where(t => t.UnlockHearth == 4).Select(t => t.Id).ToList();
        four.Should().HaveCount(4);
        views.Where(v => four.Contains(v.Tech.Id)).Should().OnlyContain(v => v.State == TechState.Locked);
    }
}
