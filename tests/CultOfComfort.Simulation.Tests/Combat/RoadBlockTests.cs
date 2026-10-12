using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Combat;

public class RoadBlockTests
{
    private static readonly List<GridPosition> Road = Enumerable.Range(0, 12).Select(x => new GridPosition(x, 0)).ToList();

    private static BattleLayout Layout(params PlacedStructure[] structures) =>
        new(12, 8, new GridPosition(0, 0), new GridPosition(11, 0), Array.Empty<PlacedTower>(),
            structures.Select(s => s.Position).ToList(), new GridPosition(0, 7), structures, Road);

    private static Battle Start(BattleLayout layout, int budget = 24) =>
        new(new BattleSetup(layout, 11, budget, 1, [], new BalanceTable { HearthMaxHealth = 100_000 }));

    [Fact]
    public void RoadGate_Intact_NobodyPassesAndEveryoneKeepsSwinging()
    {
        // Arrange — a stout gate on the road at (6,0).
        var gate = new PlacedStructure(9, new GridPosition(6, 0), 600, 600) { Barrier = true, Gate = true };
        var battle = Start(Layout(gate));
        var hits = new List<StructureHit>();
        var passedWhileStanding = false;

        // Act
        while (!battle.IsFinished)
        {
            battle.Step();
            hits.AddRange(battle.LastTickEvents.OfType<StructureHit>());
            var standing = battle.Structures.Single().Health > 0;
            passedWhileStanding |= standing && battle.Enemies.Any(e => e.IsActive && battle.PositionOf(e).X > CultOfComfort.Simulation.Numerics.Fix.FromInt(5) + CultOfComfort.Simulation.Numerics.Fix.Ratio(1, 2));
        }

        // Assert — the road itself never changed: the gate blocks it.
        Layout(gate).ResolveEnemyPath().Should().Equal(Road);
        passedWhileStanding.Should().BeFalse();
        hits.GroupBy(h => h.EnemyId).Should().Contain(g => g.Count() > 1, "a raider keeps hacking at a road gate");
    }

    [Fact]
    public void RoadGate_Broken_TheRaidGoesOn()
    {
        // Arrange — a flimsy gate.
        var gate = new PlacedStructure(9, new GridPosition(6, 0), 30, 30) { Barrier = true, Gate = true };
        var battle = Start(Layout(gate));
        var events = new List<BattleEvent>();

        // Act
        while (!battle.IsFinished)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        // Assert
        events.OfType<StructureWrecked>().Should().ContainSingle(w => w.StructureId == 9);
        events.OfType<HearthDamaged>().Should().NotBeEmpty("through the broken gate to the Hearth");
    }

    [Fact]
    public void WallBreakers_ALoneWallByTheRoad_OnlySomeGoForIt()
    {
        // Arrange — a wall two tiles off the road with nothing behind it.
        var wall = new PlacedStructure(5, new GridPosition(5, 2), 5_000, 5_000) { Barrier = true };
        var battle = Start(Layout(wall), budget: 40);
        var hitters = new HashSet<int>();

        // Act
        while (!battle.IsFinished)
        {
            battle.Step();
            foreach (var hit in battle.LastTickEvents.OfType<StructureHit>())
            {
                hitters.Add(hit.EnemyId);
            }
        }

        // Assert
        hitters.Should().NotBeEmpty();
        battle.Enemies.Where(e => hitters.Contains(e.Id)).Should().OnlyContain(e => e.WallBreaker);
        battle.Enemies.Should().Contain(e => !e.WallBreaker, "most walk on");
    }

    [Fact]
    public void WallBreakers_SameSeed_SameRaiders()
    {
        // Act
        var a = Battle.Simulate(new BattleSetup(Layout(new PlacedStructure(5, new GridPosition(5, 2), 500, 500) { Barrier = true }), 3, 30, 2, []));
        var b = Battle.Simulate(new BattleSetup(Layout(new PlacedStructure(5, new GridPosition(5, 2), 500, 500) { Barrier = true }), 3, 30, 2, []));

        // Assert
        b.StateHash.Should().Be(a.StateHash);
    }
}
