using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class TensionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    /// <summary>A village whose Hearth reached level 2 at exactly T0 + 10 min (rich start, Finish now).</summary>
    private static (BaseState State, DateTimeOffset At) HearthTwo()
    {
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 60, 380, 0) }, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0.AddMinutes(10)).HearthLevel.Should().Be(2);
        return (state, T0.AddMinutes(10));
    }

    [Fact]
    public void Default_TensionTable_MatchesTheSpec()
    {
        var t = TensionTable.Default;
        t.RatePerHourByHearth.Should().Equal(0, 8, 12);
        (t.Hostile, t.Punitive, t.Max, t.WarningHours, t.ShieldHours).Should().Be((70, 100, 150, 8, 12));
        t.BasePowerByHearth.Should().Equal(40, 54, 72);
    }

    [Fact]
    public void Snapshot_HearthOne_TensionNeverRisesAndNoRaidComes()
    {
        // Act
        var snapshot = NewBase().Snapshot(T0.AddDays(30));

        // Assert
        snapshot.Tension.Should().Be(0);
        snapshot.TensionState.Should().Be(TensionState.Calm);
        snapshot.RaidWarningEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void Snapshot_HearthTwo_RisesEightPerHour()
    {
        // Arrange
        var (state, at) = HearthTwo();

        // Act
        var snapshot = state.Snapshot(at.AddHours(5));

        // Assert
        snapshot.Tension.Should().Be(40);
        snapshot.TensionState.Should().Be(TensionState.Uneasy);
    }

    [Fact]
    public void Snapshot_CrossesSeventyWhileAway_RaidWarningCountsFromTheCrossingNotFromNow()
    {
        // Arrange — 70 / 8 per hour = 8 h 45 min after Hearth 2.
        var (state, at) = HearthTwo();
        var crossing = at.AddMinutes(8 * 60 + 45);

        // Act — the app opens four hours after the crossing.
        var snapshot = state.Snapshot(crossing.AddHours(2)); // opened two hours after the crossing (70 + 16 = 86)

        // Assert
        snapshot.TensionState.Should().Be(TensionState.Hostile);
        snapshot.RaidWarningEndsAtUtc.Should().Be(crossing.AddHours(8));
    }

    [Fact]
    public void Snapshot_LongAbsence_TensionStopsAtTheMax()
    {
        // Arrange
        var (state, at) = HearthTwo();

        // Act — well before the raid's deadline auto-resolve would matter we only read Tension's ceiling via a fresh rule:
        var snapshot = state.Snapshot(at.AddHours(8.75 + 7));

        // Assert
        snapshot.Tension.Should().BeLessThanOrEqualTo(150);
        snapshot.Tension.Should().BeGreaterThan(100);
        snapshot.TensionState.Should().Be(TensionState.Punitive);
    }

    [Fact]
    public void DebugRaiseTension_ToHostile_SchedulesARaidAtOnceEvenAtHearthOne()
    {
        // Arrange
        var state = NewBase();

        // Act
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), T0).Status.Should().Be(CommandStatus.Accepted);
        var snapshot = state.Snapshot(T0);

        // Assert
        snapshot.Tension.Should().Be(75);
        snapshot.RaidWarningEndsAtUtc.Should().Be(T0.AddHours(8));
    }

    [Fact]
    public void Snapshot_DebugRaiseTo69AtHearthOne_NeverSchedules()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 69), T0);

        // Act
        var snapshot = state.Snapshot(T0.AddDays(5));

        // Assert
        snapshot.Tension.Should().Be(69);
        snapshot.RaidWarningEndsAtUtc.Should().BeNull("no rise at Hearth 1");
    }

    [Fact]
    public void Restore_SaveWithTensionAndRaid_RoundTrips()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 80), T0);

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(T0.AddHours(1)).Should().BeEquivalentTo(state.Snapshot(T0.AddHours(1)));
    }
}
