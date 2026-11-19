using FluentAssertions;
using NiftyQuant.Application.Services.Indicators;
using Xunit;

namespace NiftyQuant.UnitTests;

public class CprCalculatorTests
{
    [Fact]
    public void Calculate_KnownValues_ProducesExactPivotAndCprLevels()
    {
        // Arrange
        const decimal high = 25100m;
        const decimal low = 24900m;
        const decimal close = 25050m;

        // Act
        var cpr = CprCalculator.Calculate(high, low, close);

        // Assert
        cpr.Pivot.Should().Be(25016.67m);
        cpr.BottomCentral.Should().Be(25000.00m);
        cpr.TopCentral.Should().Be(25033.34m);
        cpr.CprTop.Should().Be(25033.34m);
        cpr.CprBottom.Should().Be(25000.00m);
        cpr.CprWidth.Should().Be(33.34m);
        cpr.CprWidthPercent.Should().Be(0.1333m);
        cpr.IsNarrowCpr.Should().BeTrue(); // Width % is 0.1333 <= 0.15%

        cpr.R1.Should().Be(25133.34m);
        cpr.S1.Should().Be(24933.34m);
        cpr.R2.Should().Be(25216.67m);
        cpr.S2.Should().Be(24816.67m);
    }

    [Fact]
    public void Calculate_WideDay_ClassifiedAsNotNarrowCpr()
    {
        // High range day
        const decimal high = 25500m;
        const decimal low = 24500m;
        const decimal close = 24800m;

        var cpr = CprCalculator.Calculate(high, low, close);

        cpr.IsNarrowCpr.Should().BeFalse();
        cpr.CprWidthPercent.Should().BeGreaterThan(0.15m);
    }
}
