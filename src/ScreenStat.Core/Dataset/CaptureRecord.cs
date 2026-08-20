using ScreenStat.Core.Models;

namespace ScreenStat.Core.Dataset;

/// <summary>
/// One capture exactly as the engine recognized it. Written once when the
/// screenshot is taken and never edited afterwards, so it stays usable as
/// evidence of what the current algorithm produced. The ground truth the user
/// curates for the same capture lives in <see cref="CaptureLabels"/>.
/// </summary>
public sealed class CaptureRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string CaptureId { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public required CaptureImageInfo Image { get; init; }
    public required CaptureOcrInfo Ocr { get; init; }
    public IReadOnlyList<CaptureColumnRecord> Columns { get; init; } = Array.Empty<CaptureColumnRecord>();

    public static CaptureRecord Create(
        string captureId,
        DateTimeOffset capturedAt,
        string imageFile,
        int width,
        int height,
        OcrDocument document,
        IReadOnlyList<NumericColumn> columns,
        TimeSpan elapsed) => new()
        {
            CaptureId = captureId,
            CapturedAt = capturedAt,
            Image = CaptureImageInfo.Create(imageFile, width, height),
            Ocr = CaptureOcrInfo.FromDocument(document, elapsed),
            Columns = columns.Select(CaptureColumnRecord.FromColumn).ToArray()
        };
}

public sealed class CaptureImageInfo
{
    public required string File { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>
    /// Width over height. RapidLayoutOcrService picks its detector preset from
    /// this ratio alone, so recording it makes the preset recoverable later
    /// without the service having to expose which one it used.
    /// </summary>
    public double AspectRatio { get; init; }

    public static CaptureImageInfo Create(string file, int width, int height) => new()
    {
        File = file,
        Width = width,
        Height = height,
        AspectRatio = height <= 0 ? 0 : Math.Round((double)width / height, 4)
    };
}

public sealed class CaptureOcrInfo
{
    public required string Engine { get; init; }
    public bool Success { get; init; }
    public long ElapsedMs { get; init; }
    public string? Warning { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<CaptureRegionRecord> Regions { get; init; } = Array.Empty<CaptureRegionRecord>();

    public static CaptureOcrInfo FromDocument(OcrDocument document, TimeSpan elapsed) => new()
    {
        Engine = document.Engine,
        Success = document.Success,
        ElapsedMs = (long)Math.Round(elapsed.TotalMilliseconds),
        Warning = document.WarningMessage,
        Error = document.ErrorMessage,
        Regions = document.Regions.Select(CaptureRegionRecord.FromRegion).ToArray()
    };
}

public sealed class CaptureRegionRecord
{
    public int SourceOrder { get; init; }
    public required string Text { get; init; }
    public double Confidence { get; init; }
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public OcrBounds ToBounds() => new(Left, Top, Width, Height);

    public static CaptureRegionRecord FromRegion(OcrRegion region) => new()
    {
        SourceOrder = region.SourceOrder,
        Text = region.Text,
        Confidence = Math.Round(region.Confidence, 4),
        Left = region.Bounds.Left,
        Top = region.Bounds.Top,
        Width = region.Bounds.Width,
        Height = region.Bounds.Height
    };
}

public sealed class CaptureColumnRecord
{
    public int Index { get; init; }
    public IReadOnlyList<CaptureTokenRecord> Tokens { get; init; } = Array.Empty<CaptureTokenRecord>();

    public static CaptureColumnRecord FromColumn(NumericColumn column) => new()
    {
        Index = column.Index,
        Tokens = column.Tokens.Select(CaptureTokenRecord.FromToken).ToArray()
    };
}

public sealed class CaptureTokenRecord
{
    public int SourceOrder { get; init; }
    public required string Text { get; init; }
    public double Value { get; init; }
    public string? Unit { get; init; }
    public double Confidence { get; init; }
    public bool IsCorrected { get; init; }
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public static CaptureTokenRecord FromToken(NumericToken token) => new()
    {
        SourceOrder = token.SourceOrder,
        Text = token.OriginalText,
        Value = token.Value,
        Unit = token.Unit,
        Confidence = Math.Round(token.Confidence, 4),
        IsCorrected = token.IsCorrected,
        Left = token.Bounds.Left,
        Top = token.Bounds.Top,
        Width = token.Bounds.Width,
        Height = token.Bounds.Height
    };
}
