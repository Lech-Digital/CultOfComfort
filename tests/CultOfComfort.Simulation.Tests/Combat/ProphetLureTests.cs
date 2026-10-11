using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class ProphetLureTests
{
    /// <summary>Budget 1, one wave: a single Levy spawns at (0,1) on tick 40 and walks right at 1 tile/s along y = 1.</summary>
    private const int SingleLevy = 1;

    private static readonly GridPosition BesideTheRoad = new(4, 2);

    /// <summary>He never hits back, so lured raiders survive and stay interesting.</summary>
    private static BalanceTable Balance(Func<LeaderStats, LeaderStats>? leader = null, Func<EnemyStats, EnemyStats>? levy = null)
    {
        var enemies = new Dictionary<EnemyType, EnemyStats>(BalanceTable.Default.Enemies);
        enemies[EnemyType.Levy] = (levy ?? (s => s))(enemies[EnemyType.Levy]);
        return new BalanceTable
        {
            HearthMaxHealth = 100_000,
            Leader = (leader ?? (l => l with { AttackDamage = Fix.Zero }))(BalanceTable.Default.Leader with { AttackDamage = Fix.Zero }),
            Enemies = enemies,
        };
    }

    private static Battle StartBattle(BalanceTable balance, int budget, GridPosition leaderStart, int height = 3, params PlacedTower[] towers)
    {
        var layout = new BattleLayout(10, height, new GridPosition(0, 1), new GridPosition(9, 1), towers, [], leaderStart);
        return new Battle(new BattleSetup(layout, Seed: 42, budget, WaveCount: 1, [], balance));
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
    public void Defaults_Balance_MeleeRaidersNoticeTheProphetAndZealotsDoNot()
    {
        // Act
        var enemies = BalanceTable.Default.Enemies;

        // Assert
        BalanceTable.Default.Version.Should().Be("2026.10.3");
        enemies[EnemyType.Levy].NoticeRange.Should().Be(Fix.FromInt(2));
        enemies[EnemyType.ManAtArms].NoticeRange.Should().Be(Fix.Ratio(3, 2));
        enemies[EnemyType.Zealot].NoticeRange.Should().Be(Fix.Zero);
        BalanceTable.Default.Leader.LureLeash.Should().Be(Fix.Ratio(5, 2));
    }

    [Fact]
    public void Step_ProphetBesideTheRoad_LevyStepsOffTheRoadAndFights()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);

        // Act
        var events = StepUntil(battle, 300);

        // Assert
        var levy = battle.Enemies.Single();
        levy.IsOffRoad.Should().BeTrue();
        levy.IsFightingLeader.Should().BeTrue();
        battle.PositionOf(levy).Y.Should().BeGreaterThan(Fix.Ratio(13, 10), "it left the road (y = 1) towards him (y = 2)");
        events.OfType<LeaderHit>().Should().NotBeEmpty();
    }

    [Fact]
    public void Step_ProphetBeyondNoticeRange_LevyMarchesPastOnTheRoad()
    {
        // Arrange — three tiles off the road is beyond the Levy's notice range of 2 (corridor 10x5).
        var battle = StartBattle(Balance(), SingleLevy, new GridPosition(4, 4), height: 5);

        // Act
        var events = StepUntil(battle, 400);

        // Assert
        battle.Enemies.Single().IsOffRoad.Should().BeFalse();
        events.OfType<LeaderHit>().Should().BeEmpty();
    }

    [Fact]
    public void Step_ProphetWalksAwayBeyondTheLeash_LevyReturnsToTheRoadAndMarchesOn()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var roadDistance = levy.DistanceAlongPath;

        // Act — he walks to the far corner, more than 2½ tiles from the Levy's road point.
        battle.Intervene(PlayerActionType.MoveLeader, new GridPosition(0, 0));
        StepUntil(battle, 420);

        // Assert
        levy.IsOffRoad.Should().BeFalse();
        levy.IsFightingLeader.Should().BeFalse();
        levy.DistanceAlongPath.Should().BeGreaterThan(roadDistance, "back on the road, it marches on");
    }

    [Fact]
    public void Step_ProphetSwoons_LuredLevyWalksBackToTheRoad()
    {
        // Arrange — 5 HP: the second hit knocks him out for 100 ticks.
        var battle = StartBattle(Balance(l => l with { AttackDamage = Fix.Zero, MaxHealth = 5, SwoonTicks = 100 }), SingleLevy, BesideTheRoad);
        var swoon = StepUntil(battle, 400).OfType<LeaderSwooned>().First();

        // Act
        StepUntil(battle, swoon.Tick + 40);

        // Assert
        battle.Enemies.Single().IsOffRoad.Should().BeFalse("nobody fights a man lying in the mud");
    }

    [Fact]
    public void Step_RaiderWithoutNoticeRange_FightsFromTheRoad()
    {
        // Arrange — a Levy made to behave like a Zealot: no notice range, throws from 2½ tiles.
        var balance = Balance(levy: s => s with { NoticeRange = Fix.Zero, EngageRange = Fix.Ratio(5, 2) });
        var battle = StartBattle(balance, SingleLevy, BesideTheRoad);

        // Act
        var events = StepUntil(battle, 300);

        // Assert
        var levy = battle.Enemies.Single();
        levy.IsOffRoad.Should().BeFalse();
        levy.IsFightingLeader.Should().BeTrue();
        events.OfType<LeaderHit>().Should().NotBeEmpty();
    }

    [Fact]
    public void Step_EngagedCapReached_ExtraRaiderStaysOnTheRoad()
    {
        // Arrange — cap of one, a budget for several Levies.
        var battle = StartBattle(Balance(l => l with { AttackDamage = Fix.Zero, MaxEngaged = 1 }), budget: 4, BesideTheRoad);
        var maxOffRoadAndHunting = 0;

        // Act
        while (!battle.IsFinished && battle.Tick < 500)
        {
            battle.Step();
            var hunting = battle.Enemies.Count(e => e.IsActive && (e.IsFightingLeader || (e.IsOffRoad && e.Offset != default && IsCloserToHimThanItsRoadPoint(battle, e))));
            maxOffRoadAndHunting = Math.Max(maxOffRoadAndHunting, hunting);
        }

        // Assert
        maxOffRoadAndHunting.Should().Be(1);
        battle.Enemies.Count.Should().BeGreaterThan(1, "the test needs more than one raider");
    }

    private static bool IsCloserToHimThanItsRoadPoint(Battle battle, Enemy enemy) =>
        battle.PositionOf(enemy).DistanceSquaredTo(battle.Leader.Position) < battle.RoadPointOf(enemy).DistanceSquaredTo(battle.Leader.Position);

    [Fact]
    public void Step_TowerOutOfReachOfTheRoad_HitsALuredRaider()
    {
        // Arrange — 10x7 field, road along y = 1; a Flinger at (3,5) is 4 tiles from the road (range 3½).
        // He stands at (3,3): the Levy comes up to about (3,2.5), inside the Flinger's range.
        var flinger = new PlacedTower(TowerType.SaplingFlinger, new GridPosition(3, 5));
        var lured = StartBattle(Balance(), SingleLevy, new GridPosition(3, 3), height: 7, flinger);
        var control = StartBattle(Balance(), SingleLevy, new GridPosition(0, 6), height: 7, flinger);

        // Act
        var luredEvents = StepUntil(lured, 400);
        var controlEvents = StepUntil(control, 400);

        // Assert
        luredEvents.OfType<TowerFired>().Should().NotBeEmpty("pulled off the road, it walked into range");
        controlEvents.OfType<TowerFired>().Should().BeEmpty("on the road it never comes within range");
    }

    [Fact]
    public void Step_LuredRaiderFrightened_WalksBackToTheRoadBeforeFleeing()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var roadDistance = levy.DistanceAlongPath;

        // Act
        battle.Intervene(PlayerActionType.PhantomVoice);
        battle.Step();
        while (levy.IsOffRoad && battle.Tick < 400)
        {
            levy.DistanceAlongPath.Should().Be(roadDistance, "it first scrambles back onto the road");
            battle.Step();
        }

        StepUntil(battle, battle.Tick + 10);

        // Assert
        levy.IsOffRoad.Should().BeFalse();
        levy.DistanceAlongPath.Should().BeLessThan(roadDistance, "then it flees back up the road");
    }

    [Fact]
    public void Step_LuredRaiderHitBySluice_KeepsItsOffsetAndIsWashedBack()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var offset = levy.Offset;
        var roadDistance = levy.DistanceAlongPath;

        // Act
        battle.Intervene(PlayerActionType.OpenSluiceGate);
        battle.Step();

        // Assert
        levy.DistanceAlongPath.Should().BeLessThan(roadDistance);
        levy.Offset.Should().NotBe(default(FixVector2), "washed along, still off the road");
        (levy.Offset.Y - offset.Y).Abs().Should().BeLessThan(Fix.Ratio(1, 10));
    }

    [Fact]
    public void Step_OffRoadRaider_NeverAdvancesAlongTheRoad()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);

        // Act & Assert
        var last = Fix.Zero;
        while (!battle.IsFinished && battle.Tick < 600)
        {
            battle.Step();
            var levy = battle.Enemies.SingleOrDefault();
            if (levy is { IsActive: true })
            {
                if (levy.IsOffRoad)
                {
                    levy.DistanceAlongPath.Should().BeLessThanOrEqualTo(last);
                }

                last = levy.DistanceAlongPath;
            }
        }
    }

    [Fact]
    public void Step_NewcomerNoticesHimWhileTheCapIsFull_FightersInReachKeepTheirSlots()
    {
        // Arrange — a long road past a Prophet parked near its end: Zealots pin him from range while Levies and
        // men-at-arms arrive later and notice him. A fighter still in reach must never be bumped by a newcomer.
        var layout = new BattleLayout(12, 5, new GridPosition(0, 2), new GridPosition(11, 2), [], [], new GridPosition(10, 0));
        var balance = Balance();
        var bumped = 0;
        PlayerInput[] inputs = [new(500, PlayerActionType.OpenSluiceGate), new(900, PlayerActionType.OpenSluiceGate)];

        // Act
        for (var seed = 1UL; seed <= 40; seed++)
        {
            var battle = new Battle(new BattleSetup(layout, seed, Budget: 40, WaveCount: 3, inputs, balance));
            var fighting = new HashSet<int>();
            while (!battle.IsFinished && battle.Tick < 4000)
            {
                battle.Step();
                foreach (var enemy in battle.Enemies)
                {
                    var stats = balance.Enemies[enemy.Type];
                    var inReach = battle.PositionOf(enemy).DistanceSquaredTo(battle.Leader.Position) <= stats.EngageRange * stats.EngageRange;
                    var targetable = !battle.Leader.IsIgnoredByEnemies(battle.Tick - 1, balance.Leader.ReviveGraceTicks);
                    if (fighting.Contains(enemy.Id) && !enemy.IsFightingLeader && enemy.IsActive && inReach && targetable && !enemy.IsFrightened(battle.Tick - 1))
                    {
                        bumped++;
                    }
                }

                fighting = battle.Enemies.Where(e => e.IsFightingLeader).Select(e => e.Id).ToHashSet();
            }
        }

        // Assert
        bumped.Should().Be(0);
    }

    [Fact]
    public void Simulate_LuredBattleReplay_MatchesLiveStateHash()
    {
        // Arrange
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], BesideTheRoad);
        var inputs = new[] { new PlayerInput(250, PlayerActionType.MoveLeader, new GridPosition(0, 0)), new PlayerInput(300, PlayerActionType.PhantomVoice) };
        var setup = new BattleSetup(layout, Seed: 42, Budget: 6, WaveCount: 2, inputs, Balance());
        var live = new Battle(setup);
        while (!live.IsFinished)
        {
            live.Step();
        }

        // Act
        var replay = Battle.Simulate(setup);

        // Assert
        replay.Should().Be(live.Result);
    }
}
