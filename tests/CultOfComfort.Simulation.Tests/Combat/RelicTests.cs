using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class RelicTests
{
    private const int All = (int)(Relic.LaserPointer | Relic.DynamoTorch | Relic.Magnifier);
    private static readonly GridPosition OnTheRoad = new(4, 1);

    /// <summary>A 10×3 corridor; budget 1 is a single Levy that spawns at (0,1) on tick 40 and walks right at 1 tile/s.</summary>
    private static Battle StartBattle(Relic relics, int budget, int waves, GridPosition leaderStart, params PlayerInput[] inputs)
    {
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], leaderStart);
        var balance = new BalanceTable { HearthMaxHealth = 100_000, Leader = BalanceTable.Default.Leader with { AttackDamage = Fix.Zero } };
        return new Battle(new BattleSetup(layout, Seed: 42, budget, waves, inputs, balance) { Relics = relics });
    }

    private static List<BattleEvent> StepUntil(Battle battle, int tick)
    {
        var events = new List<BattleEvent>();
        while (!battle.IsFinished && battle.Tick <= tick)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        return events;
    }

    [Theory]
    [InlineData(1, (int)Relic.None)]
    [InlineData(2, (int)(Relic.LaserPointer | Relic.DynamoTorch))]
    [InlineData(3, All)]
    public void ForHearth_EachLevel_UnlocksMoreOfTheBoot(int hearth, int expected)
    {
        // Act
        var relics = RelicBook.ForHearth(hearth);

        // Assert
        relics.Should().Be((Relic)expected);
    }

    [Fact]
    public void Step_LaserOnALevy_BlindsItWhereItStands()
    {
        // Arrange
        var battle = StartBattle((Relic)All, 1, 1, OnTheRoad, new PlayerInput(60, PlayerActionType.LaserPointer, new GridPosition(1, 1)));
        StepUntil(battle, 59);

        // Act
        var events = StepUntil(battle, 60);
        var at = battle.Enemies.Single().DistanceAlongPath;
        StepUntil(battle, 90);

        // Assert
        events.OfType<RelicUsed>().Should().ContainSingle().Which.TargetId.Should().Be(battle.Enemies.Single().Id);
        battle.Enemies.Single().DistanceAlongPath.Should().Be(at, "blinded raiders don't walk");
        battle.IsLaserReady.Should().BeFalse();
    }

    [Fact]
    public void Step_LaserWithNobodyNear_IsRejectedAndKeepsItsCharge()
    {
        // Arrange
        var battle = StartBattle((Relic)All, 1, 1, OnTheRoad, new PlayerInput(60, PlayerActionType.LaserPointer, new GridPosition(8, 2)));

        // Act
        var events = StepUntil(battle, 60);

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle(r => r.Type == PlayerActionType.LaserPointer);
        battle.IsLaserReady.Should().BeTrue();
    }

    [Fact]
    public void Step_RelicNotFoundYet_IsRejected()
    {
        // Arrange — Hearth 1: the boot is still a mystery.
        var battle = StartBattle(Relic.None, 1, 1, OnTheRoad, new PlayerInput(60, PlayerActionType.DynamoTorch));

        // Act
        var events = StepUntil(battle, 60);

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle().Which.Reason.Should().Contain("car");
        battle.Leader.IsTorchLit(battle.Tick).Should().BeFalse();
    }

    [Fact]
    public void Step_TorchLitWhenTheLevyArrives_ItCannotFightHimAndCrawls()
    {
        // Arrange — the Levy reaches him around tick 120; the torch is lit at 100 for six seconds.
        var dark = StartBattle((Relic)All, 1, 1, OnTheRoad);
        var lit = StartBattle((Relic)All, 1, 1, OnTheRoad, new PlayerInput(100, PlayerActionType.DynamoTorch));

        // Act
        var darkEvents = StepUntil(dark, 200);
        var litEvents = StepUntil(lit, 200);

        // Assert
        darkEvents.OfType<LeaderHit>().Should().NotBeEmpty("without the torch the Levy fights him");
        litEvents.OfType<LeaderHit>().Should().BeEmpty("dazzled: it can't see to fight");
        litEvents.OfType<RelicUsed>().Should().ContainSingle(r => r.Type == PlayerActionType.DynamoTorch);
    }

    [Fact]
    public void Step_SunFireOnALevy_SetsItAlightAndBurns()
    {
        // Arrange
        var battle = StartBattle((Relic)All, 1, 1, new GridPosition(3, 1), new PlayerInput(45, PlayerActionType.SunFire, new GridPosition(0, 1)));

        // Act
        var events = StepUntil(battle, 45);
        var right = battle.Enemies.Single().Health;
        StepUntil(battle, 65);

        // Assert
        events.OfType<RelicUsed>().Should().ContainSingle().Which.EnemiesAffected.Should().Be(1);
        battle.Enemies.Single().IsBurning(battle.Tick).Should().BeTrue();
        battle.Enemies.Single().Health.Should().BeLessThan(right);
    }

    [Fact]
    public void Step_SunFireAfterTheFirstWave_RejectedTheSunHasSet()
    {
        // Arrange — budget 3 over two waves: the second wave starts before tick 400.
        var battle = StartBattle((Relic)All, 3, 2, new GridPosition(3, 1), new PlayerInput(400, PlayerActionType.SunFire, new GridPosition(1, 1)));

        // Act
        var events = StepUntil(battle, 400);

        // Assert
        battle.CurrentWave.Should().BeGreaterThan(1);
        battle.IsSunUp.Should().BeFalse();
        events.OfType<InputRejected>().Should().ContainSingle(r => r.Type == PlayerActionType.SunFire).Which.Reason.Should().Contain("sun");
    }

    [Fact]
    public void Simulate_SameRelicInputs_ProducesIdenticalHash()
    {
        // Arrange
        var inputs = new[]
        {
            new PlayerInput(45, PlayerActionType.SunFire, new GridPosition(0, 1)),
            new PlayerInput(70, PlayerActionType.LaserPointer, new GridPosition(1, 1)),
            new PlayerInput(100, PlayerActionType.DynamoTorch),
        };

        // Act
        var first = StartBattle((Relic)All, 3, 2, OnTheRoad, inputs).RunToCompletion();
        var second = StartBattle((Relic)All, 3, 2, OnTheRoad, inputs).RunToCompletion();

        // Assert
        second.StateHash.Should().Be(first.StateHash);
    }

    [Fact]
    public void Simulate_NoRelics_HashUnchangedByTheRelicCode()
    {
        // Arrange — the relic-free path must hash exactly as before (old Nights replay on the server).
        var layout = TestLayouts.Fortified();

        // Act
        var result = Battle.Simulate(new BattleSetup(layout, Seed: 7, Budget: 12, WaveCount: 3, Inputs: []));
        var withEmptyRelics = Battle.Simulate(new BattleSetup(layout, Seed: 7, Budget: 12, WaveCount: 3, Inputs: []) { Relics = Relic.None });

        // Assert
        withEmptyRelics.StateHash.Should().Be(result.StateHash);
    }
}

public class BishopTests
{
    [Fact]
    public void Roster_HearthThree_BringsTheChurchWithABishop()
    {
        // Act
        var roster = CultOfComfort.Simulation.Waves.RaidRoster.ForHearth(3)!;

        // Assert
        roster.Mix.Should().Contain(m => m.Type == EnemyType.Bishop);
    }

    [Fact]
    public void Stats_Bishop_IsSlowAndTough()
    {
        // Act
        var bishop = BalanceTable.Default.Enemies[EnemyType.Bishop];
        var levy = BalanceTable.Default.Enemies[EnemyType.Levy];

        // Assert
        bishop.TilesPerSecond.Should().BeLessThan(levy.TilesPerSecond);
        bishop.MaxHealth.Should().BeGreaterThan(BalanceTable.Default.Enemies[EnemyType.Priest].MaxHealth);
        BalanceTable.Default.BishopBlessingRadius.Should().BeGreaterThan(BalanceTable.Default.BlessingRadius);
    }
}
