using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using Xunit.Abstractions;

namespace CultOfComfort.Simulation.Tests.World;

public class BotCultsTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly WorldMap Server1 = WorldGenerator.Generate(20261014);

    [Fact]
    public void Populate_Eighty_AllPlacedWithUniqueNames()
    {
        // Arrange
        var ledger = new WorldLedger(Server1, DomainTable.Default, T0);

        // Act
        var added = BotCults.Populate(ledger, 80, 20261014, T0);

        // Assert
        added.Should().Be(80);
        ledger.Cults.Should().OnlyContain(c => c.IsBot && c.Id.StartsWith(BotCults.IdPrefix));
        ledger.Cults.Select(c => c.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Act_CalledTwice_NeverMoreThanTheSlots()
    {
        // Arrange
        var ledger = new WorldLedger(Server1, DomainTable.Default, T0);
        BotCults.Populate(ledger, 20, 1, T0);

        // Act
        BotCults.Act(ledger, DomainTable.Default, T0);
        BotCults.Act(ledger, DomainTable.Default, T0);

        // Assert
        ledger.Missionaries.GroupBy(m => m.CultId).Should().OnlyContain(g => g.Count() <= BotCults.HabitsOf(g.Key).Slots);
        ledger.Missionaries.Should().OnlyContain(m => Server1.Node(m.NodeId)!.DistanceTo(ledger.Cult(m.CultId)!.Q, ledger.Cult(m.CultId)!.R) <= BotCults.HabitsOf(m.CultId).Reach);
    }

    [Fact]
    public void Retire_HumansArrive_TheWeakestBotsLeaveAndTheirInfluenceWithThem()
    {
        // Arrange — 10 bots, one of them strong somewhere.
        var ledger = new WorldLedger(Server1, DomainTable.Default, T0);
        BotCults.Populate(ledger, 10, 2, T0);
        var strong = ledger.Cults.First();
        ledger.Grant(Server1.Nodes[0].Id, strong.Id, 50_000);
        for (var i = 0; i < 4; i++)
        {
            ledger.Join($"human{i}", string.Empty, false, (ulong)(100 + i), T0);
        }

        // Act
        var retired = BotCults.Retire(ledger, 10);

        // Assert
        retired.Should().Be(4);
        ledger.Cults.Should().HaveCount(10);
        ledger.Cult(strong.Id).Should().NotBeNull("it had influence; the empty-handed bots went first");
        ledger.Humans.Should().Be(4);
    }

    [Fact]
    public void Run_SameSeed_SameReport()
    {
        // Act
        var a = WorldSimulation.Run(7, humans: 40, bots: 20, days: 2);
        var b = WorldSimulation.Run(7, humans: 40, bots: 20, days: 2);

        // Assert
        a.Should().Equal(b);
    }

    [Fact]
    public void Run_FullWorldForTwoWeeks_TheMapFillsUpAndStaysContestedButSane()
    {
        // Act — a full world: 500 humans arriving over the first days, 80 bots at launch.
        var report = WorldSimulation.Run(20261014, humans: 500, bots: 80, days: 14);
        output.WriteLine(WorldSimulation.Format(report));
        var last = report[^1];

        // Assert
        last.Cults.Should().Be(500, "bots retire as humans fill the world");
        last.Held.Should().BeGreaterThan(Server1.Nodes.Count / 3, "a full world reaches well past its own doorsteps");
        last.Contested.Should().BeGreaterThan(100, "rivals meet");
        last.Patrons.Should().BeGreaterThan(0);
        last.TensionMax.Should().BeLessThan(30, "the world's Tension must stay below the raid machinery's own scale");
    }
}
