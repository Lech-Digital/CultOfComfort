using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class RoadTaleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DomainNode Muddlecombe = DomainMap.Marchlands.Node(1)!; // hamlet, 1 hex: 20 min each way

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(4000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    /// <summary>A Hearth 2 village whose preacher has been out once, 2 hours, and is home with a tale.</summary>
    private static (BaseState State, DateTimeOffset Now, TaleView Tale) Back()
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 2), T0).Status.Should().Be(CommandStatus.Accepted);
        var now = T0.AddHours(3);
        return (state, now, state.Snapshot(now).Domain!.Tales!.Single());
    }

    [Fact]
    public void ChooseTale_AnEndingThatGives_GrantsItAndWritesTheReport()
    {
        // Arrange
        var (state, now, tale) = Back();
        var choice = tale.Options.ToList().FindIndex(o => o.ChancePercent == 100 && o.Cost.IsZero && !o.Gift.IsZero);
        choice = choice < 0 ? 0 : choice;
        var option = tale.Options[choice];
        var before = state.Snapshot(now).Stock;

        // Act
        var outcome = state.Apply(new ChooseTale(Guid.NewGuid(), tale.Id, choice), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(now);
        after.Stock.Should().Be(before + option.Gift - option.Cost);
        after.Domain!.Tales.Should().BeEmpty();
        after.Domain.Reports.Should().ContainSingle().Which.Tale.Should().Be(tale.Tale);
    }

    [Fact]
    public void ChooseTale_Twice_IsRejected()
    {
        // Arrange
        var (state, now, tale) = Back();
        state.Apply(new ChooseTale(Guid.NewGuid(), tale.Id, 0), now);

        // Act
        var outcome = state.Apply(new ChooseTale(Guid.NewGuid(), tale.Id, 1), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("That story's been told.");
    }

    [Fact]
    public void ChooseTale_NoSuchEnding_IsRejected()
    {
        // Arrange
        var (state, now, tale) = Back();

        // Act
        var outcome = state.Apply(new ChooseTale(Guid.NewGuid(), tale.Id, 2), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(now).Domain!.Tales.Should().ContainSingle();
    }

    [Fact]
    public void Options_ALuckyPreacher_FindsTwiceAsMuch()
    {
        // Arrange — preacher 3 is the Lucky one, preacher 2 the Thrifty.
        // Act
        var lucky = Tales.Options(RoadTale.Goat, 3, 2);
        var plain = Tales.Options(RoadTale.Goat, 2, 2);

        // Assert
        lucky[0].Gift.Timber.Should().Be(2 * plain[0].Gift.Timber);
        plain[0].Gift.Timber.Should().Be(16, "8 Timber an hour preached");
    }

    [Fact]
    public void ComeHome_MoreTalesThanKept_TheOldestEndsTheFreeWay()
    {
        // Arrange — tales pile up while nobody chooses.
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var now = T0;
        for (var i = 0; i <= Tales.Kept; i++)
        {
            state.Apply(new SendMissionary(Guid.NewGuid(), Muddlecombe.Id, 1), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
            state.Snapshot(now);
        }

        // Act
        var domain = state.Snapshot(now).Domain!;

        // Assert
        domain.Tales.Should().HaveCount(Tales.Kept);
        domain.Reports.Should().ContainSingle("the oldest tale ended on its own");
    }

    [Fact]
    public void FreeChoice_TheTollOrTheShrine_NeverCostsAnything()
    {
        // Act / Assert
        foreach (var tale in Enum.GetValues<RoadTale>())
        {
            var options = Tales.Options(tale, 1, 4);
            var free = options[Tales.FreeChoice(options)];
            free.Cost.IsZero.Should().BeTrue($"{tale} ends for free when nobody listens");
            free.Tension.Should().Be(0);
        }
    }
}

public class PleaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    /// <summary>A Hearth 2 village with the lord's first demand open, Tension at 50.</summary>
    private static (BaseState State, DateTimeOffset Now, DemandView Demand) Asked()
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        var now = T0.AddHours(11);
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 50 - state.Snapshot(now).Tension), now);
        return (state, now, state.Snapshot(now).Domain!.Demand!);
    }

    [Fact]
    public void Stall_Once_MoreTimeForSomeTemper()
    {
        // Arrange
        var (state, now, demand) = Asked();
        var tension = state.Snapshot(now).Tension;

        // Act
        var outcome = state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Stall), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(now);
        after.Domain!.Demand!.DueAtUtc.Should().Be(demand.DueAtUtc.AddHours(Demands.StallHours));
        after.Domain.Demand.Stalled.Should().BeTrue();
        after.Tension.Should().Be(tension + Demands.StallPenalty);
    }

    [Fact]
    public void Stall_Twice_IsRejected()
    {
        // Arrange
        var (state, now, demand) = Asked();
        state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Stall), now);

        // Act
        var outcome = state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Stall), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("He won't wait twice.");
    }

    [Fact]
    public void Haggle_AfterFlattery_AlwaysWorksAndTheDemandShrinks()
    {
        // Arrange
        var (state, now, demand) = Asked();
        var devotion = state.Snapshot(now).Stock.Devotion;
        state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Flatter), now).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Haggle), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(now);
        after.Stock.Devotion.Should().Be(devotion - Demands.FlatterCost(2).Devotion);
        after.Domain!.Demand!.Haggle.Should().Be(DemandHaggle.Won);
        after.Domain.Demand.Amount.Should().Be(Demands.Haggled(demand.Amount));
    }

    [Fact]
    public void Haggle_WithoutFlattery_EitherShrinksTheDemandOrAngersHim()
    {
        // Arrange
        var (state, now, demand) = Asked();
        var tension = state.Snapshot(now).Tension;

        // Act
        state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Haggle), now).Status.Should().Be(CommandStatus.Accepted);
        var again = state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Haggle), now);

        // Assert
        var after = state.Snapshot(now);
        if (Demands.HaggleWorks(demand.Id, flattered: false))
        {
            after.Domain!.Demand!.Amount.Should().Be(Demands.Haggled(demand.Amount));
            after.Tension.Should().Be(tension);
        }
        else
        {
            after.Domain!.Demand!.Amount.Should().Be(demand.Amount);
            after.Tension.Should().Be(tension + Demands.RefusePenalty);
        }

        again.Status.Should().Be(CommandStatus.Rejected, "one haggle per demand");
    }

    [Fact]
    public void Plead_AnotherDemand_IsRejected()
    {
        // Arrange
        var (state, now, demand) = Asked();

        // Act
        var outcome = state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id + 1, Plea.Flatter), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Restore_APleadedDemand_KeepsWhatWasTried()
    {
        // Arrange
        var (state, now, demand) = Asked();
        state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Stall), now);
        state.Apply(new PleadWithLord(Guid.NewGuid(), demand.Id, Plea.Flatter), now);

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, state.ToData()).Snapshot(now).Domain!.Demand!;

        // Assert
        restored.Stalled.Should().BeTrue();
        restored.Flattered.Should().BeTrue();
        restored.HaggleChancePercent.Should().Be(100);
    }
}
