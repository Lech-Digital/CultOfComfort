using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 the Prophet grows: experience from Nights, levels, perks at 3, 5, 7 and 9.</summary>
public class ProphetTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static BaseState WithXp(int xp, params string[] perks)
    {
        var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData() with { ProphetXp = xp, ProphetPerks = perks };
        return BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(150, 3)]
    [InlineData(100000, ProphetBook.MaxLevel)]
    public void LevelOf_Xp_ClimbsTheLadder(int xp, int level) => ProphetBook.LevelOf(xp).Should().Be(level);

    [Fact]
    public void RecordNight_ThreeStars_HeLearns()
    {
        // Arrange
        var state = WithXp(0);

        // Act
        state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);

        // Assert
        state.Snapshot(T0).Prophet!.Xp.Should().Be(ProphetBook.XpForNight(3));
    }

    [Fact]
    public void ChoosePerk_AtLevelThree_OneOfThePairAndThenNoMore()
    {
        // Arrange
        var state = WithXp(150);
        state.Snapshot(T0).Prophet!.Choice!.Select(p => p.Id).Should().BeEquivalentTo(new[] { "bathrobe", "slippers" });

        // Act
        var first = state.Apply(new ChooseProphetPerk(Guid.NewGuid(), "slippers"), T0);
        var second = state.Apply(new ChooseProphetPerk(Guid.NewGuid(), "bathrobe"), T0);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        second.Status.Should().Be(CommandStatus.Rejected, "one perk a level");
        state.Snapshot(T0).Prophet!.Choice.Should().BeNull();
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData()).Snapshot(T0).Prophet!.Perks.Select(p => p.Id).Should().Equal("slippers");
    }

    [Fact]
    public void ChoosePerk_BeforeItsLevel_Rejected()
    {
        // Act
        var outcome = WithXp(150).Apply(new ChooseProphetPerk(Guid.NewGuid(), "megaphone"), T0);

        // Assert
        outcome.Reason.Should().Contain("level 5");
    }

    [Fact]
    public void PlanNight_LevelThreeWithTheBathrobe_HealthAndBiteInThePlanAndTheBattle()
    {
        // Arrange — level 3 (+8% health, +10% damage) and the bathrobe (+20% health).
        var state = WithXp(150, "bathrobe");
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;
        var balance = plan.Balance()!;

        // Assert
        plan.ProphetHealthPercent.Should().Be(28);
        plan.ProphetDamagePercent.Should().Be(10);
        balance.Leader.MaxHealth.Should().Be(CultOfComfort.Simulation.Balance.BalanceTable.Default.Leader.MaxHealth * 128 / 100);
    }

    [Fact]
    public void PlanNight_LevelOneNoPerks_TheDefaultBalance()
    {
        // Act
        var plan = WithXp(0).Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Balance().Should().BeNull("a new Prophet fights as every Prophet did before");
    }
}
