using ScreenStat.Core.Models;

namespace ScreenStat.Core.Statistics;

/// <summary>
/// Statistics helpers for ScreenStat.
/// Percentile algorithm: linear interpolation on sorted values,
/// index = p * (n - 1), identical to the common "inclusive" method
/// (Excel PERCENTILE.INC / NIST R6 style).
/// </summary>
public static class StatisticsCalculator
{
    public static StatisticsResult Calculate(IReadOnlyList<double> values)
    {
        if (values is null || values.Count == 0)
        {
            return StatisticsResult.Empty;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        var count = sorted.Length;
        var sum = sorted.Sum();

        return new StatisticsResult
        {
            Count = count,
            Sum = sum,
            Average = sum / count,
            Min = sorted[0],
            Max = sorted[^1],
            Median = Percentile(sorted, 0.50),
            P90 = Percentile(sorted, 0.90),
            P95 = Percentile(sorted, 0.95),
            P99 = Percentile(sorted, 0.99)
        };
    }

    public static StatisticsResult Calculate(IReadOnlyList<NumberValue> values)
    {
        if (values is null || values.Count == 0)
        {
            return StatisticsResult.Empty;
        }

        return Calculate(values.Select(v => v.Value).ToArray());
    }

    /// <summary>
    /// Inclusive linear-interpolation percentile.
    /// p in [0, 1]. Assumes values are already sorted ascending.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues is null || sortedValues.Count == 0)
        {
            throw new ArgumentException("Values must not be empty.", nameof(sortedValues));
        }

        if (percentile is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 1.");
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var rank = percentile * (sortedValues.Count - 1);
        var lowIndex = (int)Math.Floor(rank);
        var highIndex = (int)Math.Ceiling(rank);

        if (lowIndex == highIndex)
        {
            return sortedValues[lowIndex];
        }

        var weight = rank - lowIndex;
        return sortedValues[lowIndex] + (sortedValues[highIndex] - sortedValues[lowIndex]) * weight;
    }
}
