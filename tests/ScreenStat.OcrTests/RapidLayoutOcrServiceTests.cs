using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenStat.App.Services;
using ScreenStat.Core.Analysis;
using Xunit.Abstractions;

namespace ScreenStat.OcrTests;

public sealed class RapidLayoutOcrServiceTests
{
    private readonly ITestOutputHelper _output;

    public RapidLayoutOcrServiceTests(ITestOutputHelper output) => _output = output;

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
        var stopwatch = Stopwatch.StartNew();
        var document = await service.RecognizeLayoutAsync(pixels, width, height);
        stopwatch.Stop();
        var columns = Analyze(document);

        _output.WriteLine("16-row cold OCR elapsed: {0} ms", stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        var warmDocument = await service.RecognizeLayoutAsync(pixels, width, height);
        stopwatch.Stop();
        var warmColumns = Analyze(warmDocument);
        _output.WriteLine("16-row warm OCR elapsed: {0} ms", stopwatch.ElapsedMilliseconds);

        Assert.True(document.Success, document.ErrorMessage);
        var column = Assert.Single(columns);
        Assert.Equal(expected, column.Tokens.Select(token => token.Value));
        Assert.All(column.Tokens, token => Assert.True(token.Bounds.Height > 0));
        Assert.Equal(expected, Assert.Single(warmColumns).Tokens.Select(token => token.Value));
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

    [Fact]
    public async Task RecognizeLayout_FourteenPixelBrowserStyleTable_RecognizesThreeColumnsOfSixteen()
    {
        var expected = new[]
        {
            Enumerable.Range(0, 16).Select(index => 1037d + index * 37).ToArray(),
            new[] { 12.5, 13.2, 11.9, 15.6, 14.1, 16.8, 10.7, 12.3, 17.4, 13.9, 11.2, 18.1, 14.7, 12.8, 16.2, 13.5 },
            new[] { 99.1, 98.8, 99.4, 97.9, 98.5, 99.0, 99.6, 98.7, 97.8, 99.2, 99.5, 97.6, 98.9, 99.3, 98.1, 99.7 }
        };
        var (pixels, width, height) = RenderTableToBgra(expected);

        using var service = new RapidLayoutOcrService();
        var document = await service.RecognizeLayoutAsync(pixels, width, height);
        var columns = Analyze(document);

        Assert.True(document.Success, document.ErrorMessage);
        Assert.Equal(3, columns.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], columns[index].Tokens.Select(token => token.Value));
        }
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

    private static (byte[] pixels, int width, int height) RenderTableToBgra(IReadOnlyList<double[]> columns)
    {
        const int columnWidth = 138;
        const int rowHeight = 30;
        var width = columnWidth * columns.Count;
        var height = rowHeight * columns[0].Length;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(34, 34, 34));
        using var alternateBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var gridPen = new Pen(Color.FromArgb(216, 216, 216));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center
        };

        graphics.Clear(Color.White);
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        for (var row = 0; row < columns[0].Length; row++)
        {
            if (row % 2 == 1)
            {
                graphics.FillRectangle(alternateBrush, 0, row * rowHeight, width, rowHeight);
            }

            for (var column = 0; column < columns.Count; column++)
            {
                var bounds = new RectangleF(
                    column * columnWidth + 8,
                    row * rowHeight,
                    columnWidth - 16,
                    rowHeight);
                graphics.DrawString(columns[column][row].ToString("0.##"), font, textBrush, bounds, format);
            }

            graphics.DrawLine(gridPen, 0, row * rowHeight, width, row * rowHeight);
        }

        for (var column = 0; column <= columns.Count; column++)
        {
            graphics.DrawLine(gridPen, column * columnWidth, 0, column * columnWidth, height);
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
