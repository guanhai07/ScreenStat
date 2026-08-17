namespace ScreenStat.Core.Models;

public sealed class OcrDocument
{
    public bool Success { get; init; }
    public string Engine { get; init; } = string.Empty;
    public IReadOnlyList<OcrRegion> Regions { get; init; } = Array.Empty<OcrRegion>();
    public string? ErrorMessage { get; init; }

    public string FullText => string.Join(
        Environment.NewLine,
        Regions
            .OrderBy(region => region.Bounds.Top)
            .ThenBy(region => region.Bounds.Left)
            .Select(region => region.Text));

    public static OcrDocument Failed(string engine, string message) => new()
    {
        Success = false,
        Engine = engine,
        ErrorMessage = message
    };
}
