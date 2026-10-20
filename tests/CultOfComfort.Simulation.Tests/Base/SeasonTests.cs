using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 seasons: three days each, the same for everyone, each changing the Nights.</summary>
public class SeasonTests
{
    // Seasons on the clock: 2026-10-08..10 spring, 11..13 summer, 14..16 autumn, 17..19 winter.
    private static readonly DateTimeOffset Spring = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Summer = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Autumn = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Winter = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A Hearth 3 village a Night in (so the first Night's fixed size doesn't apply), at <paramref name="at"/>.</summary>
    private static BaseState Village(DateTimeOffset at)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, at);
        for (var level = 2; level <= 3; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), at);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), at);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), at);
        }

        var data = state.ToData() with { LastNightRecorded = 1 };
        return BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
    }

    [Theory]
    [InlineData("2026-10-09", Season.Spring)]
    [InlineData("2026-10-12", Season.Summer)]
    [InlineData("2026-10-15", Season.Autumn)]
    [InlineData("2026-10-18", Season.Winter)]
    public void Of_ADate_ItsSeason(string date, Season season) =>
        SeasonBook.Of(DateTimeOffset.Parse(date + "T12:00:00Z")).Should().Be(season);

    [Fact]
    public void BeginNight_Summer_SmallerThanSpring()
    {
        // Act
        var spring = Village(Spring).Apply(new BeginNight(Guid.NewGuid()), Spring).Plan!;
        var summer = Village(Summer).Apply(new BeginNight(Guid.NewGuid()), Summer).Plan!;

        // Assert
        summer.Budget.Should().Be(spring.Budget * 85 / 100);
    }

    [Fact]
    public void BeginNight_Winter_RaidersTrudge()
    {
        // Act
        var plan = Village(Winter).Apply(new BeginNight(Guid.NewGuid()), Winter).Plan!;

        // Assert
        plan.EnemySpeedPercent.Should().Be(-15);
        var levy = CultOfComfort.Simulation.Balance.EnemyType.Levy;
        plan.Balance()!.Enemies[levy].TilesPerSecond.Should().BeLessThan(CultOfComfort.Simulation.Balance.BalanceTable.Default.Enemies[levy].TilesPerSecond);
    }

    [Fact]
    public void TonightsRoad_Spring_NeverTheFloodedSouthRoad()
    {
        // Arrange
        var state = Village(Spring);

        // Act
        var roads = Enumerable.Range(2, 40).Select(n => state.TonightsRoad(n, (ulong)(946 + n), Season.Spring)).Distinct().ToList();

        // Assert
        roads.Should().NotContain(SeasonBook.FloodedRoad);
        roads.Should().Contain(new[] { 0, 2 }, "the north and east roads still come");
    }

    [Fact]
    public void BeginNight_Autumn_MoreScrapAfter()
    {
        // Act
        var plan = Village(Autumn).Apply(new BeginNight(Guid.NewGuid()), Autumn).Plan!;

        // Assert
        plan.SalvagePercent.Should().Be(50);
        plan.Budget.Should().BeGreaterThan(Village(Spring).Apply(new BeginNight(Guid.NewGuid()), Spring).Plan!.Budget);
    }
}
