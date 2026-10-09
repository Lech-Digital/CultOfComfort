using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Waves;

namespace CultOfComfort.Simulation.Tests.Waves;

public class WaveGeneratorTests
{
    private readonly BalanceTable _balance = BalanceTable.Default;

    [Fact]
    public void Generate_SameSeed_ProducesIdenticalWaves()
    {
        // Act
        var first = WaveGenerator.Generate(40, 4, 99, _balance);
        var second = WaveGenerator.Generate(40, 4, 99, _balance);

        // Assert
        second.Should().Equal(first);
    }

    [Fact]
    public void Generate_AnyBudget_SpendsExactlyTheBudgetInOrder()
    {
        // Act
        var spawns = WaveGenerator.Generate(40, 4, 7, _balance);

        // Assert
        spawns.Sum(s => _balance.Enemies[s.Type].WaveCost).Should().Be(40);
        spawns.Select(s => s.Tick).Should().BeInAscendingOrder();
        spawns.Select(s => s.Wave).Should().BeInAscendingOrder().And.Contain(4);
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentWaves()
    {
        // Act
        var first = WaveGenerator.Generate(40, 4, 1, _balance);
        var second = WaveGenerator.Generate(40, 4, 2, _balance);

        // Assert
        second.Should().NotEqual(first);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(10, 0)]
    public void Generate_NonPositiveBudgetOrWaves_ThrowsArgumentOutOfRangeException(int budget, int waves)
    {
        // Act
        Action act = () => WaveGenerator.Generate(budget, waves, 1, _balance);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
