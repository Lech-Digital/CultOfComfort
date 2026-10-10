using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class ProphetThreatTests
{
    /// <summary>Budget 1, one wave: a single Levy spawns at (0,1) on tick 40 and walks right at 1 tile/s.</summary>
    private const int SingleLevy = 1;

    private static readonly GridPosition OnTheRoad = new(3, 1);

    private static BalanceTable Balance(Func<LeaderStats, LeaderStats>? leader = null) => new()
    {
        HearthMaxHealth = 100_000,
        Leader = (leader ?? (l => l))(BalanceTable.Default.Leader),
    };

    private static Battle StartBattle(BalanceTable balance, int budget, GridPosition? leaderStart, params PlayerInput[] inputs) =>
        StartBattle(balance, budget, waveCount: 1, leaderStart, inputs);

    private static Battle StartBattle(BalanceTable balance, int budget, int waveCount, GridPosition? leaderStart, params PlayerInput[] inputs)
    {
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], leaderStart);
        return new Battle(new BattleSetup(layout, Seed: 42, budget, waveCount, inputs, balance));
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

    [Fact]
    public void Step_LevyMeetsProphetOnTheRoad_StopsAndFightsInsteadOfMarching()
    {
        // Arrange — he never hits back, so the Levy stays engaged.
        var battle = StartBattle(Balance(l => l with { AttackDamage = Fix.Zero }), SingleLevy, OnTheRoad);

        // Act
        var events = StepUntil(battle, 300);

        // Assert
        var levy = battle.Enemies.Single();
        levy.IsFightingLeader.Should().BeTrue();
        battle.PositionOf(levy).X.Should().BeLessThan(Fix.FromInt(3), "it stops in reach, short of him");
        events.OfType<LeaderHit>().Should().NotBeEmpty().And.OnlyContain(hit => hit.Damage == 3);
        battle.Leader.Health.Should().BeLessThan(battle.Leader.MaxHealth);
        events.OfType<HearthDamaged>().Should().BeEmpty();
    }

    [Fact]
    public void Step_ProphetHealthRunsOut_SwoonsLetsEnemiesPassAndRevivesAtHalfHealth()
    {
        // Arrange — 5 HP: the second Levy hit (3 + 3) knocks him out for 100 ticks.
        var balance = Balance(l => l with { MaxHealth = 5, AttackDamage = Fix.Zero, SwoonTicks = 100 });
        var battle = StartBattle(balance, SingleLevy, OnTheRoad);
        var events = StepUntil(battle, 120);
        var swoon = events.OfType<LeaderSwooned>().Single();

        // Act
        var swoonEvents = StepUntil(battle, swoon.Tick + 99);
        var afterEvents = StepUntil(battle, swoon.Tick + 100);

        // Assert
        swoonEvents.OfType<LeaderHit>().Should().BeEmpty("enemies ignore a man lying in the mud");
        battle.Enemies.Single().IsFightingLeader.Should().BeFalse();
        afterEvents.OfType<LeaderRevived>().Should().ContainSingle().Which.Health.Should().Be(2);
    }

    [Fact]
    public void Step_MoveOrderedWhileSwooning_StaysDownThenWalksOnRevival()
    {
        // Arrange — knocked out at 5 HP, ordered to walk away while lying in the mud.
        var balance = Balance(l => l with { MaxHealth = 5, AttackDamage = Fix.Zero, SwoonTicks = 100 });
        var battle = StartBattle(balance, SingleLevy, OnTheRoad);
        var swoon = StepUntil(battle, 120).OfType<LeaderSwooned>().Single();
        var lyingAt = battle.Leader.Position;

        // Act
        battle.Intervene(PlayerActionType.MoveLeader, new GridPosition(6, 0));
        var swoonEvents = StepUntil(battle, swoon.Tick + 99);
        var positionWhileDown = battle.Leader.Position;
        StepUntil(battle, swoon.Tick + 160);

        // Assert
        swoonEvents.OfType<LeaderMoveOrdered>().Should().ContainSingle();
        positionWhileDown.Should().Be(lyingAt);
        battle.Leader.Position.Should().Be(new GridPosition(6, 0).ToVector());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(40, false)]
    public void Step_JustRevivedBesideAnEnemy_IsOnlyHitWithoutGrace(int graceTicks, bool expectHit)
    {
        // Arrange — a one-tick faint: he gets up with the Levy still in reach, and it has ~1.5 s left in reach after that.
        var balance = Balance(l => l with { MaxHealth = 5, AttackDamage = Fix.Zero, SwoonTicks = 1, ReviveGraceTicks = graceTicks });
        var battle = StartBattle(balance, SingleLevy, OnTheRoad);
        while (!battle.LastTickEvents.OfType<LeaderSwooned>().Any())
        {
            battle.Step();
        }

        // Act
        var afterEvents = StepUntil(battle, battle.Tick + 60);

        // Assert
        afterEvents.OfType<LeaderRevived>().Should().NotBeEmpty();
        afterEvents.OfType<LeaderHit>().Any().Should().Be(expectHit, "without grace he is hit (and floored) again straight away");
    }

    [Fact]
    public void Step_MoreEnemiesThanHeCanHold_ExtraOnesWalkPast()
    {
        // Arrange — he can only hold one; budget 2 sends two Levies.
        var balance = Balance(l => l with { MaxEngaged = 1, MaxHealth = 10_000, AttackDamage = Fix.Zero });
        var battle = StartBattle(balance, budget: 2, OnTheRoad);

        // Act
        var events = StepUntil(battle, 500);

        // Assert
        battle.Enemies.Count(e => e.IsFightingLeader).Should().Be(1);
        events.OfType<HearthDamaged>().Should().ContainSingle("the second Levy walked past him to the Hearth");
    }

    [Fact]
    public void Step_HotStonesOnEnemyInReach_DamagesAndMuddies()
    {
        // Arrange — the Levy is at the spawn (0,1), three tiles from him.
        var battle = StartBattle(Balance(), SingleLevy, OnTheRoad,
            new PlayerInput(41, PlayerActionType.HotStones, new GridPosition(0, 1)));

        // Act
        var events = StepUntil(battle, 41);

        // Assert
        events.OfType<HotStonesThrown>().Should().ContainSingle().Which.EnemiesAffected.Should().Be(1);
        var levy = battle.Enemies.Single();
        levy.Health.Should().Be(Fix.FromInt(15));
        levy.IsMuddied(battle.Tick).Should().BeTrue();
        battle.IsHotStonesReady.Should().BeFalse();
    }

    [Fact]
    public void Step_HotStonesBeyondReach_IsRejectedAndKeepsCooldown()
    {
        // Arrange — target is seven tiles away; his reach is four.
        var battle = StartBattle(Balance(), SingleLevy, new GridPosition(7, 1),
            new PlayerInput(41, PlayerActionType.HotStones, new GridPosition(0, 1)));

        // Act
        var events = StepUntil(battle, 41);

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle(r => r.Type == PlayerActionType.HotStones);
        events.OfType<HotStonesThrown>().Should().BeEmpty();
        battle.IsHotStonesReady.Should().BeTrue();
    }

    [Fact]
    public void Step_HotStonesTwiceInARow_SecondIsRejectedWhileHeating()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, OnTheRoad,
            new PlayerInput(41, PlayerActionType.HotStones, new GridPosition(1, 1)),
            new PlayerInput(42, PlayerActionType.HotStones, new GridPosition(1, 1)));

        // Act
        var events = StepUntil(battle, 42);

        // Assert
        events.OfType<HotStonesThrown>().Should().ContainSingle();
        events.OfType<InputRejected>().Should().ContainSingle(r => r.Reason.Contains("heating"));
    }

    [Fact]
    public void Step_NotHitForAWhile_RegainsHealth()
    {
        // Arrange — budget 3 over two waves: one Levy, then two more eight seconds later. He kills the first himself.
        var battle = StartBattle(Balance(), budget: 3, waveCount: 2, OnTheRoad);
        var events = StepUntil(battle, 180);
        var lowest = events.OfType<LeaderHit>().Min(hit => hit.HealthRemaining);

        // Act — the second wave is still on its way.
        StepUntil(battle, 240);

        // Assert
        battle.Enemies[0].State.Should().Be(EnemyState.Defeated);
        battle.IsFinished.Should().BeFalse();
        battle.Leader.Health.Should().BeGreaterThan(lowest);
    }

    [Fact]
    public void Simulate_SameInputsWithSkillsAndSwoons_ProducesIdenticalHash()
    {
        // Arrange
        var layout = TestLayouts.Fortified();
        var inputs = new List<PlayerInput>
        {
            new(10, PlayerActionType.MoveLeader, new GridPosition(3, 1)),
            new(300, PlayerActionType.HotStones, new GridPosition(2, 1)),
            new(500, PlayerActionType.PhantomVoice),
            new(900, PlayerActionType.HotStones, new GridPosition(1, 1)),
        };
        var setup = new BattleSetup(layout, Seed: 7, Budget: 40, WaveCount: 4, inputs);

        // Act
        var first = Battle.Simulate(setup);
        var second = Battle.Simulate(setup);

        // Assert
        second.Should().Be(first);
    }
}
