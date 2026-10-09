using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class BattleTests
{
    // A Hearth that cannot fall keeps intervention tests independent of combat balance.
    private static readonly BalanceTable Indestructible = new() { HearthMaxHealth = 100_000 };

    private static BattleSetup Setup(BattleLayout layout, ulong seed = 42, int budget = 12, params PlayerInput[] inputs) =>
        new(layout, seed, budget, WaveCount: 3, inputs);

    [Fact]
    public void Simulate_SameSetupTwice_ProducesIdenticalResult()
    {
        // Arrange
        var setup = Setup(TestLayouts.Fortified(), inputs: new PlayerInput(200, InterventionType.OpenSluiceGate));

        // Act
        var first = Battle.Simulate(setup);
        var second = Battle.Simulate(setup);

        // Assert
        second.Should().Be(first);
    }

    [Fact]
    public void Simulate_DifferentSeed_ProducesDifferentStateHash()
    {
        // Act
        var first = Battle.Simulate(Setup(TestLayouts.Fortified(), seed: 1));
        var second = Battle.Simulate(Setup(TestLayouts.Fortified(), seed: 2));

        // Assert
        second.StateHash.Should().NotBe(first.StateHash);
    }

    [Fact]
    public void Simulate_FortifiedCorridor_WinsWithThreeStars()
    {
        // Act
        var result = Battle.Simulate(Setup(TestLayouts.Fortified(), budget: 8));

        // Assert
        result.Outcome.Should().Be(BattleOutcome.Won);
        result.Stars.Should().Be(3);
        result.EnemiesLeaked.Should().Be(0);
    }

    [Fact]
    public void Simulate_NoTowers_LosesWithZeroStars()
    {
        // Act
        var result = Battle.Simulate(Setup(TestLayouts.Corridor(), budget: 30));

        // Assert
        result.Outcome.Should().Be(BattleOutcome.Lost);
        result.Stars.Should().Be(0);
        result.HearthHealth.Should().Be(0);
    }

    [Fact]
    public void Simulate_ReplayOfLiveInputLog_MatchesLiveStateHash()
    {
        // Arrange — "client" plays live, intervening whenever an enemy passes the halfway mark.
        var layout = TestLayouts.Corridor(new PlacedTower(TowerType.SaplingFlinger, new GridPosition(4, 0)));
        var live = new Battle(Setup(layout, budget: 20) with { Balance = Indestructible });
        while (!live.IsFinished)
        {
            if (live.IsSluiceReady && live.Enemies.Any(e => e.IsActive && e.DistanceAlongPath > live.PathLength / 2))
            {
                live.Intervene(InterventionType.OpenSluiceGate);
            }

            live.Step();
        }

        // Act — "server" replays from the submitted input log only.
        var replayed = Battle.Simulate(Setup(layout, budget: 20, inputs: live.InputLog.ToArray()) with { Balance = Indestructible });

        // Assert
        live.InputLog.Should().NotBeEmpty();
        replayed.Should().Be(live.Result);
    }

    [Fact]
    public void Simulate_TamperedInputLog_ProducesDifferentStateHash()
    {
        // Arrange
        var honest = Battle.Simulate(Setup(TestLayouts.Fortified(), inputs: new PlayerInput(200, InterventionType.OpenSluiceGate)));

        // Act
        var tampered = Battle.Simulate(Setup(TestLayouts.Fortified(), inputs: new PlayerInput(201, InterventionType.OpenSluiceGate)));

        // Assert
        tampered.StateHash.Should().NotBe(honest.StateHash);
    }

    [Fact]
    public void Step_SluiceGateOpened_PushesMarchingEnemiesBack()
    {
        // Arrange
        var battle = new Battle(Setup(TestLayouts.Corridor()) with { Balance = Indestructible });
        while (battle.Enemies.Count == 0 || battle.Enemies[0].DistanceAlongPath < Fix.FromInt(5))
        {
            battle.Step();
        }

        var before = battle.Enemies[0].DistanceAlongPath;

        // Act
        battle.Intervene(InterventionType.OpenSluiceGate);
        battle.Step();

        // Assert
        battle.Enemies[0].DistanceAlongPath.Should().BeLessThan(before);
        battle.Enemies[0].IsMuddied(battle.Tick).Should().BeTrue();
        battle.LastTickEvents.Should().ContainSingle(e => e is InterventionUsed);
    }

    [Fact]
    public void Step_SluiceGateOnCooldown_RejectsSecondInput()
    {
        // Arrange
        var battle = new Battle(Setup(
            TestLayouts.Corridor(),
            inputs: [new PlayerInput(10, InterventionType.OpenSluiceGate), new PlayerInput(11, InterventionType.OpenSluiceGate)])
            with { Balance = Indestructible });

        // Act
        var events = new List<BattleEvent>();
        for (var i = 0; i < 12; i++)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle().Which.Tick.Should().Be(11);
        battle.SluiceChargesLeft.Should().Be(1);
    }

    [Fact]
    public void Step_SluiceGateWithoutCharges_RejectsInput()
    {
        // Arrange
        var battle = new Battle(Setup(
            TestLayouts.Corridor(),
            inputs: [new PlayerInput(0, InterventionType.OpenSluiceGate), new PlayerInput(250, InterventionType.OpenSluiceGate), new PlayerInput(500, InterventionType.OpenSluiceGate)])
            with { Balance = Indestructible });

        // Act
        var events = new List<BattleEvent>();
        while (battle.Tick <= 500)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        // Assert
        events.OfType<InputRejected>().Should().ContainSingle().Which.Reason.Should().Contain("empty");
        battle.SluiceChargesLeft.Should().Be(0);
    }

    [Fact]
    public void Constructor_LayoutWithoutPath_ThrowsInvalidLayoutException()
    {
        // Arrange
        var wall = Enumerable.Range(0, 3).Select(y => new GridPosition(5, y));
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], wall);

        // Act
        Action act = () => _ = new Battle(Setup(layout));

        // Assert
        act.Should().Throw<InvalidLayoutException>();
    }

    [Fact]
    public void Constructor_NegativeInputTick_ThrowsArgumentException()
    {
        // Act
        Action act = () => _ = new Battle(Setup(TestLayouts.Corridor(), inputs: new PlayerInput(-1, InterventionType.OpenSluiceGate)));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Step_AfterBattleFinished_ThrowsInvalidOperationException()
    {
        // Arrange
        var battle = new Battle(Setup(TestLayouts.Fortified(), budget: 3));
        battle.RunToCompletion();

        // Act
        Action act = () => battle.Step();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
