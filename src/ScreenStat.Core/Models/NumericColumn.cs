namespace ScreenStat.Core.Models;

public sealed class NumericColumn
{
    public required int Index { get; init; }
    public required IReadOnlyList<NumericToken> Tokens { get; init; }
    public required StatisticsResult Statistics { get; init; }

    public double Left => Tokens.Count == 0 ? 0 : Tokens.Min(token => token.Bounds.Left);
    public double Right => Tokens.Count == 0 ? 0 : Tokens.Max(token => token.Bounds.Right);
    public double CenterX => Tokens.Count == 0 ? 0 : Tokens.Average(token => token.Bounds.CenterX);
    public int LowConfidenceCount => Tokens.Count(token => token.IsLowConfidence);
}
