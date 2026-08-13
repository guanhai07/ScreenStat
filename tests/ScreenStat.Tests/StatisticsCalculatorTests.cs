using ScreenStat.Core.Statistics;

namespace ScreenStat.Tests;

public class StatisticsCalculatorTests
{
    [Fact]
    public void Calculate_BasicStats()
    {
        var result = StatisticsCalculator.Calculate(new[] { 123d, 156d, 98d, 231d, 145d });

        Assert.Equal(5, result.Count);
        Assert.Equal(753, result.Sum);
        Assert.Equal(150.6, result.Average, 4);
        Assert.Equal(98, result.Min);
        Assert.Equal(231, result.Max);
        Assert.Equal(145, result.Median);

        var sorted = new[] { 98d, 123d, 145d, 156d, 231d };
        Assert.Equal(StatisticsCalculator.Percentile(sorted, 0.90), result.P90, 4);
    }

    [Fact]
    public void Percentile_LinearInterpolation_FiveValues()
    {
        var sorted = new[] { 1d, 2d, 3d, 4d, 5d };

        Assert.Equal(3, StatisticsCalculator.Percentile(sorted, 0.50));
        Assert.Equal(4.6, StatisticsCalculator.Percentile(sorted, 0.90), 4);
        Assert.Equal(4.8, StatisticsCalculator.Percentile(sorted, 0.95), 4);
        Assert.Equal(4.96, StatisticsCalculator.Percentile(sorted, 0.99), 4);
    }

    [Fact]
    public void Calculate_SingleValue()
    {
        var result = StatisticsCalculator.Calculate(new[] { 42d });
        Assert.Equal(1, result.Count);
        Assert.Equal(42, result.Sum);
        Assert.Equal(42, result.Average);
        Assert.Equal(42, result.Min);
        Assert.Equal(42, result.Max);
        Assert.Equal(42, result.Median);
        Assert.Equal(42, result.P90);
        Assert.Equal(42, result.P95);
        Assert.Equal(42, result.P99);
    }

    [Fact]
    public void Calculate_Empty_ReturnsEmpty()
    {
        var result = StatisticsCalculator.Calculate(Array.Empty<double>());
        Assert.Equal(0, result.Count);
        Assert.Equal(0, result.Sum);
    }
}
