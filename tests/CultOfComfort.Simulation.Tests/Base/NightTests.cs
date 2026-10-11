using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class NightTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    private static NightPlan Begin(BaseState state, DateTimeOffset at)
    {
        var outcome = state.Apply(new BeginNight(Guid.NewGuid()), at);
        outcome.Status.Should().Be(CommandStatus.Accepted);
        return outcome.Plan!;
    }

    [Fact]
    public void BeginNight_EarlyAtZeroTension_HalfThePowerFourWaves()
    {
        // Act
        var plan = Begin(NewBase(), T0);

        // Assert
        plan.Night.Should().Be(1);
        plan.Scheduled.Should().BeFalse();
        plan.Budget.Should().Be(20, "max(40 × 0/70, 40/2)");
        plan.Waves.Should().Be(4);
    }

    [Fact]
    public void BeginNight_ScheduledRaidAt90_BudgetGrowsWithTension()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 90), T0);

        // Act
        var plan = Begin(state, T0);

        // Assert
        plan.Scheduled.Should().BeTrue();
        plan.Budget.Should().Be(48, "40 × (1 + 20/100)");
        plan.Waves.Should().Be(4);
    }

    [Fact]
    public void BeginNight_Punitive_FiveWaves()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 120), T0);

        // Act
        var plan = Begin(state, T0);

        // Assert
        plan.Waves.Should().Be(5);
    }

    [Fact]
    public void ResolveNight_TheVillageReplaysTheBattleItself()
    {
        // Arrange
        var state = NewBase();
        var plan = Begin(state, T0);
        var inputs = new[] { new PlayerInput(300, PlayerActionType.OpenSluiceGate) };
        var expected = Battle.Simulate(plan.ToBattleSetup(VillageMap.Demo, inputs));

        // Act
        var outcome = state.Apply(new ResolveNight(Guid.NewGuid(), plan.Night, inputs), T0.AddMinutes(4));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Report!.Stars.Should().Be(expected.Stars);
        outcome.Report.StateHashHex.Should().Be(expected.StateHashHex);
        outcome.Report.WhileAway.Should().BeFalse();
        var snapshot = state.Snapshot(T0.AddMinutes(4));
        snapshot.LastNightRecorded.Should().Be(1);
        snapshot.Tension.Should().Be(0);
        snapshot.ShieldEndsAtUtc.Should().Be(T0.AddMinutes(4).AddHours(12));
        snapshot.NightInProgress.Should().BeNull();
        snapshot.Stock.Scrap.Should().Be(380 + BuildingTable.Default.Salvage(expected.Stars));
    }

    [Fact]
    public void ResolveNight_WrongNightOrNoneBegun_Rejected()
    {
        // Arrange
        var state = NewBase();

        // Act
        var none = state.Apply(new ResolveNight(Guid.NewGuid(), 1, Array.Empty<PlayerInput>()), T0);
        var plan = Begin(state, T0);
        var wrong = state.Apply(new ResolveNight(Guid.NewGuid(), plan.Night + 1, Array.Empty<PlayerInput>()), T0);

        // Assert
        none.Status.Should().Be(CommandStatus.Rejected);
        wrong.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ResolveNight_InputsOutOfOrder_Rejected()
    {
        // Arrange
        var state = NewBase();
        var plan = Begin(state, T0);
        var forged = new[] { new PlayerInput(500, PlayerActionType.OpenSluiceGate), new PlayerInput(100, PlayerActionType.PhantomVoice) };

        // Act
        var outcome = state.Apply(new ResolveNight(Guid.NewGuid(), plan.Night, forged), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Place_DuringANight_Rejected()
    {
        // Arrange
        var state = NewBase();
        Begin(state, T0);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Snapshot_DeadlinePassedWhileAway_ResolvedOnceAtTheDeadline()
    {
        // Arrange — a raid announced at T0 (warning 8 h); the player stays away three days.
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);

        // Act
        var snapshot = state.Snapshot(T0.AddDays(3));
        var again = state.Snapshot(T0.AddDays(3).AddMinutes(1));

        // Assert
        snapshot.UnseenReport.Should().NotBeNull();
        snapshot.UnseenReport!.WhileAway.Should().BeTrue();
        snapshot.UnseenReport.Night.Should().Be(1);
        snapshot.LastNightRecorded.Should().Be(1);
        snapshot.RaidWarningEndsAtUtc.Should().BeNull();
        again.LastNightRecorded.Should().Be(1, "resolved exactly once");
        var expectedPlanSeed = (ulong)(1_000_003L * 1 + 946);
        snapshot.UnseenReport.StateHashHex.Should().Be(Battle.Simulate(new NightPlan(1, expectedPlanSeed, 42, 4, 0, true,
            state.ToBattleLayout().Towers, state.ToBattleLayout().Obstacles, state.ToBattleLayout().Structures).ToBattleSetup(VillageMap.Demo, Array.Empty<PlayerInput>())).StateHashHex);
    }

    [Fact]
    public void Snapshot_NightBegunAndAbandoned_SettledAfterFifteenMinutes()
    {
        // Arrange
        var state = NewBase();
        Begin(state, T0);

        // Act
        var during = state.Snapshot(T0.AddMinutes(10));
        var after = state.Snapshot(T0.AddMinutes(20));

        // Assert
        during.NightInProgress.Should().NotBeNull();
        after.NightInProgress.Should().BeNull();
        after.UnseenReport!.WhileAway.Should().BeTrue();
        after.ShieldEndsAtUtc.Should().Be(T0.AddMinutes(15).AddHours(12));
    }

    [Fact]
    public void AcknowledgeReport_ClearsIt()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);
        state.Snapshot(T0.AddDays(1)).UnseenReport.Should().NotBeNull();

        // Act
        state.Apply(new AcknowledgeReport(Guid.NewGuid()), T0.AddDays(1)).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0.AddDays(1)).UnseenReport.Should().BeNull();
    }

    [Fact]
    public void Restore_NightInProgressAndReport_RoundTrip()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0);
        state.Snapshot(T0.AddDays(1));
        Begin(state, T0.AddDays(1));

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(T0.AddDays(1).AddMinutes(1)).Should().BeEquivalentTo(state.Snapshot(T0.AddDays(1).AddMinutes(1)));
    }

    private static BaseState HearthTwoBase()
    {
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 60, 380, 0) }, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Snapshot(T0.AddMinutes(10)).HearthLevel.Should().Be(2);
        return state;
    }

    [Fact]
    public void Restore_SaveFromBeforeTension_LoadsCalmAndQuickly()
    {
        // Arrange — a Hearth 2 save written before Tension existed: no Tension clock (ticks 0).
        var old = HearthTwoBase().ToData() with { TensionMilli = 0, TensionAsOfUtcTicks = 0, Raid = null, ShieldEndsAtUtcTicks = null };
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var snapshot = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, old).Snapshot(T0.AddHours(1));

        // Assert
        watch.ElapsedMilliseconds.Should().BeLessThan(1000, "no replay of raids since year 1");
        snapshot.LastNightRecorded.Should().Be(0);
        snapshot.Tension.Should().BeLessThan(10, "Tension starts counting from the save, not from the year 1");
    }

    [Fact]
    public void Snapshot_HearthTwoAwayForAMonth_OnlyOneRaidPlaysItself()
    {
        // Arrange
        var state = HearthTwoBase();

        // Act
        var snapshot = state.Snapshot(T0.AddDays(30));

        // Assert
        snapshot.LastNightRecorded.Should().Be(1, "one raid while away, not one a day");
        snapshot.UnseenReport!.WhileAway.Should().BeTrue();
    }

    [Fact]
    public void BeginNight_EarlyDuringTheShield_Rejected()
    {
        // Arrange — a Night just fought: 12 h shield.
        var state = NewBase();
        var plan = Begin(state, T0);
        state.Apply(new ResolveNight(Guid.NewGuid(), plan.Night, Array.Empty<PlayerInput>()), T0);

        // Act
        var again = state.Apply(new BeginNight(Guid.NewGuid()), T0.AddMinutes(1));
        var afterShield = state.Apply(new BeginNight(Guid.NewGuid()), T0.AddHours(12).AddMinutes(1));

        // Assert
        again.Status.Should().Be(CommandStatus.Rejected);
        afterShield.Status.Should().Be(CommandStatus.Accepted);
    }

    [Fact]
    public void RecordNightResult_DuringANight_Rejected()
    {
        // Arrange
        var state = NewBase();
        Begin(state, T0);

        // Act
        var forged = state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);

        // Assert
        forged.Status.Should().Be(CommandStatus.Rejected);
    }
}

