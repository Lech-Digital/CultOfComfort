using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>The first fifteen minutes (spec 2026-11-17 fun first, E): the micro-raid, errands with a choice, Towels from fighting.</summary>
public class FirstMinutesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Field = new(3, 9);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    private static BaseState PastTheWoodcutter()
    {
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new ClaimErrand(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        return state;
    }

    [Fact]
    public void Default_ErrandBook_AFightInsideThreeMinutesThenTheChuteThenTwoChoicesThenTheNight()
    {
        // Act
        var ids = ErrandBook.Default.Errands.Take(6).Select(e => e.Id);

        // Assert
        ids.Should().Equal("woodcutter", "micro-raid", "slurry-chute", "cottage", "peat-cut", "first-night");
        ErrandBook.Default.Errands[3].Or.Should().Be(BuildingType.StorageHut);
        ErrandBook.Default.Errands[4].Or.Should().Be(BuildingType.CultistHut);
    }

    [Fact]
    public void BeginTrial_OnTheMicroRaidErrand_FourNosyLocalsAndNothingElse()
    {
        // Arrange
        var state = PastTheWoodcutter();
        state.Snapshot(T0).Errand!.Goal.Should().Be(ErrandGoal.MicroRaid);

        // Act
        var plan = state.Apply(new BeginTrial(Guid.NewGuid()), T0).Plan!;
        var setup = plan.ToBattleSetup(VillageMap.Demo, Array.Empty<PlayerInput>());
        var battle = new Battle(setup);
        battle.RunToCompletion();

        // Assert
        plan.Night.Should().Be(0);
        plan.Towers.Should().BeEmpty("the Prophet meets them alone");
        battle.Enemies.Should().HaveCount(4).And.OnlyContain(e => e.Type == CultOfComfort.Simulation.Balance.EnemyType.Peasant);
        battle.WaveCount.Should().Be(1);
        battle.Result!.TicksElapsed.Should().BeLessThan(90 * CultOfComfort.Simulation.Balance.BalanceTable.TicksPerSecond, "a mini Night");
    }

    [Fact]
    public void ResolveTrial_TheMicroRaid_DoesTheErrandAndCountsAsNoNight()
    {
        // Arrange
        var state = PastTheWoodcutter();
        state.Apply(new BeginTrial(Guid.NewGuid()), T0);

        // Act
        var resolved = state.Apply(new ResolveTrial(Guid.NewGuid(), 0, Array.Empty<PlayerInput>()), T0.AddMinutes(1));
        var snapshot = state.Snapshot(T0.AddMinutes(1));

        // Assert
        resolved.Status.Should().Be(CommandStatus.Accepted);
        snapshot.Errand!.Done.Should().BeTrue();
        snapshot.LastNightRecorded.Should().Be(0, "not a Night");
        snapshot.ShieldEndsAtUtc.Should().BeNull("no shield after the locals");
        snapshot.Trial!.Cleared.Should().Be(0, "not a Pilgrims' stage either");
    }

    [Fact]
    public void Progress_ChoiceErrand_EitherBuildingCounts()
    {
        // Arrange — on the Cottage-or-Storage-Hut errand.
        var book = new ErrandBook("test", new[] { ErrandBook.Default.Errands.Single(e => e.Id == "cottage") });
        var state = BaseState.CreateNew(new BuildingTable { Errands = book }, VillageMap.Demo, T0);

        // Act — the Storage Hut, the other choice.
        var placed = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 30).Select(y => new GridPosition(x, y)))
            .Select(p => state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, p), T0))
            .First(o => o.Status == CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Assert
        placed.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Errand!.Done.Should().BeTrue();
    }

    [Fact]
    public void IndexOf_ASaveOnAFoldedErrand_CarriesOnWithTheOneThatTookItIn()
    {
        // Act & Assert — a player saved mid-way on the old Storage Hut or Cultist Hut errands keeps an errand.
        var book = ErrandBook.Default;
        book.IndexOf("storage-hut", 0).Should().Be(book.IndexOf("cottage", 0));
        book.IndexOf("cultist-hut", 0).Should().Be(book.IndexOf("peat-cut", 0));
    }

    [Fact]
    public void Claim_AnErrand_PaysNoTowel()
    {
        // Arrange
        var state = NewBase();
        var before = state.Snapshot(T0).Towels;
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Act
        state.Apply(new ClaimErrand(Guid.NewGuid()), T0);

        // Assert — Towels come from the Night's stars and the Pilgrims' Way now (E4).
        state.Snapshot(T0).Towels.Should().Be(before);
    }

    [Fact]
    public void NightTowels_OnePerStarAboveTheFirst()
    {
        // Act & Assert
        DailyBook.NightTowels(0).Should().Be(0);
        DailyBook.NightTowels(1).Should().Be(0);
        DailyBook.NightTowels(3).Should().Be(2);
        TrialBook.Reward(1).Towels.Should().Be(1);
        TrialBook.Reward(5).Towels.Should().Be(2);
    }

    [Fact]
    public void Snapshot_FreshVillage_TwoBuildersFree()
    {
        // Act & Assert (E3)
        NewBase().Snapshot(T0).FreeBuilders.Should().Be(2);
    }
}
