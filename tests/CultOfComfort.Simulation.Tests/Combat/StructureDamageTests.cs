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
        BalanceTable.Default.Version.Should().Be("2026.10.6");
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
            smashedWhileHunting |= battle.LastTickEvents.OfType<StructureHit>().Any(h => battle.Enemies.First(e => e.Id == h.EnemyId) is { } e && (e.IsFightingLeader || e.HasLeaderSlot));
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

    /// <summary>A 40-tile corridor with a hut beside every road tile from x = 2 to 30; one raider at the given speed.</summary>
    private static (Battle Battle, int Huts) LongRoad(Fix tilesPerSecond)
    {
        var huts = Enumerable.Range(2, 29).Select(x => new PlacedStructure(x, new GridPosition(x, 0), 60, 60)).ToArray();
        var layout = new BattleLayout(40, 3, new GridPosition(0, 1), new GridPosition(39, 1), [], huts.Select(h => h.Position), new GridPosition(39, 2), huts);
        var enemies = new Dictionary<EnemyType, EnemyStats>(BalanceTable.Default.Enemies);
        enemies[EnemyType.Levy] = enemies[EnemyType.Levy] with { TilesPerSecond = tilesPerSecond };
        var balance = new BalanceTable { HearthMaxHealth = 100_000, Enemies = enemies, MaxTicks = 20_000 };
        return (new Battle(new BattleSetup(layout, 42, 1, 1, [], balance)), huts.Length);
    }

    [Theory]
    [InlineData(1, 1)]    // Levy pace
    [InlineData(3, 5)]    // Man-at-arms pace
    [InlineData(11, 10)]  // Zealot pace
    public void Step_RaiderAtAnyPaceAlongALongRoad_HitsEveryHutItPasses(int numerator, int denominator)
    {
        // Arrange
        var (battle, huts) = LongRoad(Fix.Ratio(numerator, denominator));

        // Act
        var hits = RunToEnd(battle).OfType<StructureHit>().Select(h => h.StructureId).ToList();

        // Assert
        hits.Should().OnlyHaveUniqueItems().And.HaveCount(huts, "a hut beside the road is hit wherever it stands, at any walking pace");
    }

    [Fact]
    public void Step_HutDiagonalToTheRoad_IsHit()
    {
        // Arrange — the road turns: spawn (0,1) → Hearth (9,1) with a wall forcing a bend; the hut sits diagonally to a road tile.
        var battle = StartBattle(1, null, new PlacedStructure(5, new GridPosition(4, 0), 60, 60));
        var path = battle.Path;
        var diagonal = new PlacedStructure(6, new GridPosition(path[3].X + 1, path[3].Y + 1), 60, 60);
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], new[] { diagonal.Position }, new GridPosition(9, 0), new[] { diagonal });
        var diagonalBattle = new Battle(new BattleSetup(layout, 42, 1, 1, [], new BalanceTable { HearthMaxHealth = 100_000 }));

        // Act
        var hits = RunToEnd(diagonalBattle).OfType<StructureHit>();

        // Assert
        hits.Should().Contain(h => h.StructureId == 6);
    }

    [Fact]
    public void Step_SluiceMidSwing_TheSwingIsCancelledNotLandedFromAfar()
    {
        // Arrange — the Prophet waits by the Hearth; wait until the Levy starts swinging at the hut at (5,0).
        var battle = StartBattle(1, null, new PlacedStructure(1, new GridPosition(5, 0), 60, 60));
        while (!battle.IsFinished && (battle.Enemies.Count == 0 || battle.Enemies[0].SmashTargetId < 0))
        {
            battle.Step();
        }

        // Act — the sluice washes it three tiles back mid-swing.
        battle.Intervene(PlayerActionType.OpenSluiceGate);
        var hits = new List<(StructureHit Hit, Fix X)>();
        while (!battle.IsFinished && battle.Tick < 1000)
        {
            battle.Step();
            foreach (var hit in battle.LastTickEvents.OfType<StructureHit>())
            {
                hits.Add((hit, battle.PositionOf(battle.Enemies[0]).X));
            }
        }

        // Assert — when it does hit the hut, it is standing next to it.
        hits.Should().OnlyContain(h => (h.X - Fix.FromInt(5)).Abs() <= Fix.FromInt(1) + Fix.Ratio(1, 2));
    }

    [Fact]
    public void Step_FrightenedMidSwing_TheSwingIsCancelled()
    {
        // Arrange — a Levy that ignores the Prophet (no notice range); he stands across the road from the hut at (5,0).
        var hut = new PlacedStructure(1, new GridPosition(5, 0), 60, 60);
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], new[] { hut.Position }, new GridPosition(5, 2), new[] { hut });
        var enemies = new Dictionary<EnemyType, EnemyStats>(BalanceTable.Default.Enemies);
        enemies[EnemyType.Levy] = enemies[EnemyType.Levy] with { NoticeRange = Fix.Zero, EngageRange = Fix.Zero };
        var battle = new Battle(new BattleSetup(layout, 42, 1, 1, [], new BalanceTable { HearthMaxHealth = 100_000, Enemies = enemies }));
        while (!battle.IsFinished && (battle.Enemies.Count == 0 || battle.Enemies[0].SmashTargetId < 0))
        {
            battle.Step();
        }

        // Act
        battle.Intervene(PlayerActionType.PhantomVoice);
        battle.Step();

        // Assert
        battle.Enemies[0].IsFrightened(battle.Tick - 1).Should().BeTrue();
        battle.Enemies[0].SmashTargetId.Should().Be(-1, "a fright breaks the swing");
        battle.Enemies[0].SmashTicksRemaining.Should().Be(0);
    }
}

