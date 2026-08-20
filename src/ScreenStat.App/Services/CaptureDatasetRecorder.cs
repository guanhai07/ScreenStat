using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenStat.Core.Dataset;

namespace ScreenStat.App.Services;

/// <summary>
/// Turns ordinary use of the app into regression data. When collection is on,
/// every capture gets its own directory holding the screenshot, what the engine
/// recognized, and — once the user reviews the result — what it should have
/// recognized.
/// </summary>
public sealed class CaptureDatasetRecorder
{
    private readonly AppSettingsService _settings;

    public CaptureDatasetRecorder(AppSettingsService settings)
    {
        _settings = settings;
        Root = DatasetLocator.ResolveRoot();
    }

    public string Root { get; }

    public bool IsEnabled => _settings.IsDatasetCaptureEnabled;

    /// <summary>
    /// Creates the capture directory and writes the screenshot immediately, so
    /// the image survives even if recognition later throws. Returns null when
    /// collection is off or the directory could not be prepared.
    /// </summary>
    public DatasetCaptureSession? TryBeginCapture(BitmapSource image, int width, int height)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var capturedAt = DateTimeOffset.Now;
        // Two captures can land in the same second, so the timestamp alone is
        // not a safe directory name.
        var suffix = Guid.NewGuid().ToString("N")[..4];
        var captureId = $"{capturedAt:yyyyMMdd-HHmmss}-{suffix}";
        var directory = Path.Combine(Root, captureId);

        try
        {
            Directory.CreateDirectory(directory);
            WritePng(image, DatasetStore.ImagePath(directory));
            return new DatasetCaptureSession(captureId, directory, capturedAt, width, height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the exact pixels that were handed to OCR. The source comes from
    /// BitBlt and may carry an unusable alpha channel, so it is converted to
    /// Bgra32 first — the same conversion ScreenCaptureService.ToBgra32Pixels
    /// applies — and PNG keeps the result lossless.
    /// </summary>
    private static void WritePng(BitmapSource source, string path)
    {
        var output = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
