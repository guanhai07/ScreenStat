namespace ScreenStat.Core.Models;

public sealed class OcrResult
{
    public required string FullText { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static OcrResult Failed(string message) => new()
    {
        FullText = string.Empty,
        Success = false,
        ErrorMessage = message
    };
}
