using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 the festival: seven themed days a week, three reward tiers each.</summary>
public class FestivalTests
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Wednesday = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private static BaseState Village(DateTimeOffset at)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, at);
        state.Apply(new DebugFillStores(Guid.NewGuid()), at);
        return state;
    }

    [Fact]
    public void Days_ByTheCalendar_MondayIsBuildersDay()
    {
        // Assert
        FestivalBook.On(FestivalBook.DayOf(Monday)).Name.Should().Be("Builders' Day");
        FestivalBook.On(FestivalBook.DayOf(Wednesday)).Name.Should().Be("Hospitality");
    }

    [Fact]
    public void BuildersDay_ACottage_CountsItsMinutes()
    {
        // Arrange
        var state = Village(Monday);

        // Act — a cottage takes a minute to build.
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 15)), Monday).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(Monday).Festival!.Points.Should().Be(BuildingTable.Default.Spec(BuildingType.Cottage).Levels[0].BuildSeconds / 60);
    }

    [Fact]
    public void Hospitality_BuildingCountsNothing()
    {
        // Arrange
        var state = Village(Wednesday);

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 15)), Wednesday);

        // Assert
        state.Snapshot(Wednesday).Festival!.Points.Should().Be(0, "only visitors count on Hospitality");
    }

    [Fact]
    public void ClaimFestival_EnoughPoints_PaysOnceThenNotAgain_AndTheNextDayStartsFresh()
    {
        // Arrange — enough Builders' Day points for the first tier.
        var data = Village(Monday).ToData() with { FestivalDay = FestivalBook.DayOf(Monday), FestivalPoints = 150, FestivalClaimed = new[] { false, false, false } };
        var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
        var towels = state.Snapshot(Monday).Towels;

        // Act
        var first = state.Apply(new ClaimFestival(Guid.NewGuid(), 0), Monday);
        var again = state.Apply(new ClaimFestival(Guid.NewGuid(), 0), Monday);
        var tooFar = state.Apply(new ClaimFestival(Guid.NewGuid(), 1), Monday);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(Monday).Towels.Should().Be(towels + FestivalBook.Days[0].Tiers[0].Towels);
        again.Status.Should().Be(CommandStatus.Rejected);
        tooFar.Reason.Should().Contain("more points");
        state.Snapshot(Monday.AddDays(1)).Festival!.Points.Should().Be(0, "a new day, a new theme");
    }
}
