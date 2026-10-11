using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class StructureDamageTests
{
    /// <summary>Corridor 10x3, road along y = 1; one Levy (budget 1) walks right from tick 40 at 1 tile/s.</summary>
    private static Battle StartBattle(int budget, GridPosition? leader, params PlacedStructure[] structures) =>
        StartBattle(budget, leader, Array.Empty<PlacedTower>(), structures);

    private static Battle StartBattle(int budget, GridPosition? leader, PlacedTower[] towers, params PlacedStructure[] structures)
    {
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), towers,
            structures.Where(s => towers.All(t => t.Position != s.Position)).Select(s => s.Position), leader ?? new GridPosition(9, 0), structures);
        var balance = new BalanceTable { HearthMaxHealth = 100_000 };
        return new Battle(new BattleSetup(layout, 42, budget, 1, [], balance));
    }

    private static List<BattleEvent> RunToEnd(Battle battle)
    {
        var events = new List<BattleEvent>();
        while (!battle.IsFinished)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void Defaults_Balance_HasStructureDamagePerRaider()
    {
        BalanceTable.Default.Version.Should().Be("2026.10.4");
        BalanceTable.Default.StructureSwingTicks.Should().Be(16);
        BalanceTable.Default.Enemies[EnemyType.Levy].StructureDamage.Should().Be(15);
        BalanceTable.Default.Enemies[EnemyType.ManAtArms].StructureDamage.Should().Be(40);
        BalanceTable.Default.Enemies[EnemyType.Zealot].StructureDamage.Should().Be(10);
    }

    [Fact]
    public void Step_LevyPassesAHut_HitsItOnceAndWalksOn()
    {
        // Arrange — a hut beside the road at (4,0).
        var battle = StartBattle(1, null, new PlacedStructure(7, new GridPosition(4, 0), 60, 60));

        // Act
        var events = new List<BattleEvent>();
        var furthest = Fix.Zero;
        while (!battle.IsFinished)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
            if (battle.Enemies.Count > 0)
            {
                furthest = Fix.Max(furthest, battle.Enemies[0].DistanceAlongPath);
            }
        }

        // Assert
        events.OfType<StructureHit>().Should().ContainSingle().Which.Should().Match<StructureHit>(h => h.StructureId == 7 && h.Damage == 15 && h.HealthRemaining == 45);
        battle.Structures.Single().Health.Should().Be(45);
        furthest.Should().BeGreaterThan(Fix.FromInt(5), "after its swing it walks on past the hut");
    }

    [Fact]
    public void Step_LevyBetweenTwoHuts_HitsEachOnceOneAfterTheOther()
    {
        // Arrange — huts on both sides of the same road tile.
        var battle = StartBattle(1, null,
            new PlacedStructure(1, new GridPosition(4, 0), 60, 60),
            new PlacedStructure(2, new GridPosition(4, 2), 60, 60));

        // Act
        var hits = RunToEnd(battle).OfType<StructureHit>().ToList();

        // Assert
        hits.Select(h => h.StructureId).Should().BeEquivalentTo(new[] { 1, 2 });
        (hits[1].Tick - hits[0].Tick).Should().BeGreaterThanOrEqualTo(BalanceTable.Default.StructureSwingTicks);
    }

    [Fact]
    public void Step_StructureAtZero_IsWreckedOnce()
    {
        // Arrange — a fence with 10 health.
        var battle = StartBattle(1, null, new PlacedStructure(3, new GridPosition(4, 0), 50, 10));

        // Act
        var events = RunToEnd(battle);

        // Assert
        events.OfType<StructureWrecked>().Should().ContainSingle().Which.StructureId.Should().Be(3);
        battle.Structures.Single().IsWrecked.Should().BeTrue();
        battle.Structures.Single().Health.Should().Be(0);
    }

    [Fact]
    public void Step_WreckedTower_StopsFiring()
    {
        // Arrange — a Slurry Chute at (4,0) with 1 health: the first Levy wrecks it.
        var chute = new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 0));
        var battle = StartBattle(3, null, new[] { chute }, new PlacedStructure(1, new GridPosition(4, 0), 80, 1));

        // Act
        var events = RunToEnd(battle);

        // Assert
        var wreck = events.OfType<StructureWrecked>().Single();
        events.OfType<TowerFired>().Should().OnlyContain(f => f.Tick <= wreck.Tick);
    }

    [Fact]
    public void Step_AlreadyWreckedTowerAtNightStart_NeverFires()
    {
        // Arrange
        var chute = new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 0));
        var battle = StartBattle(3, null, new[] { chute }, new PlacedStructure(1, new GridPosition(4, 0), 80, 0));

        // Act
        var events = RunToEnd(battle);

        // Assert
        events.OfType<TowerFired>().Should().BeEmpty();
        events.OfType<StructureHit>().Should().BeEmpty("nobody kicks a ruin");
    }

    [Fact]
    public void Step_LevyChasingTheProphet_DoesNotStopToSmash()
    {
        // Arrange — the Prophet beside the road at (4,2) lures the Levy; a hut at (3,0) is within a tile of its road path.
        var battle = StartBattle(1, new GridPosition(4, 2), new PlacedStructure(1, new GridPosition(3, 0), 60, 60));
        var smashedWhileHunting = false;

        // Act
        while (!battle.IsFinished && battle.Tick < 600)
        {
            battle.Step();
            smashedWhileHunting |= battle.LastTickEvents.OfType<StructureHit>().Any() && battle.Enemies.Any(e => e.IsFightingLeader || e.IsOffRoad);
        }

        // Assert
        smashedWhileHunting.Should().BeFalse();
    }

    [Fact]
    public void Simulate_LayoutWithoutStructures_UnchangedFromBefore()
    {
        // Arrange — the same corridor run twice, with and without an empty structure list.
        var plain = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], []);
        var empty = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], null, Array.Empty<PlacedStructure>());

        // Act
        var a = Battle.Simulate(new BattleSetup(plain, 7, 12, 3, []));
        var b = Battle.Simulate(new BattleSetup(empty, 7, 12, 3, []));

        // Assert
        b.StateHash.Should().Be(a.StateHash);
    }
}
