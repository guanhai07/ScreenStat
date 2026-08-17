using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using ScreenStat.App.Services;
using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;
using Xunit.Sdk;

namespace ScreenStat.OcrTests;

public class WindowsOcrPipelineTests
{
    private readonly NumberParser _parser = new();

    [Fact]
    public async Task Recognize_PlainIntegers()
    {
        var text = "123\n156\n98\n231\n145";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, parsed);
    }

    [Fact]
    public async Task Recognize_DecimalsAndNegatives()
    {
        var text = "12.5\n-3.25\n0.5\n-10\n100.25";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 12.5, -3.25, 0.5, -10d, 100.25 }, parsed);
    }

    [Fact]
    public async Task Recognize_Percentage()
    {
        var text = "12.5%\n8%\n99.9%\n3.14%";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 12.5, 8d, 99.9, 3.14 }, parsed);
    }

    [Fact]
    public async Task Recognize_ThousandsSeparators()
    {
        var text = "1,234\n12,345.67\n2,000";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 1234d, 12345.67, 2000d }, parsed);
    }

    [Fact]
    public async Task Recognize_LogUnits()
    {
        var text = "query finished cost=123ms\nquery finished cost=156ms\nquery finished cost=98ms\nquery finished cost=231ms\nquery finished cost=145ms";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, parsed);
    }

    [Fact]
    public async Task Recognize_FiltersDateAndIp()
    {
        var text = "2026-08-12 10:30:25\n192.168.1.100\nlatency 42";
        var parsed = await ParseRenderedAsync(text);

        Assert.Equal(new[] { 42d }, parsed);
    }

    [Fact]
    public async Task Recognize_SmallDarkThemeIntegers()
    {
        var text = "123\n156\n98\n231\n145";
        var parsed = await ParseRenderedAsync(
            text,
            fontName: "Consolas",
            fontSize: 18f,
            foreground: Color.FromArgb(220, 220, 220),
            background: Color.FromArgb(30, 30, 30));

        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, parsed);
    }

    [Fact]
    public async Task Recognize_SmallDarkThemeDecimalsAndNegatives()
    {
        var text = "12.5\n-3.25\n0.5\n-10\n100.25";
        var parsed = await ParseRenderedAsync(
            text,
            fontName: "Consolas",
            fontSize: 18f,
            foreground: Color.FromArgb(212, 212, 212),
            background: Color.FromArgb(30, 30, 30));

        Assert.Equal(new[] { 12.5, -3.25, 0.5, -10d, 100.25 }, parsed);
    }

    [Fact]
    public async Task Recognize_RecordedDarkThemeIntegerCrop()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "recorded-dark-theme.png");
        using var screenshot = new Bitmap(path);
        using var crop = screenshot.Clone(
            new Rectangle(48, 9, 46, 126),
            PixelFormat.Format32bppArgb);
        var (bgra, width, height) = BitmapToBgra(crop);

        var parsed = await ParseBgraAsync(bgra, width, height);

        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, parsed);
    }

    private async Task<IReadOnlyList<double>> ParseRenderedAsync(
        string text,
        string fontName = "Calibri",
        float fontSize = 48f,
        Color? foreground = null,
        Color? background = null)
    {
        var (bgra, width, height) = RenderTextToBgra(
            text,
            fontName,
            fontSize,
            foreground ?? Color.Black,
            background ?? Color.White);
        return await ParseBgraAsync(bgra, width, height);
    }

    private async Task<IReadOnlyList<double>> ParseBgraAsync(byte[] bgra, int width, int height)
    {
        var ocr = await new WindowsOcrService().RecognizeAsync(bgra, width, height);
        if (!ocr.Success)
        {
            throw new SkipException("Windows OCR language pack unavailable: " + ocr.ErrorMessage);
        }

        return _parser.Parse(ocr.FullText).Select(v => v.Value).ToList();
    }

    private static (byte[] bgra, int width, int height) BitmapToBgra(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var bgra = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
            return (bgra, bitmap.Width, bitmap.Height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static (byte[] bgra, int width, int height) RenderTextToBgra(
        string text,
        string fontName,
        float fontSize,
        Color foreground,
        Color background)
    {
        using var font = new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using var probe = new Bitmap(1, 1);
        using (var graphics = Graphics.FromImage(probe))
        {
            var size = graphics.MeasureString(text, font);
            var width = Math.Max(64, (int)Math.Ceiling(size.Width) + 80);
            var height = Math.Max(48, (int)Math.Ceiling(size.Height) + 80);

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var draw = Graphics.FromImage(bitmap))
            {
                draw.Clear(background);
                draw.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using var brush = new SolidBrush(foreground);
                draw.DrawString(text, font, brush, 40f, 40f);
            }

            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                var bgra = new byte[Math.Abs(data.Stride) * height];
                Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
                return (bgra, width, height);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
