using ScreenStat.App.Services;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;

namespace ScreenStat.OcrTests;

public sealed class FallbackLayoutOcrServiceTests
{
    private static readonly byte[] Pixel = [0, 0, 0, 255];

    [Fact]
    public async Task PrimarySuccess_DoesNotCallFallback()
    {
        var primary = new StubLayoutOcrService(Success("ONNX", "123"));
        var fallback = new StubLayoutOcrService(Success("Windows", "999"));
        using var service = new FallbackLayoutOcrService(primary, fallback);

        var result = await service.RecognizeLayoutAsync(Pixel, 1, 1);

        Assert.Equal("ONNX", result.Engine);
        Assert.Null(result.WarningMessage);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task PrimaryFailure_ReturnsFallbackWithVisibleWarning()
    {
        var primary = new StubLayoutOcrService(OcrDocument.Failed("ONNX", "model unavailable"));
        var fallback = new StubLayoutOcrService(Success("Windows", "123"));
        using var service = new FallbackLayoutOcrService(primary, fallback);

        var result = await service.RecognizeLayoutAsync(Pixel, 1, 1);

        Assert.True(result.Success);
        Assert.Equal("Windows", result.Engine);
        Assert.Contains("model unavailable", result.WarningMessage);
        Assert.Equal("123", Assert.Single(result.Regions).Text);
    }

    [Fact]
    public async Task BothFail_ReturnsCombinedDiagnostic()
    {
        var primary = new StubLayoutOcrService(OcrDocument.Failed("ONNX", "model unavailable"));
        var fallback = new StubLayoutOcrService(OcrDocument.Failed("Windows", "language pack unavailable"));
        using var service = new FallbackLayoutOcrService(primary, fallback);

        var result = await service.RecognizeLayoutAsync(Pixel, 1, 1);

        Assert.False(result.Success);
        Assert.Contains("model unavailable", result.ErrorMessage);
        Assert.Contains("language pack unavailable", result.ErrorMessage);
    }

    private static OcrDocument Success(string engine, string text) => new()
    {
        Success = true,
        Engine = engine,
        Regions =
        [
            new OcrRegion
            {
                Text = text,
                Bounds = new OcrBounds(0, 0, 10, 10),
                Confidence = 0.9,
                Engine = engine
            }
        ]
    };

    private sealed class StubLayoutOcrService : ILayoutOcrService
    {
        private readonly OcrDocument _result;

        public StubLayoutOcrService(OcrDocument result) => _result = result;

        public int CallCount { get; private set; }

        public Task<OcrDocument> RecognizeLayoutAsync(
            byte[] bgraPixels,
            int width,
            int height,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
