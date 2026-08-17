using System.Runtime.InteropServices;
using RapidOcrNet;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;
using SkiaSharp;

namespace ScreenStat.App.Services;

/// <summary>
/// Runs the bundled PP-OCRv5 ONNX models locally and preserves word coordinates.
/// The model sessions are initialized on first use and then reused for later captures.
/// </summary>
public sealed class RapidLayoutOcrService : ILayoutOcrService, IDisposable
{
    private const string EngineName = "PP-OCRv5-ONNX";

    private static readonly RapidOcrOptions ScreenshotOptions = RapidOcrOptions.Default with
    {
        DoAngle = false,
        ReturnWordBox = true,
        ReturnSingleCharBox = false,
        TextScore = 0.30f,
        ImgResize = 0,
        LimitSideLen = 960,
        MaxSideLen = 2560,
        Padding = 24
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private RapidOcr? _ocr;
    private bool _disposed;

    public async Task<OcrDocument> RecognizeLayoutAsync(
        byte[] bgraPixels,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);
        ValidateImage(bgraPixels, width, height);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInitialized();

            using var bitmap = CreateBitmap(bgraPixels, width, height);
            var result = await Task.Run(
                () => _ocr!.Detect(bitmap, ScreenshotOptions),
                cancellationToken).ConfigureAwait(false);

            var regions = ConvertRegions(result);
            return new OcrDocument
            {
                Success = regions.Count > 0,
                Engine = EngineName,
                Regions = regions,
                ErrorMessage = regions.Count == 0 ? "未检测到文字区域。" : null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OcrDocument.Failed(EngineName, $"本地 ONNX 识别失败：{exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ocr?.Dispose();
        _gate.Dispose();
    }

    private void EnsureInitialized()
    {
        if (_ocr is not null)
        {
            return;
        }

        var ocr = new RapidOcr();
        try
        {
            ocr.InitModels();
            _ocr = ocr;
        }
        catch
        {
            ocr.Dispose();
            throw;
        }
    }

    private static SKBitmap CreateBitmap(byte[] bgraPixels, int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul));
        Marshal.Copy(bgraPixels, 0, bitmap.GetPixels(), bgraPixels.Length);
        return bitmap;
    }

    private static IReadOnlyList<OcrRegion> ConvertRegions(global::RapidOcrNet.OcrResult result)
    {
        var regions = new List<OcrRegion>();
        var sourceOrder = 0;

        foreach (var block in result.TextBlocks ?? [])
        {
            var words = block.WordResults;
            if (words is { Length: > 0 })
            {
                foreach (var word in words)
                {
                    AddRegion(regions, word.Text, word.BoxPoints, word.Score, sourceOrder++);
                }

                continue;
            }

            var confidence = block.CharScores is { Length: > 0 }
                ? block.CharScores.Average(score => (double)score)
                : block.BoxScore;
            AddRegion(regions, block.Text, block.BoxPoints, confidence, sourceOrder++);
        }

        return regions;
    }

    private static void AddRegion(
        ICollection<OcrRegion> regions,
        string? text,
        IReadOnlyList<SKPointI>? points,
        double confidence,
        int sourceOrder)
    {
        if (string.IsNullOrWhiteSpace(text) || points is null || points.Count == 0)
        {
            return;
        }

        var left = points.Min(point => (double)point.X);
        var top = points.Min(point => (double)point.Y);
        var right = points.Max(point => (double)point.X);
        var bottom = points.Max(point => (double)point.Y);
        var bounds = new OcrBounds(left, top, right - left, bottom - top);
        if (bounds.IsEmpty)
        {
            return;
        }

        regions.Add(new OcrRegion
        {
            Text = text.Trim(),
            Bounds = bounds,
            Confidence = Math.Clamp(confidence, 0, 1),
            Engine = EngineName,
            SourceOrder = sourceOrder
        });
    }

    private static void ValidateImage(byte[] pixels, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var requiredLength = checked(width * height * 4);
        if (pixels.Length != requiredLength)
        {
            throw new ArgumentException(
                $"BGRA 像素长度应为 {requiredLength}，实际为 {pixels.Length}。",
                nameof(pixels));
        }
    }
}
