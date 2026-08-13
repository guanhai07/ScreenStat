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

    private async Task<IReadOnlyList<double>> ParseRenderedAsync(string text)
    {
        var (bgra, width, height) = RenderTextToBgra(text);
        var ocr = await new WindowsOcrService().RecognizeAsync(bgra, width, height);
        if (!ocr.Success)
        {
            throw new SkipException("Windows OCR language pack unavailable: " + ocr.ErrorMessage);
        }

        return _parser.Parse(ocr.FullText).Select(v => v.Value).ToList();
    }

    private static (byte[] bgra, int width, int height) RenderTextToBgra(string text)
    {
        using var font = new Font("Calibri", 48f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var probe = new Bitmap(1, 1);
        using (var graphics = Graphics.FromImage(probe))
        {
            var size = graphics.MeasureString(text, font);
            var width = Math.Max(64, (int)Math.Ceiling(size.Width) + 80);
            var height = Math.Max(48, (int)Math.Ceiling(size.Height) + 80);

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var draw = Graphics.FromImage(bitmap))
            {
                draw.Clear(Color.White);
                draw.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                draw.DrawString(text, font, Brushes.Black, 40f, 40f);
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
