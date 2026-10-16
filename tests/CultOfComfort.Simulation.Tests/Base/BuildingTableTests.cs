using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BuildingTableTests
{
    [Fact]
    public void Default_Hearth_FollowsTheGddCostsAndCaps()
    {
        // Act
        var hearth = BuildingTable.Default.Spec(BuildingType.Hearth);

        // Assert
        hearth.MaxLevel.Should().Be(3);
        hearth.Levels[1].Cost.Should().Be(new ResourceBag(450, 0, 50, 0)); // village-2026.10.4: the tutorial step
        hearth.Levels[1].BuildSeconds.Should().Be(600);
        hearth.Levels[2].Cost.Should().Be(new ResourceBag(1200, 150, 200, 100)); // 2026-10-16 first day: ~7 h for an errand player, not ~30
        hearth.Levels[2].BuildSeconds.Should().Be(45 * 60);
        BuildingTable.Default.HearthStorageCaps.Should().Equal(1000, 2500, 6000);
        BuildingTable.Default.StartingStock.Should().Be(new ResourceBag(500, 60, 380, 0));
    }

    [Fact]
    public void Default_Towers_AreUnlimitedAndInstant_FlingerAtHearthTwo()
    {
        // Act
        var chute = BuildingTable.Default.Spec(BuildingType.SlurryChute);
        var flinger = BuildingTable.Default.Spec(BuildingType.SaplingFlinger);

        // Assert
        chute.IsTower.Should().BeTrue();
        chute.Tower.Should().Be(TowerType.SlurryChute);
        chute.UnlockHearth.Should().Be(1);
        chute.MaxCount(1).Should().Be(int.MaxValue);
        chute.Levels[0].BuildSeconds.Should().Be(0);
        flinger.UnlockHearth.Should().Be(2);
        flinger.Levels[0].Cost.Should().Be(new ResourceBag(0, 0, 70, 0));
    }

    [Fact]
    public void ResourceBag_Missing_ListsOnlyWhatIsShort()
    {
        // Arrange
        var stock = new ResourceBag(40, 100, 5, 0);

        // Act
        var missing = stock.Missing(new ResourceBag(50, 20, 10, 0));

        // Assert
        missing.Should().Be(new ResourceBag(10, 0, 5, 0));
        stock.Covers(new ResourceBag(50, 20, 10, 0)).Should().BeFalse();
        stock.Covers(new ResourceBag(40, 100, 5, 0)).Should().BeTrue();
    }

    [Fact]
    public void Salvage_StarsOutOfRange_AreClamped()
    {
        BuildingTable.Default.Salvage(-1).Should().Be(40);
        BuildingTable.Default.Salvage(3).Should().Be(160);
        BuildingTable.Default.Salvage(9).Should().Be(160);
    }

    [Fact]
    public void VillageMap_Demo_MatchesTheBattleBoard()
    {
        // Act
        var map = VillageMap.Demo;

        // Assert
        (map.Width, map.Height).Should().Be((48, 32), "the full map; 30×20 is open at Hearth 1");
        map.Spawn.Should().Be(new GridPosition(0, 1));
        map.Hearth.Should().Be(new GridPosition(17, 9));
        map.Rocks.Should().HaveCount(8 + 29 + 6 + 10, "walls of 8 and 29 tiles, 6 boulders on the road side and 10 out in the new land");
        map.ToBattleLayout([], []).ResolveEnemyPath().Should().NotBeEmpty();
    }
}
