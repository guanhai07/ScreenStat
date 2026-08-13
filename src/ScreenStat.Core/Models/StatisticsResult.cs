namespace ScreenStat.Core.Models;

public sealed class StatisticsResult
{
    public int Count { get; init; }
    public double Sum { get; init; }
    public double Average { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Median { get; init; }
    public double P90 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }

    public static StatisticsResult Empty { get; } = new();
}
