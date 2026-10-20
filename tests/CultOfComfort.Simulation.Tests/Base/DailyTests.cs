using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class DailyTests
{
    // 10:00 UTC on 15 Oct 2026: on a UTC clock, that day started at 04:00.
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    /// <summary>A Hearth 2 village with the starter errands done and, optionally, today's list fixed.</summary>
    private static BaseState Village(params string[] today)
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        var data = state.ToData() with { ErrandIndex = ErrandBook.Default.Errands.Count, ErrandId = null }; // every errand claimed
        if (today.Length > 0)
        {
            var day = DailyBook.DayOf(T0, 0);
            data = data with { Daily = new DailyData(day, 0, today, today.Select(_ => 0).ToList(), today.Select(_ => false).ToList(), false, 0, int.MinValue, 0, Array.Empty<string>()) };
        }

        return BaseState.Restore(Rich(), VillageMap.Demo, data);
    }

    [Fact]
    public void Snapshot_StarterErrandsNotDone_NoDailyList()
    {
        // Arrange
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);

        // Act / Assert
        state.Snapshot(T0).Daily.Should().BeNull();
    }

    [Fact]
    public void Snapshot_StarterErrandsDone_ThreeErrandsTheSameForEveryCult()
    {
        // Arrange
        var a = Village();
        var b = Village();

        // Act
        var listA = a.Snapshot(T0).Daily!;
        var listB = b.Snapshot(T0.AddHours(3)).Daily!;

        // Assert
        listA.Errands.Should().HaveCount(3);
        listA.Errands.Select(e => e.Id).Should().Equal(listB.Errands.Select(e => e.Id));
        listA.NextListAtUtc.Should().Be(new DateTimeOffset(2026, 10, 16, 4, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void SendMissionary_PreachErrandToday_CountsTheHoursAndPays()
    {
        // Arrange
        var state = Village("preach", "upgrade", "trade");
        var before = state.Snapshot(T0).Stock;

        // Act
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 4), T0).Status.Should().Be(CommandStatus.Accepted);
        var claim = state.Apply(new ClaimDaily(Guid.NewGuid(), 0), T0);

        // Assert
        claim.Status.Should().Be(CommandStatus.Accepted);
        claim.Collected.Should().Be(DailyBook.Reward(DailyBook.Get("preach")!, 2));
        state.Snapshot(T0).Daily!.Errands[0].Claimed.Should().BeTrue();
    }

    [Fact]
    public void ClaimDaily_NotDone_IsRejected()
    {
        // Arrange
        var state = Village("preach", "upgrade", "trade");

        // Act
        var claim = state.Apply(new ClaimDaily(Guid.NewGuid(), 1), T0);

        // Assert
        claim.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ClaimThermos_AllThreeClaimed_TowelsAndAStreak()
    {
        // Arrange — three errands done: preach 4 hours, build two things, upgrade one.
        var state = Village("preach", "upgrade", "build");
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 4), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new Simulation.Grid.GridPosition(15, 5), false), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new Simulation.Grid.GridPosition(22, 13), false), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var id = state.Snapshot(T0).Buildings.First(b => b.Type == BuildingType.Woodcutter).Id;
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0).Status.Should().Be(CommandStatus.Accepted);
        for (var slot = 0; slot < 3; slot++)
        {
            state.Apply(new ClaimDaily(Guid.NewGuid(), slot), T0).Status.Should().Be(CommandStatus.Accepted);
        }

        // Act
        var thermos = state.Apply(new ClaimThermos(Guid.NewGuid()), T0);
        var again = state.Apply(new ClaimThermos(Guid.NewGuid()), T0);

        // Assert
        thermos.Status.Should().Be(CommandStatus.Accepted);
        again.Status.Should().Be(CommandStatus.Rejected);
        var daily = state.Snapshot(T0).Daily!;
        daily.Towels.Should().Be(DailyBook.ThermosTowels);
        daily.Streak.Should().Be(1);
        daily.ThermosClaimed.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_NextDay_NewListAndAMissedDayBreaksTheStreak()
    {
        // Arrange — a streak of 3 whose last Thermos was yesterday.
        var state = Village("preach", "upgrade", "trade");
        var data = state.ToData();
        var today = DailyBook.DayOf(T0, 0);
        state = BaseState.Restore(Rich(), VillageMap.Demo, data with { Daily = data.Daily! with { Streak = 3, LastThermosDay = today - 1 } });

        // Act
        var tomorrow = state.Snapshot(T0.AddDays(1)).Daily!;
        var dayAfter = state.Snapshot(T0.AddDays(2)).Daily!;

        // Assert
        tomorrow.Day.Should().Be(today + 1);
        tomorrow.Errands.Should().OnlyContain(e => !e.Claimed && e.Progress == 0);
        tomorrow.Streak.Should().Be(0, "today had no Thermos, so tomorrow the streak is gone");
        dayAfter.Streak.Should().Be(0);
    }

    [Fact]
    public void ClaimThermos_SeventhDayRunning_TenTowelsAndTheGoldenDuck()
    {
        // Arrange — six Thermoses in a row, the last one yesterday; today's three claimed.
        var state = Village("preach", "upgrade", "trade");
        var data = state.ToData();
        var today = DailyBook.DayOf(T0, 0);
        state = BaseState.Restore(Rich(), VillageMap.Demo, data with
        {
            Daily = data.Daily! with { Streak = 6, LastThermosDay = today - 1, Claimed = new[] { true, true, true }, Progress = new[] { 4, 1, 1 } },
        });

        // Act
        state.Apply(new ClaimThermos(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        var daily = state.Snapshot(T0).Daily!;
        daily.Streak.Should().Be(7);
        daily.Towels.Should().Be(DailyBook.ThermosTowels + DailyBook.StreakTowels);
        daily.Cosmetics.Should().Equal("golden-duck");
    }

    [Fact]
    public void SpendTowels_Reroll_SwapsTheErrandForAnotherOne()
    {
        // Arrange
        var state = Village("preach", "upgrade", "trade");
        var data = state.ToData();
        state = BaseState.Restore(Rich(), VillageMap.Demo, data with { Daily = data.Daily! with { Towels = 5 } });

        // Act
        var outcome = state.Apply(new SpendTowels(Guid.NewGuid(), TowelUse.Reroll, 2), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var daily = state.Snapshot(T0).Daily!;
        daily.Towels.Should().Be(3);
        new[] { "preach", "upgrade", "trade" }.Should().NotContain(daily.Errands[2].Id);
    }

    [Fact]
    public void SpendTowels_NotEnough_IsRejected()
    {
        // Arrange
        var state = Village("preach", "upgrade", "trade");

        // Act
        var outcome = state.Apply(new SpendTowels(Guid.NewGuid(), TowelUse.Reroll, 0), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void SpendTowels_Hurry_TakesHalfAnHourOffTheConstruction()
    {
        // Arrange — a Storage Hut under way.
        var state = Village("preach", "upgrade", "trade");
        var data = state.ToData();
        state = BaseState.Restore(Rich(), VillageMap.Demo, data with { Daily = data.Daily! with { Towels = 1 } });
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new Simulation.Grid.GridPosition(22, 13), false), T0).Status.Should().Be(CommandStatus.Accepted);
        var ends = state.Snapshot(T0).ConstructionEndsAtUtc!.Value;

        // Act
        state.Apply(new SpendTowels(Guid.NewGuid(), TowelUse.Hurry), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        var now = state.Snapshot(T0).ConstructionEndsAtUtc;
        (now == null || now.Value <= ends.AddMinutes(-DailyBook.HurryMinutes)).Should().BeTrue();
    }

    [Theory]
    [InlineData(3, 59, -1)]
    [InlineData(4, 0, 0)]
    [InlineData(23, 59, 0)]
    public void DayOf_FourInTheMorning_IsWhenTheDayTurns(int hour, int minute, int dayDelta)
    {
        // Arrange — a player two hours ahead of UTC (CEST).
        var local = new DateTimeOffset(2026, 10, 15, hour, minute, 0, TimeSpan.FromHours(2));
        var morning = new DateTimeOffset(2026, 10, 15, 4, 0, 0, TimeSpan.FromHours(2));

        // Act
        var day = DailyBook.DayOf(local, 120);

        // Assert
        day.Should().Be(DailyBook.DayOf(morning, 120) + dayDelta);
    }

    [Fact]
    public void Restore_DailyList_Survives()
    {
        // Arrange
        var state = Village("preach", "upgrade", "trade");
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 2), T0);

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(T0).Daily.Should().BeEquivalentTo(state.Snapshot(T0).Daily);
    }
}
