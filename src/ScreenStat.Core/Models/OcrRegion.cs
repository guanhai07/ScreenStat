namespace ScreenStat.Core.Models;

public sealed class OcrRegion
{
    public required string Text { get; init; }
    public required OcrBounds Bounds { get; init; }
    public double Confidence { get; init; }
    public string Engine { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
}
