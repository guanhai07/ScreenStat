namespace ScreenStat.Core.Models;

public sealed class NumberValue
{
    public required double Value { get; init; }
    public string? OriginalText { get; init; }
    public string? Unit { get; init; }
    public int Position { get; init; }
}
