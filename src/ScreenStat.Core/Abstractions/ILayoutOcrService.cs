using ScreenStat.Core.Models;

namespace ScreenStat.Core.Abstractions;

public interface ILayoutOcrService
{
    Task<OcrDocument> RecognizeLayoutAsync(
        byte[] bgraPixels,
        int width,
        int height,
        CancellationToken cancellationToken = default);
}
