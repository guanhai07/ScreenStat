using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenStat.App.Services;
using ScreenStat.Core.Dataset;

namespace ScreenStat.DatasetTests;

/// <summary>
/// The capture is written by WPF and read back by System.Drawing, so the
/// dataset is only trustworthy if that round trip is lossless — otherwise a
/// replayed sample would not be the image OCR originally saw.
/// </summary>
public sealed class CaptureDatasetRecorderTests : IDisposable
{
    private const string RootVariable = "SCREENSTAT_DATASET_DIR";
    private const string EnableVariable = "SCREENSTAT_DATASET";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ScreenStatRecorderTests",
        Guid.NewGuid().ToString("N"));

    private readonly string? _previousRoot;
    private readonly string? _previousEnable;

    public CaptureDatasetRecorderTests()
    {
        _previousRoot = Environment.GetEnvironmentVariable(RootVariable);
        _previousEnable = Environment.GetEnvironmentVariable(EnableVariable);
        Environment.SetEnvironmentVariable(RootVariable, _root);
        // Forces collection on without touching the developer's saved settings.
        Environment.SetEnvironmentVariable(EnableVariable, "1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(RootVariable, _previousRoot);
        Environment.SetEnvironmentVariable(EnableVariable, _previousEnable);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void BeginCapture_WritesPixelsThatSurviveReloading()
    {
        const int width = 7;
        const int height = 5;
        var expected = BuildPixels(width, height);
        var recorder = new CaptureDatasetRecorder(new AppSettingsService());

        var session = recorder.TryBeginCapture(CreateImage(width, height, expected), width, height);

        Assert.NotNull(session);
        Assert.Equal(_root, recorder.Root);
        var (actual, loadedWidth, loadedHeight) =
            DatasetImageLoader.Load(DatasetStore.ImagePath(session!.DirectoryPath));
        Assert.Equal(width, loadedWidth);
        Assert.Equal(height, loadedHeight);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BeginCapture_ReturnsNullWhenCollectionIsOff()
    {
        Environment.SetEnvironmentVariable(EnableVariable, null);
        var recorder = new CaptureDatasetRecorder(new AppSettingsService());

        Assert.Null(recorder.TryBeginCapture(CreateImage(2, 2, BuildPixels(2, 2)), 2, 2));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void Discard_RemovesTheWholeCaptureDirectory()
    {
        var recorder = new CaptureDatasetRecorder(new AppSettingsService());
        var session = recorder.TryBeginCapture(CreateImage(3, 3, BuildPixels(3, 3)), 3, 3);
        Assert.NotNull(session);

        session!.Discard();

        Assert.False(Directory.Exists(session.DirectoryPath));
        Assert.True(session.IsDiscarded);
    }

    private static byte[] BuildPixels(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < width * height; index++)
        {
            pixels[index * 4] = (byte)(index * 7 % 256);       // B
            pixels[index * 4 + 1] = (byte)(index * 13 % 256);  // G
            pixels[index * 4 + 2] = (byte)(index * 29 % 256);  // R
            pixels[index * 4 + 3] = 255;                       // A, as BitBlt captures end up
        }

        return pixels;
    }

    private static BitmapSource CreateImage(int width, int height, byte[] pixels)
    {
        var source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        source.Freeze();
        return source;
    }
}
