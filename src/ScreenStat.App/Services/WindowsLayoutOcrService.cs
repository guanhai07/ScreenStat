using ScreenStat.App.Resources;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;

namespace ScreenStat.App.Services;

/// <summary>
/// Adapts the built-in Windows OCR engine to the layout contract. Windows OCR
/// does not expose stable word boxes here, so the adapter creates row bounds.
/// It is intentionally used only as an offline fallback.
/// </summary>
public sealed class WindowsLayoutOcrService : ILayoutOcrService
{
    private const string EngineName = "Windows OCR";
    private readonly IOcrService _ocrService;

    public WindowsLayoutOcrService(IOcrService? ocrService = null)
    {
        _ocrService = ocrService ?? new WindowsOcrService();
    }

    public async Task<OcrDocument> RecognizeLayoutAsync(
        byte[] bgraPixels,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var result = await _ocrService
            .RecognizeAsync(bgraPixels, width, height, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
        {
            return OcrDocument.Failed(EngineName, result.ErrorMessage ?? Strings.OcrWindowsFailed);
        }

        var lines = (result.FullText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return OcrDocument.Failed(EngineName, Strings.OcrWindowsNoText);
        }

        var rowHeight = Math.Max(1d, (double)height / lines.Length);
        var regions = lines
            .Select((line, index) => new OcrRegion
            {
                Text = line,
                Bounds = new OcrBounds(0, index * rowHeight, width, rowHeight),
                Confidence = 0.5,
                Engine = EngineName,
                SourceOrder = index
            })
            .ToArray();

        return new OcrDocument
        {
            Success = true,
            Engine = EngineName,
            Regions = regions
        };
    }
}
