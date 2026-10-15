using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class MissionHouseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DomainNode Muddlecombe = DomainMap.Marchlands.Node(1)!; // hamlet, 1 hex

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    private static (BaseState State, DateTimeOffset Now) Village(int hearth)
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        var now = T0;
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        return (state, now);
    }

    [Fact]
    public void Snapshot_HearthThree_TwoNamedPreachersWithTheirQuirks()
    {
        // Arrange
        var (state, now) = Village(3);

        // Act
        var preachers = state.Snapshot(now).Domain!.Preachers!;

        // Assert
        preachers.Should().HaveCount(2);
        preachers[0].Name.Should().Be("Brother Damp");
        preachers[0].Quirk.Should().Be(PreacherQuirk.Tireless, "the first preacher wins no extra faith: the early map is no faster");
        preachers[1].Quirk.Should().Be(PreacherQuirk.Thrifty);
        preachers.Should().OnlyContain(p => p.Level == 1 && p.Xp == 0 && p.MissionaryId == null);
    }

    [Fact]
    public void Send_APreacherAlreadyOut_IsRejected()
    {
        // Arrange
        var (state, now) = Village(3);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 2, PreacherId: 1), now).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 2, PreacherId: 1), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("Brother Damp is already out.");
    }

    [Fact]
    public void Send_NoPreacherNamed_TheFirstFreeOneGoes()
    {
        // Arrange
        var (state, now) = Village(3);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 2, PreacherId: 1), now);

        // Act
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 2), now).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(now).Domain!.Missionaries.Select(m => m.PreacherId).Should().Equal(1, 2);
    }

    [Fact]
    public void Send_TheThriftyPreacher_PaysLessForProvisions()
    {
        // Arrange — 4 hours: 100 Timber and 40 Devotion for anyone else.
        var (state, now) = Village(3);
        var before = state.Snapshot(now).Stock;

        // Act
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 4, PreacherId: 2), now).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        var after = state.Snapshot(now).Stock;
        (before.Timber - after.Timber).Should().Be(70);
        (before.Devotion - after.Devotion).Should().Be(28);
    }

    [Fact]
    public void ComeHome_AfterASermon_EarnsExperienceAndWritesAReport()
    {
        // Arrange — Brother Damp (Tireless) preaches 8 hours, 1 hex away.
        var (state, now) = Village(2);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 8), now);

        // Act
        var later = state.Snapshot(now.AddHours(9)).Domain!;

        // Assert
        var damp = later.Preachers!.Single();
        damp.Xp.Should().BeGreaterThanOrEqualTo(24, "3 an hour for the Tireless, plus any turnips");
        damp.Level.Should().Be(2);
        damp.MissionaryId.Should().BeNull("he's home");
        later.Reports.Should().ContainSingle().Which.Should().Match<RoadReport>(r => r.PreacherId == 1 && r.NodeId == Muddlecombe.Id && r.Hours == 8);
    }

    [Fact]
    public void Missionary_ALevelledPreacher_CarriesTheirBonus()
    {
        // Arrange — 8 hours makes Brother Damp level 2 (+6%).
        var (state, now) = Village(2);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 8), now);
        var back = now.AddHours(9);

        // Act
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 1), back).Status.Should().Be(CommandStatus.Accepted);

        // Assert — the server reads this for the shared world.
        state.Snapshot(back).Domain!.Missionaries.Single().BonusPercent.Should().Be(Preachers.BonusPerLevelPercent);
    }

    [Fact]
    public void Restore_PreachersAndReports_Survive()
    {
        // Arrange
        var (state, now) = Village(2);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 8), now);
        var later = now.AddHours(9);
        var before = state.Snapshot(later).Domain!;

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, state.ToData()).Snapshot(later).Domain!;

        // Assert
        restored.Preachers.Should().BeEquivalentTo(before.Preachers);
        restored.Reports.Should().BeEquivalentTo(before.Reports);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(15, 1)]
    [InlineData(16, 2)]
    [InlineData(224, 5)]
    [InlineData(9999, 5)]
    public void Level_Experience_GivesTheLevel(int xp, int level)
    {
        // Act / Assert
        Preachers.Level(xp).Should().Be(level);
    }

    [Fact]
    public void Find_SameMissionary_SameFind()
    {
        // Act
        var a = Preachers.Find(17, 3, 4);
        var b = Preachers.Find(17, 3, 4);

        // Assert
        a.Should().Be(b);
    }
}

public class DemandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    /// <summary>A Hearth 2 village; the Hearth finished at <c>hearthDone</c>.</summary>
    private static (BaseState State, DateTimeOffset HearthDone) Village()
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return (state, T0);
    }

    [Fact]
    public void Demand_TenHoursAfterHearthTwo_TheLordAsks()
    {
        // Arrange
        var (state, done) = Village();

        // Act
        var before = state.Snapshot(done.AddHours(9.9)).Domain!.Demand;
        var demand = state.Snapshot(done.AddHours(10.1)).Domain!.Demand;

        // Assert
        before.Should().BeNull();
        demand.Should().NotBeNull();
        demand!.Id.Should().Be(1);
        demand.DueAtUtc.Should().Be(done.AddHours(10 + Demands.WindowHours));
        DomainTable.TitheValue(demand.Amount).Should().BeGreaterThan(0);
    }

    [Fact]
    public void MeetDemand_Paid_TemperDropsTwiceATithesWorth()
    {
        // Arrange
        var (state, done) = Village();
        var at = done.AddHours(11);
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 80), at);
        var demand = state.Snapshot(at).Domain!.Demand!;
        var before = state.Snapshot(at);

        // Act
        var outcome = state.Apply(new MeetDemand(Guid.NewGuid(), demand.Id), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(at);
        after.Tension.Should().Be(Math.Max(0, before.Tension - DomainTable.TitheValue(demand.Amount) * 2 / DomainTable.Default.TitheValuePerTension));
        after.Stock.Timber.Should().Be(before.Stock.Timber - demand.Amount.Timber);
        after.Domain!.Demand.Should().BeNull();
    }

    [Fact]
    public void MeetDemand_AnotherId_IsRejected()
    {
        // Arrange
        var (state, done) = Village();
        var at = done.AddHours(11);
        var demand = state.Snapshot(at).Domain!.Demand!;

        // Act
        var outcome = state.Apply(new MeetDemand(Guid.NewGuid(), demand.Id + 1), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(at).Domain!.Demand.Should().NotBeNull();
    }

    [Fact]
    public void Demand_Missed_TemperJumpsAndTheNextComesLater()
    {
        // Arrange
        var (state, done) = Village();
        var issued = done.AddHours(10);
        var due = issued.AddHours(Demands.WindowHours);
        var tensionBefore = state.Snapshot(due.AddMinutes(-1)).Tension;

        // Act
        var after = state.Snapshot(due.AddMinutes(1));

        // Assert
        after.Domain!.Demand.Should().BeNull();
        after.Tension.Should().BeGreaterThanOrEqualTo(tensionBefore + Demands.MissPenalty);
        state.Snapshot(due + Demands.Gap(2) + TimeSpan.FromMinutes(1)).Domain!.Demand!.Id.Should().Be(2);
    }

    [Fact]
    public void Demand_LookingOftenOrRarely_SameDemandsAndTemper()
    {
        // Arrange — lazy evaluation must not depend on how often anyone looks (no raid in 30 hours at Hearth 2).
        var (often, done) = Village();
        var (rarely, _) = Village();

        // Act
        for (var h = 1; h < 30; h++)
        {
            often.Snapshot(done.AddHours(h));
        }

        var end = done.AddHours(30);

        // Assert
        often.Snapshot(end).Tension.Should().Be(rarely.Snapshot(end).Tension);
        often.Snapshot(end).Domain!.Demand.Should().BeEquivalentTo(rarely.Snapshot(end).Domain!.Demand);
    }

    [Fact]
    public void Restore_SaveFromBeforeDemands_FirstOneTenHoursAfterItsLatestMoment()
    {
        // Arrange — a Hearth 2 save with no demand clock at all.
        var (state, done) = Village();
        var data = state.ToData();
        var old = data with { Domain = data.Domain! with { NextDemandUtcTicks = null, Demand = null, NextDemandId = 1 } };
        var latest = new DateTimeOffset(old.TensionAsOfUtcTicks, TimeSpan.Zero);

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, old);

        // Assert
        restored.Snapshot(latest.AddHours(9.9)).Domain!.Demand.Should().BeNull();
        restored.Snapshot(latest.AddHours(10.1)).Domain!.Demand.Should().NotBeNull();
    }
}
