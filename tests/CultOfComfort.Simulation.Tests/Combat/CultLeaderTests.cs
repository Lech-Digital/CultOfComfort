using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class CultLeaderTests
{
    private static readonly BalanceTable Indestructible = new() { HearthMaxHealth = 100_000 };

    private static Battle StartBattle(BattleLayout layout, params PlayerInput[] inputs) =>
        new(new BattleSetup(layout, Seed: 42, Budget: 12, WaveCount: 3, inputs, Indestructible));

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
    public void Constructor_NoLeaderStartGiven_PlacesLeaderBesideTheHearth()
    {
        // Arrange
        var layout = TestLayouts.Corridor();

        // Act
        var battle = StartBattle(layout);

        // Assert
        battle.Leader.Position.Should().Be(new GridPosition(8, 1).ToVector());
    }

    [Fact]
    public void Step_MoveLeaderOrder_WalksLeaderToTargetTile()
    {
        // Arrange — 5 tiles from (8,1) at 3 tiles/s needs ~34 ticks.
        var battle = StartBattle(TestLayouts.Corridor(), new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(4, 0)));

        // Act
        var events = StepUntil(battle, 40);

        // Assert
        events.OfType<LeaderMoveOrdered>().Should().ContainSingle();
        battle.Leader.Position.Should().Be(new GridPosition(4, 0).ToVector());
        battle.Leader.IsMoving.Should().BeFalse();
    }

    [Fact]
    public void Step_MoveLeaderAroundTower_NeverEntersBlockedTile()
    {
        // Arrange
        var tower = new GridPosition(5, 0);
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SaplingFlinger, tower));
        var battle = StartBattle(layout, new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(3, 0)));
        var visited = new List<FixVector2>();

        // Act
        while (battle.Tick <= 60)
        {
            battle.Step();
            visited.Add(battle.Leader.Position);
        }

        // Assert
        visited.Should().NotContain(tower.ToVector());
        battle.Leader.Position.Should().Be(new GridPosition(3, 0).ToVector());
    }

    [Theory]
    [InlineData(9, 1)]   // the Hearth itself
    [InlineData(5, 0)]   // a tower
    [InlineData(30, 1)]  // off the map
    public void Step_MoveLeaderToUnwalkableTile_RejectsOrder(int x, int y)
    {
        // Arrange
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(5, 0)));
        var battle = StartBattle(layout, new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(x, y)));

        // Act
        var events = StepUntil(battle, 0);

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle().Which.Type.Should().Be(PlayerActionType.MoveLeader);
        battle.Leader.IsMoving.Should().BeFalse();
    }

    [Fact]
    public void Step_EnemyWithinReach_LeaderAttacksIt()
    {
        // Arrange — leader waits next to the road; first levy walks past him.
        var battle = StartBattle(TestLayouts.Corridor(), new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(3, 0)));

        // Act
        var events = StepUntil(battle, 200);

        // Assert
        events.OfType<LeaderAttacked>().Should().NotBeEmpty();
    }

    [Fact]
    public void Step_PhantomVoiceNearEnemies_FrightenedEnemiesWalkBackwards()
    {
        // Arrange — leader at (3,0); voice once the first enemy is right next to him.
        var battle = StartBattle(TestLayouts.Corridor(), new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(3, 0)));
        while (battle.Enemies.Count == 0 || battle.Enemies[0].DistanceAlongPath < Fix.FromInt(2))
        {
            battle.Step();
        }

        battle.Intervene(PlayerActionType.PhantomVoice);
        battle.Step();
        var afterShout = battle.Enemies[0].DistanceAlongPath;

        // Act
        StepUntil(battle, battle.Tick + 10);

        // Assert
        battle.LastTickEvents.Should().NotContain(e => e is InputRejected);
        battle.Enemies[0].IsFrightened(battle.Tick - 1).Should().BeTrue();
        battle.Enemies[0].DistanceAlongPath.Should().BeLessThan(afterShout);
    }

    [Fact]
    public void Step_PhantomVoiceOnCooldown_RejectsSecondShout()
    {
        // Arrange
        var battle = StartBattle(
            TestLayouts.Corridor(),
            new PlayerInput(5, PlayerActionType.PhantomVoice),
            new PlayerInput(50, PlayerActionType.PhantomVoice));

        // Act
        var events = StepUntil(battle, 50);

        // Assert
        events.OfType<InterventionUsed>().Should().ContainSingle();
        events.OfType<InputRejected>().Should().ContainSingle().Which.Reason.Should().Contain("hoarse");
    }

    [Fact]
    public void Simulate_ReplayWithLeaderCommands_MatchesLiveStateHash()
    {
        // Arrange — live play: chase the leading enemy around and shout when it gets close.
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(4, 0)));
        var live = StartBattle(layout);
        while (!live.IsFinished)
        {
            if (live.Tick % 40 == 0)
            {
                live.Intervene(PlayerActionType.MoveLeader, new GridPosition(2 + live.Tick / 40 % 6, 2));
            }

            if (live.IsVoiceReady && live.Enemies.Any(e => e.IsActive && live.PositionOf(e).DistanceSquaredTo(live.Leader.Position) < Fix.FromInt(2)))
            {
                live.Intervene(PlayerActionType.PhantomVoice);
            }

            live.Step();
        }

        // Act
        var replayed = Battle.Simulate(new BattleSetup(layout, 42, 12, 3, live.InputLog.ToList(), Indestructible));

        // Assert
        live.InputLog.Should().Contain(i => i.Type == PlayerActionType.PhantomVoice);
        replayed.Should().Be(live.Result);
    }

    [Fact]
    public void Validate_LeaderStartsOnObstacle_ThrowsInvalidLayoutException()
    {
        // Arrange
        var rock = new GridPosition(3, 0);
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [rock], leaderStart: rock);

        // Act
        Action act = () => layout.Validate();

        // Assert
        act.Should().Throw<InvalidLayoutException>().WithMessage("*leader*");
    }
}
