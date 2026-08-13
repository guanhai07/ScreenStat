using ScreenStat.Core.Models;

namespace ScreenStat.Core.Abstractions;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(byte[] bgraPixels, int width, int height, CancellationToken cancellationToken = default);
}
