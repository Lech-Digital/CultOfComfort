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
        hearth.Levels[1].Cost.Should().Be(new ResourceBag(600, 0, 50, 0));
        hearth.Levels[1].BuildSeconds.Should().Be(600);
        hearth.Levels[2].Cost.Should().Be(new ResourceBag(2000, 200, 300, 100));
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
        (map.Width, map.Height).Should().Be((22, 14), "the full map; 18×11 is open at Hearth 1");
        map.Spawn.Should().Be(new GridPosition(0, 1));
        map.Hearth.Should().Be(new GridPosition(17, 9));
        map.Rocks.Should().HaveCount(25, "walls of 8 and 11 plus 6 boulders");
        map.ToBattleLayout([], []).ResolveEnemyPath().Should().NotBeEmpty();
    }
}
