namespace ScreenStat.Core.Models;

public sealed class NumericToken
{
    public required double Value { get; init; }
    public required string OriginalText { get; init; }
    public string? Unit { get; init; }
    public required OcrBounds Bounds { get; init; }
    public double Confidence { get; init; }
    public int SourceOrder { get; init; }
    public bool IsLowConfidence => Confidence < 0.75;
}
