using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Numerics;

public class FixTests
{
    [Fact]
    public void Multiply_TwoFractions_ReturnsExactProduct()
    {
        // Arrange
        var a = Fix.Ratio(3, 2);
        var b = Fix.Ratio(5, 2);

        // Act
        var product = a * b;

        // Assert
        product.Should().Be(Fix.Ratio(15, 4));
    }

    [Fact]
    public void Divide_IntegerValues_ReturnsFraction()
    {
        // Arrange
        var a = Fix.FromInt(7);
        var b = Fix.FromInt(2);

        // Act
        var quotient = a / b;

        // Assert
        quotient.Should().Be(Fix.Ratio(7, 2));
        quotient.ToDouble().Should().Be(3.5);
    }

    [Fact]
    public void FloorToInt_NegativeFraction_RoundsTowardNegativeInfinity()
    {
        // Arrange
        var value = Fix.Ratio(-3, 2);

        // Act
        var floored = value.FloorToInt();

        // Assert
        floored.Should().Be(-2);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange
        var value = Fix.One;

        // Act
        Action act = () => _ = value / Fix.Zero;

        // Assert
        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Ratio_WithZeroDenominator_ThrowsDivideByZeroException()
    {
        // Act
        Action act = () => Fix.Ratio(1, 0);

        // Assert
        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Clamp_ValueAboveMax_ReturnsMax()
    {
        // Arrange
        var value = Fix.FromInt(12);

        // Act
        var clamped = Fix.Clamp(value, Fix.Zero, Fix.FromInt(10));

        // Assert
        clamped.Should().Be(Fix.FromInt(10));
    }
}
