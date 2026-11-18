using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Combat;

/// <summary>Rules 1 (spec 2026-11-17 fun first, C): waves with gaps, boons, the early call, earned stars, the first Night's one wreck.</summary>
public class FunFirstRulesTests
{
    private static BattleSetup Setup(int budget = 6, int waves = 3, NightObjective objective = NightObjective.ProphetNeverSwoons,
        int maxWrecks = 0, IReadOnlyList<PlayerInput>? inputs = null, BattleLayout? layout = null, BalanceTable? balance = null) =>
        new(layout ?? TestLayouts.Fortified(), 42, budget, waves, inputs ?? [], balance)
        {
            Rules = Battle.FunFirstRules,
            Objective = objective,
            MaxWrecks = maxWrecks,
        };

    private static List<BattleEvent> RunToEnd(Battle battle, Func<Battle, BattleEvent, bool>? act = null)
    {
        var events = new List<BattleEvent>();
        while (!battle.IsFinished)
        {
            battle.Step();
            foreach (var e in battle.LastTickEvents)
            {
                events.Add(e);
                if (act != null && act(battle, e))
                {
                    act = null;
                }
            }
        }

        return events;
    }

    [Fact]
    public void Step_Rules1_NextWaveWaitsForTheLastToBeClearedThenAGap()
    {
        // Arrange
        var battle = new Battle(Setup());

        // Act
        var events = RunToEnd(battle);

        // Assert
        var gap = events.OfType<WaveGap>().First();
        var wave2 = events.OfType<WaveStarted>().Single(w => w.Wave == 2);
        gap.NextWave.Should().Be(2);
        wave2.Tick.Should().Be(gap.Tick + Battle.GapTicks, "the wave comes when the gap is over");
        events.OfType<EnemySpawned>().Where(s => s.Wave == 2).Should().OnlyContain(s => s.Tick >= wave2.Tick);
        events.OfType<WaveGap>().Should().HaveCount(2, "one before each later wave, none after the last");
    }

    [Fact]
    public void Intervene_CallWaveInTheGap_TheWaveComesNowAndCounts()
    {
        // Arrange
        var battle = new Battle(Setup());

        // Act
        var events = RunToEnd(battle, (b, e) =>
        {
            if (e is WaveGap)
            {
                b.Intervene(PlayerActionType.CallWave);
                return true;
            }

            return false;
        });

        // Assert
        var gap = events.OfType<WaveGap>().First();
        events.OfType<WaveCalled>().Should().ContainSingle().Which.Wave.Should().Be(2);
        events.OfType<WaveStarted>().Single(w => w.Wave == 2).Tick.Should().BeLessThan(gap.Tick + Battle.GapTicks);
        battle.Result!.EarlyCalls.Should().Be(1);
    }

    [Fact]
    public void Intervene_CallWaveDuringAWave_IsRejected()
    {
        // Arrange
        var battle = new Battle(Setup());
        while (battle.CurrentWave < 1)
        {
            battle.Step();
        }

        // Act
        battle.Intervene(PlayerActionType.CallWave);
        battle.Step();

        // Assert
        battle.LastTickEvents.OfType<InputRejected>().Should().ContainSingle(r => r.Type == PlayerActionType.CallWave);
        battle.EarlyCalls.Should().Be(0);
    }

    [Fact]
    public void Intervene_PickBoon_HelpsTheNextWaveOnly()
    {
        // Arrange
        var battle = new Battle(Setup());
        Boon? offered = null;

        // Act
        var events = RunToEnd(battle, (b, e) =>
        {
            if (e is WaveGap gap)
            {
                offered = gap.Offer[1];
                b.Intervene(PlayerActionType.PickBoon, new GridPosition(1, 0));
                return true;
            }

            return false;
        });

        // Assert
        events.OfType<BoonPicked>().Should().ContainSingle().Which.Boon.Should().Be(offered!.Value);
        events.OfType<WaveGap>().First().Offer.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        battle.ActiveBoon.Should().BeNull("a boon lasts its wave; the last wave had none");
    }

    [Fact]
    public void BoonsFor_SameSeedAndWave_SameThreeEveryTime()
    {
        // Act & Assert
        Battle.BoonsFor(42, 2).Should().Equal(Battle.BoonsFor(42, 2));
        Battle.BoonsFor(42, 2).Should().HaveCount(3).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Simulate_InputsWithCallAndBoon_ReplaysToTheSameHash()
    {
        // Arrange — fight live once, then replay the log.
        var live = new Battle(Setup(inputs: [new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(5, 2))]));
        RunToEnd(live, (b, e) =>
        {
            if (e is WaveGap)
            {
                b.Intervene(PlayerActionType.PickBoon, new GridPosition(0, 0));
                b.Intervene(PlayerActionType.CallWave);
                return true;
            }

            return false;
        });

        // Act
        var replay = Battle.Simulate(Setup(inputs: live.InputLog.ToList()));

        // Assert
        replay.Should().Be(live.Result);
    }

    [Fact]
    public void Stars_NobodyTouchedIt_OneStarAtMost()
    {
        // Act — the towers win it alone, the Hearth untouched.
        var result = Battle.Simulate(Setup());

        // Assert
        result.HearthHealth.Should().Be(result.HearthMaxHealth);
        result.Stars.Should().Be(1, "a Night with no input (and so an auto-resolve) earns one star at most");
    }

    [Fact]
    public void Stars_PlayedAndTheObjectiveMet_ThreeStars()
    {
        // Act — he walks somewhere quiet and never swoons.
        var result = Battle.Simulate(Setup(inputs: [new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(9, 0))]));

        // Assert
        result.ObjectiveMet.Should().BeTrue();
        result.Stars.Should().Be(3);
    }

    [Fact]
    public void Stars_PlayedButTheObjectiveMissed_TwoStars()
    {
        // Act — nobody threw three into one stone.
        var result = Battle.Simulate(Setup(objective: NightObjective.ThreeInOneStone,
            inputs: [new PlayerInput(0, PlayerActionType.MoveLeader, new GridPosition(9, 0))]));

        // Assert
        result.ObjectiveMet.Should().BeFalse();
        result.Stars.Should().Be(2);
    }

    [Fact]
    public void Step_FirstNightMaxOneWreck_OnlyOneBuildingFalls()
    {
        // Arrange — three huts a Levy's single swing would each wreck, beside an undefended road.
        var huts = new[] { 3, 5, 7 }.Select(x => new PlacedStructure(x, new GridPosition(x, 0), 15, 15)).ToArray();
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), Array.Empty<PlacedTower>(),
            huts.Select(h => h.Position), new GridPosition(9, 2), huts);
        var balance = new BalanceTable { HearthMaxHealth = 100_000 };

        // Act
        var capped = new Battle(Setup(budget: 1, waves: 1, maxWrecks: 1, layout: layout, balance: balance));
        RunToEnd(capped);
        var uncapped = new Battle(Setup(budget: 1, waves: 1, layout: layout, balance: balance));
        RunToEnd(uncapped);

        // Assert
        capped.Structures.Count(s => s.IsWrecked).Should().Be(1);
        capped.Structures.Where(s => !s.IsWrecked).Should().OnlyContain(s => s.Health == 1, "the rest are left hanging by a thread");
        uncapped.Structures.Count(s => s.IsWrecked).Should().Be(3);
    }

    [Fact]
    public void ObjectiveFor_TakesTurnsByNight()
    {
        // Act & Assert
        Battle.ObjectiveFor(1).Should().Be(NightObjective.NothingWrecked);
        Battle.ObjectiveFor(2).Should().Be(NightObjective.ProphetNeverSwoons);
        Battle.ObjectiveFor(3).Should().Be(NightObjective.ThreeInOneStone);
        Battle.ObjectiveFor(4).Should().Be(NightObjective.NothingWrecked);
    }
}
