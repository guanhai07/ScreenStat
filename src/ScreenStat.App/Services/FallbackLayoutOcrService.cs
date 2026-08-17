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
            var primaryError = primaryResult.ErrorMessage ?? "未检测到文字区域";
            return new OcrDocument
            {
                Success = true,
                Engine = fallbackResult.Engine,
                Regions = fallbackResult.Regions,
                WarningMessage = $"本地 ONNX OCR 未完成识别，已自动改用 Windows OCR。原因：{primaryError}"
            };
        }

        return OcrDocument.Failed(
            $"{primaryResult.Engine} + {fallbackResult.Engine}",
            $"两种本地识别均失败。ONNX：{primaryResult.ErrorMessage ?? "未知错误"}；Windows OCR：{fallbackResult.ErrorMessage ?? "未知错误"}");
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
