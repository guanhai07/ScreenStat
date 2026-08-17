using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using ScreenStat.App.Services;
using ScreenStat.Core.Analysis;

namespace ScreenStat.OcrTests;

public sealed class RapidLayoutOcrServiceTests
{
    [Fact]
    public async Task RecognizeLayout_SixteenReportRows_PreservesEveryValueAndCoordinate()
    {
        var expected = Enumerable.Range(1, 16)
            .Select(index => 1000d + index * 37)
            .ToArray();
        var text = string.Join(Environment.NewLine, expected.Select(value => value.ToString("0")));
        var (pixels, width, height) = RenderTextToBgra(
            text,
            "Segoe UI",
            20f,
            Color.FromArgb(35, 35, 35),
            Color.White);

        using var service = new RapidLayoutOcrService();
        var document = await service.RecognizeLayoutAsync(pixels, width, height);
        var columns = Analyze(document);

        Assert.True(document.Success, document.ErrorMessage);
        var column = Assert.Single(columns);
        Assert.Equal(expected, column.Tokens.Select(token => token.Value));
        Assert.All(column.Tokens, token => Assert.True(token.Bounds.Height > 0));
    }

    [Fact]
    public async Task RecognizeLayout_RecordedDarkThemeCrop_RecognizesAllFiveRows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "recorded-dark-theme.png");
        using var screenshot = new Bitmap(path);
        using var crop = screenshot.Clone(
            new Rectangle(48, 9, 46, 126),
            PixelFormat.Format32bppArgb);
        var (pixels, width, height) = BitmapToBgra(crop);

        using var service = new RapidLayoutOcrService();
        var document = await service.RecognizeLayoutAsync(pixels, width, height);
        var columns = Analyze(document);

        Assert.True(document.Success, document.ErrorMessage);
        var column = Assert.Single(columns);
        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, column.Tokens.Select(token => token.Value));
    }

    private static IReadOnlyList<ScreenStat.Core.Models.NumericColumn> Analyze(
        ScreenStat.Core.Models.OcrDocument document)
    {
        var parser = new NumericRegionParser();
        var analyzer = new CoordinateColumnAnalyzer();
        return analyzer.Analyze(parser.Parse(document));
    }

    private static (byte[] pixels, int width, int height) RenderTextToBgra(
        string text,
        string fontName,
        float fontSize,
        Color foreground,
        Color background)
    {
        using var font = new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var probe = new Bitmap(1, 1);
        using var probeGraphics = Graphics.FromImage(probe);
        var size = probeGraphics.MeasureString(text, font);
        var width = Math.Max(80, (int)Math.Ceiling(size.Width) + 40);
        var height = Math.Max(80, (int)Math.Ceiling(size.Height) + 40);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(background);
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using var brush = new SolidBrush(foreground);
            graphics.DrawString(text, font, brush, 20f, 20f);
        }

        return BitmapToBgra(bitmap);
    }

    private static (byte[] pixels, int width, int height) BitmapToBgra(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return (pixels, bitmap.Width, bitmap.Height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
