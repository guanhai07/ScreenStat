using ScreenStat.App.Resources;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;

namespace ScreenStat.App.Services;

/// <summary>
/// Uses the coordinate-aware local ONNX engine first and falls back to Windows
/// OCR only when the primary engine cannot produce any regions.
/// </summary>
public sealed class FallbackLayoutOcrService : ILayoutOcrService, IDisposable
{
    private readonly ILayoutOcrService _primary;
    private readonly ILayoutOcrService _fallback;
    private bool _disposed;

    public FallbackLayoutOcrService(ILayoutOcrService primary, ILayoutOcrService fallback)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public async Task<OcrDocument> RecognizeLayoutAsync(
        byte[] bgraPixels,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var primaryResult = await RecognizeSafelyAsync(
            _primary,
            bgraPixels,
            width,
            height,
            cancellationToken).ConfigureAwait(false);
        if (primaryResult.Success && primaryResult.Regions.Count > 0)
        {
            return primaryResult;
        }

        var fallbackResult = await RecognizeSafelyAsync(
            _fallback,
            bgraPixels,
            width,
            height,
            cancellationToken).ConfigureAwait(false);
        if (fallbackResult.Success && fallbackResult.Regions.Count > 0)
        {
            var primaryError = primaryResult.ErrorMessage ?? Strings.OcrNoTextRegions;
            return new OcrDocument
            {
                Success = true,
                Engine = fallbackResult.Engine,
                Regions = fallbackResult.Regions,
                WarningMessage = string.Format(Strings.OcrFallbackWarning, primaryError)
            };
        }

        return OcrDocument.Failed(
            $"{primaryResult.Engine} + {fallbackResult.Engine}",
            string.Format(
                Strings.OcrBothEnginesFailed,
                primaryResult.ErrorMessage ?? Strings.OcrUnknownError,
                fallbackResult.ErrorMessage ?? Strings.OcrUnknownError));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_primary is IDisposable primaryDisposable)
        {
            primaryDisposable.Dispose();
        }

        if (!ReferenceEquals(_fallback, _primary) && _fallback is IDisposable fallbackDisposable)
        {
            fallbackDisposable.Dispose();
        }
    }

    private static async Task<OcrDocument> RecognizeSafelyAsync(
        ILayoutOcrService service,
        byte[] pixels,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service
                .RecognizeLayoutAsync(pixels, width, height, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OcrDocument.Failed(service.GetType().Name, exception.Message);
        }
    }
}
