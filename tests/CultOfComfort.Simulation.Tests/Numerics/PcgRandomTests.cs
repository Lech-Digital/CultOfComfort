using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Numerics;

public class PcgRandomTests
{
    [Fact]
    public void NextUInt_WithReferenceSeed_MatchesPcg32ReferenceOutput()
    {
        // Arrange — reference values from the official pcg32-demo (seed 42, stream 54).
        var random = new PcgRandom(42, 54);

        // Act
        var values = Enumerable.Range(0, 6).Select(_ => random.NextUInt()).ToArray();

        // Assert
        values.Should().Equal(0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e);
    }

    [Fact]
    public void NextUInt_WithDifferentStreams_ProducesDifferentSequences()
    {
        // Arrange
        var first = new PcgRandom(7, 1);
        var second = new PcgRandom(7, 2);

        // Act
        var a = Enumerable.Range(0, 4).Select(_ => first.NextUInt()).ToArray();
        var b = Enumerable.Range(0, 4).Select(_ => second.NextUInt()).ToArray();

        // Assert
        a.Should().NotEqual(b);
    }

    [Fact]
    public void Next_ManySamples_StaysWithinRange()
    {
        // Arrange
        var random = new PcgRandom(123, 1);

        // Act
        var values = Enumerable.Range(0, 10_000).Select(_ => random.Next(80, 121)).ToArray();

        // Assert
        values.Should().OnlyContain(v => v >= 80 && v < 121);
        values.Should().Contain(80).And.Contain(120);
    }

    [Fact]
    public void Next_WithEmptyRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var random = new PcgRandom(1, 1);

        // Act
        Action act = () => random.Next(5, 5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
