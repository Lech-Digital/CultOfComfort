using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Combat;

public class RaidTests
{
    private const int House = 7;

    // A straight road along row 0 from (0,0) to the Hearth at (11,0); the land below it is open.
    private static readonly List<GridPosition> Road = Enumerable.Range(0, 12).Select(x => new GridPosition(x, 0)).ToList();

    private static BattleLayout Layout(IEnumerable<PlacedStructure> structures) =>
        new(12, 8, new GridPosition(0, 0), new GridPosition(11, 0), Array.Empty<PlacedTower>(),
            structures.SelectMany(s => s.Tiles ?? new[] { s.Position }).ToList(), new GridPosition(0, 7), structures, Road); // the Prophet far off

    private static PlacedStructure Hut(int x, int y) => new(House, new GridPosition(x, y), 60, 60);

    private static PlacedStructure Wall(int id, int x, int y, int health = 400) =>
        new(id, new GridPosition(x, y), health, health) { Barrier = true };

    private static (List<BattleEvent> Events, Battle Battle, bool LeftTheRoad) Run(BattleLayout layout, int budget = 24)
    {
        var battle = new Battle(new BattleSetup(layout, 11, budget, 1, [], new Balance.BalanceTable { HearthMaxHealth = 100_000 })); // the Hearth outlasts them all
        var events = new List<BattleEvent>();
        var leftTheRoad = false;
        while (!battle.IsFinished)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
            leftTheRoad |= battle.Enemies.Any(e => e.IsActive && e.RaidTargetId >= 0 && e.IsOffRoad);
        }

        return (events, battle, leftTheRoad);
    }

    [Fact]
    public void Raider_HutTwoTilesOffTheRoad_DetoursSmashesItAndComesBack()
    {
        // Act
        var (events, battle, leftTheRoad) = Run(Layout(new[] { Hut(5, 2) }));

        // Assert
        leftTheRoad.Should().BeTrue("they walk over to it");
        events.OfType<StructureHit>().Should().Contain(h => h.StructureId == House);
        battle.Enemies.Should().OnlyContain(e => !e.IsActive, "everyone either died or reached the Hearth: nobody got stuck in the field");
        battle.Tick.Should().BeLessThan(new Balance.BalanceTable().MaxTicks - 1);
    }

    [Fact]
    public void Raider_HutBeyondRaidRange_IsLeftAlone()
    {
        // Act
        var (events, _, leftTheRoad) = Run(Layout(new[] { Hut(5, 5) }));

        // Assert
        events.OfType<StructureHit>().Should().BeEmpty();
        leftTheRoad.Should().BeFalse();
    }

    [Fact]
    public void Raider_HutInsideAWall_HacksAtTheWallFirst()
    {
        // Arrange — the hut at (5,3) is ringed by a strong wall.
        var ring = new[] { (4, 2), (5, 2), (6, 2), (4, 3), (6, 3), (4, 4), (5, 4), (6, 4) }
            .Select((t, i) => Wall(100 + i, t.Item1, t.Item2)).ToList();

        // Act
        var (events, _, _) = Run(Layout(ring.Append(Hut(5, 3))));

        // Assert — the wall takes the blows; the hut is only reached through a gap.
        var hits = events.OfType<StructureHit>().ToList();
        hits.Should().Contain(h => h.StructureId >= 100, "they hack at the wall");
        var firstHutHit = hits.FindIndex(h => h.StructureId == House);
        if (firstHutHit >= 0)
        {
            var firstGap = events.FindIndex(e => e is StructureWrecked w && w.StructureId >= 100);
            firstGap.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(events.IndexOf(hits[firstHutHit]), "only through a gap in the wall");
        }
    }

    [Fact]
    public void Raider_WeakFenceInTheWay_BreaksThroughAndReachesTheHut()
    {
        // Arrange — a flimsy fence (15 HP) between the road and the hut at (5,3): the first blow opens a gap.
        var fence = new[] { (4, 2), (5, 2), (6, 2), (4, 3), (6, 3), (4, 4), (5, 4), (6, 4) }
            .Select((t, i) => Wall(100 + i, t.Item1, t.Item2, health: 15)).ToList();

        // Act
        var (events, _, _) = Run(Layout(fence.Append(Hut(5, 3))), budget: 40);

        // Assert
        events.OfType<StructureWrecked>().Should().Contain(w => w.StructureId >= 100);
        events.OfType<StructureHit>().Should().Contain(h => h.StructureId == House, "through the gap");
    }

    [Fact]
    public void Gate_TheProphetWalksThroughRaidersDoNot()
    {
        // Arrange
        var gate = new PlacedStructure(9, new GridPosition(5, 3), 200, 200) { Barrier = true, Gate = true };

        // Act
        var layout = Layout(new[] { gate });

        // Assert
        layout.IsBlocked(new GridPosition(5, 3)).Should().BeTrue();
        layout.IsWalkableForLeader(new GridPosition(5, 3)).Should().BeTrue();
    }

    [Fact]
    public void Raid_SameSetupTwice_SameHash()
    {
        // Act
        var a = Battle.Simulate(new BattleSetup(Layout(new[] { Hut(5, 2) }), 3, 30, 2, []));
        var b = Battle.Simulate(new BattleSetup(Layout(new[] { Hut(5, 2) }), 3, 30, 2, []));

        // Assert
        b.StateHash.Should().Be(a.StateHash);
    }

    [Fact]
    public void Village_WallsFencesAndGates_AreBarriers()
    {
        // Arrange
        var t0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(5000, 900, 1000, 300) }, VillageMap.Demo, t0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WoodenWall, new GridPosition(13, 4)), t0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, new GridPosition(14, 4)), t0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(15, 4)), t0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var structures = state.ToBattleLayout().Structures.ToDictionary(s => s.Position);

        // Assert
        structures[new GridPosition(13, 4)].Should().Match<PlacedStructure>(s => s.Barrier && !s.Gate && s.MaxHealth == 250);
        structures[new GridPosition(14, 4)].Should().Match<PlacedStructure>(s => s.Barrier && s.Gate && s.MaxHealth == 200);
        structures[new GridPosition(15, 4)].Barrier.Should().BeTrue();
    }
}
