using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class DefenderAndAutoRelicTests
{
    private const int All = (int)(Relic.LaserPointer | Relic.DynamoTorch | Relic.Magnifier);

    /// <summary>A 10×3 corridor; budget 1 is a single Levy from (0,1), walking right at 1 tile/s from tick 40.</summary>
    private static Battle StartBattle(int budget, GridPosition leaderStart, bool autoRelics = false, Relic relics = Relic.None, params GridPosition[] defenders)
    {
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], leaderStart);
        var balance = new BalanceTable { HearthMaxHealth = 100_000, Leader = BalanceTable.Default.Leader with { AttackDamage = Fix.Zero } };
        return new Battle(new BattleSetup(layout, Seed: 42, budget, 1, [], balance) { Relics = relics, AutoRelics = autoRelics, Defenders = defenders });
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
    public void Step_AutoRelics_TheLaserFiresAtARaiderInReachWithoutATap()
    {
        // Arrange — the Prophet stands at the far end; the Levy walks into the laser's reach.
        var battle = StartBattle(1, new GridPosition(8, 0), autoRelics: true, relics: (Relic)All);

        // Act
        var events = StepUntil(battle, 200);

        // Assert
        events.OfType<RelicUsed>().Should().Contain(r => r.Type == PlayerActionType.LaserPointer);
        battle.InputLog.Should().BeEmpty("nobody tapped anything");
    }

    [Fact]
    public void Step_RelicsNotAuto_NothingFiresByItself()
    {
        // Arrange — a Night planned before auto relics replays as it was.
        var battle = StartBattle(1, new GridPosition(8, 0), autoRelics: false, relics: (Relic)All);

        // Act
        var events = StepUntil(battle, 200);

        // Assert
        events.OfType<RelicUsed>().Should().BeEmpty();
    }

    [Fact]
    public void Step_ADefenderBesideTheRoad_GoesForTheRaiderAndHitsIt()
    {
        // Arrange — a cultist posted at (3,0), next to the road.
        var battle = StartBattle(1, new GridPosition(9, 0), defenders: new GridPosition(3, 0));

        // Act
        var events = StepUntil(battle, 200);

        // Assert
        events.OfType<DefenderAttacked>().Should().NotBeEmpty();
        events.OfType<DefenderHit>().Should().NotBeEmpty("the raider fights back");
    }

    [Fact]
    public void Step_NoRaiderInGuardRange_TheDefenderStaysAtTheirPost()
    {
        // Arrange — posted far up the corridor; nothing has come yet.
        var battle = StartBattle(1, new GridPosition(9, 0), defenders: new GridPosition(8, 2));

        // Act
        StepUntil(battle, 30);

        // Assert
        battle.Defenders.Single().Position.Should().Be(new GridPosition(8, 2).ToVector());
        battle.Defenders.Single().TargetId.Should().Be(-1);
    }

    [Fact]
    public void Step_DefenderBeatenDown_ComesBackAtTheirPostLater()
    {
        // Arrange — a fragile cultist against a big wave.
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], new GridPosition(9, 0));
        var balance = new BalanceTable
        {
            HearthMaxHealth = 100_000, Leader = BalanceTable.Default.Leader with { AttackDamage = Fix.Zero },
            Defender = BalanceTable.Default.Defender with { MaxHealth = 3, DownTicks = 40 },
        };
        var battle = new Battle(new BattleSetup(layout, Seed: 42, 1, 1, [], balance) { Defenders = new[] { new GridPosition(3, 0) } });

        // Act
        var events = StepUntil(battle, 600);

        // Assert
        var downed = events.OfType<DefenderDowned>().First();
        var back = events.OfType<DefenderBack>().First();
        (back.Tick - downed.Tick).Should().Be(40);
    }

    [Fact]
    public void Simulate_SameNightTwice_SameResultAndHash()
    {
        // Arrange
        BattleResult Run() => StartBattle(3, new GridPosition(6, 0), autoRelics: true, relics: (Relic)All, new GridPosition(3, 0), new GridPosition(5, 2)).RunToCompletion();

        // Act
        var a = Run();
        var b = Run();

        // Assert
        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public void BestHotStonesTarget_NobodyInReach_IsNull()
    {
        // Arrange
        var battle = StartBattle(1, new GridPosition(9, 0));

        // Act / Assert
        battle.BestHotStonesTarget().Should().BeNull();
    }

    [Fact]
    public void PlanNight_ACultistHut_SendsOutOnePerLevel()
    {
        // Arrange
        var t0 = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
        var table = new BuildingTable { StartingStock = new ResourceBag(9000, 9000, 9000, 9000), HearthStorageCaps = new[] { 9000, 9000, 9000 } };
        var state = BaseState.CreateNew(table, VillageMap.Demo, t0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.CultistHut, new GridPosition(14, 12), false), t0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), t0);
        var level = state.Snapshot(t0).Buildings.Single(b => b.Type == BuildingType.CultistHut).Level;

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), t0).Plan!;

        // Assert
        plan.AutoRelics.Should().BeTrue();
        level.Should().BeGreaterThan(0);
        plan.Defenders.Should().HaveCount(level);
        plan.Defenders!.Distinct().Should().ContainSingle("they all stand at the hut's door");
    }
}
