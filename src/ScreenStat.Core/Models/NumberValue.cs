namespace ScreenStat.Core.Models;

public sealed class NumberValue
{
    public required double Value { get; init; }
    public string? OriginalText { get; init; }
    public string? Unit { get; init; }
    public int Position { get; init; }

    /// <summary>
    /// True when a digit look-alike had to be repaired to produce this value,
    /// so it should be shown for review rather than trusted silently.
    /// </summary>
    public bool IsCorrected { get; init; }
}
