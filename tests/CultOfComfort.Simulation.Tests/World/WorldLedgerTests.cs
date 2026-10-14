using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;

namespace CultOfComfort.Simulation.Tests.World;

public class WorldLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly WorldMap Server1 = WorldGenerator.Generate(20261014);
    private static readonly DomainTable Table = DomainTable.Default;

    private static WorldLedger NewLedger() => new(Server1, Table, T0);

    /// <summary>A place with offerings (not a seat) close to the cult.</summary>
    private static WorldNode PlaceNear(WorldCult cult) =>
        Server1.Near(cult.Q, cult.R, 6).Where(n => n.Type is NodeType.Hamlet or NodeType.Village)
            .OrderBy(n => n.DistanceTo(cult.Q, cult.R)).ThenBy(n => n.Id).First();

    [Fact]
    public void Join_TwoCults_SpacedAndIdempotent()
    {
        // Arrange
        var ledger = NewLedger();

        // Act
        var a = ledger.Join("a", "Alpha", false, 1, T0)!;
        var b = ledger.Join("b", "Beta", false, 2, T0)!;
        var again = ledger.Join("a", "Alpha", false, 99, T0);

        // Assert
        Hex.Distance(a.Q, a.R, b.Q, b.R).Should().BeGreaterThanOrEqualTo(Server1.Shape.VillageSpacing);
        again.Should().Be(a);
        ledger.Cults.Should().HaveCount(2);
    }

    [Fact]
    public void Grant_EmptyPlace_TakesFromLoyaltyOnly()
    {
        // Arrange
        var ledger = NewLedger();
        var node = Server1.Nodes[0];

        // Act
        var gained = ledger.Grant(node.Id, "a", 30_000);

        // Assert
        gained.Should().Be(30_000);
        ledger.Loyalty(node.Id).Should().Be(70_000);
    }

    [Fact]
    public void Grant_NoLoyaltyLeft_DisplacesOthersInProportion()
    {
        // Arrange — the Authority has 10 left, b holds 60, c holds 30.
        var ledger = NewLedger();
        var node = Server1.Nodes[0];
        ledger.Grant(node.Id, "b", 60_000);
        ledger.Grant(node.Id, "c", 30_000);

        // Act — a brings 19: 10 from loyalty, 9 displaced (6 from b, 3 from c).
        var gained = ledger.Grant(node.Id, "a", 19_000);

        // Assert
        gained.Should().Be(19_000);
        ledger.Influence(node.Id, "b").Should().Be(54_000);
        ledger.Influence(node.Id, "c").Should().Be(27_000);
        ledger.Loyalty(node.Id).Should().Be(0);
    }

    [Fact]
    public void Grant_ManyRivals_NeverMoreThanOneHundred()
    {
        // Arrange
        var ledger = NewLedger();
        var node = Server1.Nodes[0];

        // Act
        for (var i = 0; i < 200; i++)
        {
            ledger.Grant(node.Id, $"c{i % 7}", 7_777 + i * 13);
        }

        // Assert
        ledger.Holders(node.Id).Values.Sum().Should().BeLessThanOrEqualTo(WorldLedger.Full);
        ledger.Holders(node.Id).Values.Should().OnlyContain(v => v > 0);
    }

    [Fact]
    public void PatronOf_BelowForty_NoPatron()
    {
        // Arrange
        var ledger = NewLedger();
        var node = Server1.Nodes[0];
        ledger.Grant(node.Id, "a", 39_999);

        // Act
        var before = ledger.PatronOf(node.Id);
        ledger.Grant(node.Id, "a", 1);
        var after = ledger.PatronOf(node.Id);

        // Assert
        before.Should().BeNull();
        after.Should().Be("a");
    }

    [Fact]
    public void Advance_SermonEnds_InfluenceLandsOnTimeNotBefore()
    {
        // Arrange
        var ledger = NewLedger();
        var cult = ledger.Join("a", "Alpha", false, 1, T0)!;
        var node = PlaceNear(cult);
        var ends = ledger.PreachEnds(cult, node, 4, T0);
        ledger.Dispatch("a", node.Id, 4, 0, T0);

        // Act
        ledger.Advance(ends.AddSeconds(-1));
        var early = ledger.Influence(node.Id, "a");
        var changes = ledger.Advance(ends);

        // Assert
        early.Should().Be(0);
        ledger.Influence(node.Id, "a").Should().Be(ledger.Gain(node, 4, 0));
        ledger.Missionaries.Should().BeEmpty();
        changes.Should().ContainSingle(c => c.CultId == "a" && c.SharesChanged);
    }

    [Fact]
    public void Advance_BecomesPatron_SpikesTension()
    {
        // Arrange — a already holds 39; one more sermon crowns it.
        var ledger = NewLedger();
        var cult = ledger.Join("a", "Alpha", false, 1, T0)!;
        var node = PlaceNear(cult);
        ledger.Grant(node.Id, "a", 39_000);
        ledger.Dispatch("a", node.Id, 8, 0, T0);

        // Act
        var changes = ledger.Advance(T0.AddDays(0.9));

        // Assert
        ledger.PatronOf(node.Id).Should().Be("a");
        changes.Single(c => c.CultId == "a").TensionSpike.Should().Be(Table.PatronTensionSpike);
    }

    [Fact]
    public void Advance_OneDay_FadesTwoPercent()
    {
        // Arrange
        var ledger = NewLedger();
        var node = Server1.Nodes[0];
        ledger.Grant(node.Id, "a", 50_000);

        // Act
        ledger.Advance(T0.AddDays(1));

        // Assert
        ledger.Influence(node.Id, "a").Should().Be(49_000);
        ledger.NextDecayAt.Should().Be(T0.AddDays(2));
    }

    [Fact]
    public void Purse_FullHoldingAtHamlet_AccruesAndCapsAtTheBuffer()
    {
        // Arrange — 100 of a hamlet: its whole 30 Timber an hour, plus the Patron's 25%.
        var ledger = NewLedger();
        ledger.Join("a", "Alpha", false, 1, T0);
        var hamlet = Server1.Nodes.First(n => n.Type == NodeType.Hamlet);
        ledger.Grant(hamlet.Id, "a", WorldLedger.Full);
        var perHour = Table.Kinds[NodeType.Hamlet].OfferingsPerHour.Timber * (100 + Table.PatronBonusPercent) / 100;

        // Act
        var afterTwo = ledger.Purse("a", T0.AddHours(2));
        var afterDay = ledger.Purse("a", T0.AddHours(20));

        // Assert
        afterTwo.Timber.Should().Be(perHour * 2);
        afterDay.Timber.Should().Be(perHour * Table.OfferingsBufferHours);
    }

    [Fact]
    public void Take_PartOfThePurse_LeavesTheRest()
    {
        // Arrange
        var ledger = NewLedger();
        ledger.Join("a", "Alpha", false, 1, T0);
        var hamlet = Server1.Nodes.First(n => n.Type == NodeType.Hamlet);
        ledger.Grant(hamlet.Id, "a", WorldLedger.Full);
        var at = T0.AddHours(2);
        var purse = ledger.Purse("a", at);

        // Act
        ledger.Take("a", new ResourceBag(10, 0, 0, 0), at);

        // Assert
        ledger.Purse("a", at).Timber.Should().Be(purse.Timber - 10);
    }

    [Fact]
    public void DispatchProblem_BeyondReach_Refused()
    {
        // Arrange
        var ledger = NewLedger();
        var cult = ledger.Join("a", "Alpha", false, 1, T0)!;
        var far = Server1.Nodes.OrderByDescending(n => n.DistanceTo(cult.Q, cult.R)).First();

        // Act
        var problem = ledger.DispatchProblem("a", far.Id, 4, reach: 5);
        var bad = ledger.DispatchProblem("a", PlaceNear(cult).Id, 3, reach: 10);

        // Assert
        problem.Should().Contain("Too far");
        bad.Should().NotBeNull("3 hours is not a session");
    }

    [Fact]
    public void ToData_Restore_SameLedger()
    {
        // Arrange
        var ledger = NewLedger();
        var cult = ledger.Join("a", "Alpha", false, 1, T0)!;
        ledger.Join("b", "Beta", true, 2, T0);
        var node = PlaceNear(cult);
        ledger.Grant(node.Id, "a", 45_000);
        ledger.Grant(node.Id, "b", 20_000);
        ledger.Dispatch("b", node.Id, 2, 25, T0);

        // Act
        var restored = WorldLedger.Restore(Server1, Table, ledger.ToData());

        // Assert
        restored.ToData().Should().BeEquivalentTo(ledger.ToData());
        restored.PatronOf(node.Id).Should().Be("a");
        restored.Purse("a", T0.AddHours(3)).Should().Be(ledger.Purse("a", T0.AddHours(3)));
    }
}
