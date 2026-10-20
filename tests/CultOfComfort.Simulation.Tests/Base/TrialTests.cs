using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 the Pilgrims' Way: a ladder of challenge Nights, any time, wrecking nothing.</summary>
public class TrialTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static BaseState Village() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    [Fact]
    public void BeginTrial_WhileShielded_TheFirstStage()
    {
        // Arrange — shielded for eight hours.
        var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, Village().ToData() with { ShieldEndsAtUtcTicks = T0.AddHours(8).UtcTicks });
        state.Snapshot(T0).ShieldEndsAtUtc.Should().NotBeNull();

        // Act
        var outcome = state.Apply(new BeginTrial(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted, "the pilgrims don't care about shields");
        outcome.Plan!.Night.Should().Be(1);
        outcome.Plan.Budget.Should().Be(TrialBook.Budget(1));
        state.Snapshot(T0).Trial!.Walking.Should().BeTrue();
    }

    [Fact]
    public void ResolveTrial_ReplayedResultDecides_ClearedPaysAndClimbs()
    {
        // Arrange
        var state = Village();
        var plan = state.Apply(new BeginTrial(Guid.NewGuid()), T0).Plan!;
        var expected = new Battle(plan.ToBattleSetup(VillageMap.Demo, Array.Empty<PlayerInput>())).RunToCompletion();
        var before = state.Snapshot(T0).Stock;

        // Act
        var outcome = state.Apply(new ResolveTrial(Guid.NewGuid(), 1, Array.Empty<PlayerInput>()), T0);

        // Assert
        outcome.Report!.Stars.Should().Be(expected.Stars);
        var after = state.Snapshot(T0);
        after.Trial!.Cleared.Should().Be(expected.Stars >= 1 ? 1 : 0);
        after.Trial.Walking.Should().BeFalse();
        if (expected.Stars >= 1)
        {
            (after.Stock.Timber - before.Timber).Should().BeGreaterThan(0, "a cleared stage pays");
        }

        after.Buildings.Should().NotContain(b => b.Wrecked, "a trial wrecks nothing");
    }

    [Fact]
    public void ResolveTrial_TheWrongStage_Rejected()
    {
        // Arrange
        var state = Village();
        state.Apply(new BeginTrial(Guid.NewGuid()), T0);

        // Act & Assert
        state.Apply(new ResolveTrial(Guid.NewGuid(), 5, Array.Empty<PlayerInput>()), T0).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ClaimAlms_FiveStagesTwoHours_TwoHoursOfAlmsAndThenEmpty()
    {
        // Arrange
        var data = Village().ToData() with { TrialsCleared = 5, AlmsAsOfUtcTicks = T0.AddHours(-2).UtcTicks };
        var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
        var rate = TrialBook.AlmsPerHour(5);
        state.Snapshot(T0).Trial!.AlmsWaiting.Timber.Should().Be(rate.Timber * 2);

        // Act
        var outcome = state.Apply(new ClaimAlms(Guid.NewGuid()), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Trial!.AlmsWaiting.IsZero.Should().BeTrue();
        state.Apply(new ClaimAlms(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Rejected, "empty");
    }

    [Fact]
    public void Alms_AWeekAway_CappedAtTwelveHours()
    {
        // Arrange
        var data = Village().ToData() with { TrialsCleared = 3, AlmsAsOfUtcTicks = T0.AddDays(-7).UtcTicks };

        // Act
        var waiting = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(T0).Trial!.AlmsWaiting;

        // Assert
        waiting.Timber.Should().Be(TrialBook.AlmsPerHour(3).Timber * TrialBook.AlmsCapHours);
    }
}
