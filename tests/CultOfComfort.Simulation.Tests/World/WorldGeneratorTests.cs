using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;

namespace CultOfComfort.Simulation.Tests.World;

public class WorldGeneratorTests
{
    private static readonly WorldMap Server1 = WorldGenerator.Generate(20261014);

    [Fact]
    public void Generate_SameSeed_SameWorld()
    {
        // Act
        var again = WorldGenerator.Generate(20261014);

        // Assert
        again.Nodes.Select(n => (n.Id, n.Name, n.Type, n.Q, n.R, n.Squalor, n.DomainId))
            .Should().Equal(Server1.Nodes.Select(n => (n.Id, n.Name, n.Type, n.Q, n.R, n.Squalor, n.DomainId)));
    }

    [Fact]
    public void Generate_OtherSeed_OtherWorld()
    {
        // Act
        var other = WorldGenerator.Generate(7);

        // Assert
        other.Nodes.Select(n => (n.Q, n.R)).Should().NotEqual(Server1.Nodes.Select(n => (n.Q, n.R)));
    }

    [Fact]
    public void Generate_Server1_HasTheSpecsSize()
    {
        // Act
        var places = Server1.Nodes.Count;
        var hexes = Hex.Disc(46).Count();

        // Assert
        hexes.Should().Be(6487);
        places.Should().BeInRange(2100, 2500, "about 35% of the hexes");
        Server1.Domains.Count.Should().Be(60);
        Server1.Domains.Count(d => d.Authority == AuthorityKind.Bishop).Should().BeGreaterThan(20);
        Server1.Nodes.Count(n => n.Type == NodeType.Hamlet).Should().BeGreaterThan(Server1.Nodes.Count(n => n.Type == NodeType.Abbey));
    }

    [Fact]
    public void Generate_EveryPlace_UniqueHexUniqueNameAndADomainWithASeat()
    {
        // Assert
        Server1.Nodes.Select(n => (n.Q, n.R)).Should().OnlyHaveUniqueItems();
        Server1.Nodes.Select(n => n.Name).Should().OnlyHaveUniqueItems();
        Server1.Nodes.Should().OnlyContain(n => Server1.Contains(n.Q, n.R));
        foreach (var domain in Server1.Domains)
        {
            var seat = Server1.Node(domain.SeatNodeId)!;
            seat.Type.Should().Be(domain.Authority == AuthorityKind.Warlord ? NodeType.Keep : NodeType.Cathedral);
            seat.DomainId.Should().Be(domain.Id);
        }

        Server1.Nodes.Should().OnlyContain(n => n.DomainId >= 1 && n.DomainId <= Server1.Domains.Count);
    }

    [Fact]
    public void PlaceVillage_FillingTheWorld_SpacedFairAndNoneOnAPlace()
    {
        // Act — 500 villages, one after another, each with its own player seed.
        var villages = new List<(int Q, int R)>();
        for (var i = 0; i < 500; i++)
        {
            var spot = Server1.PlaceVillage(villages, (ulong)(1000 + i));
            spot.Should().NotBeNull($"village {i} found room");
            villages.Add(spot!.Value);
        }

        // Assert
        Server1.PlaceVillage(villages, 9999).Should().BeNull("the world is full at 500");
        villages.Should().OnlyContain(v => Server1.NodeAt(v.Q, v.R) == null);
        for (var a = 0; a < villages.Count; a++)
        {
            for (var b = a + 1; b < villages.Count; b++)
            {
                Hex.Distance(villages[a].Q, villages[a].R, villages[b].Q, villages[b].R).Should().BeGreaterThanOrEqualTo(3);
            }
        }

        // Every village has places within a Hearth 3 reach of 5 hexes.
        villages.Should().OnlyContain(v => Server1.Near(v.Q, v.R, 5).Count() >= 8);
    }

    [Fact]
    public void PlaceVillage_FirstVillages_AreNotAllAtTheEdge()
    {
        // Act
        var villages = new List<(int Q, int R)>();
        for (var i = 0; i < 20; i++)
        {
            villages.Add(Server1.PlaceVillage(villages, (ulong)(50 + i))!.Value);
        }

        // Assert — random over the whole disc, not pushed outward.
        villages.Count(v => Hex.Distance(0, 0, v.Q, v.R) <= 25).Should().BeGreaterThan(3);
    }
}
