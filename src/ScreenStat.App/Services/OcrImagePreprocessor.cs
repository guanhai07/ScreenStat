using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenStat.App.Services;

/// <summary>
/// Preprocess screen crops so Windows.Media.Ocr works better on IDE/dark-theme text.
/// Also splits vertical lists into row bands so short numbers (e.g. 98) are not dropped.
/// </summary>
internal static class OcrImagePreprocessor
{
    public sealed record PreparedImage(byte[] BgraPixels, int Width, int Height, string Profile);

    public static IReadOnlyList<PreparedImage> BuildCandidates(byte[] sourceBgra, int width, int height)
    {
        if (sourceBgra.Length < width * height * 4 || width <= 0 || height <= 0)
        {
            return Array.Empty<PreparedImage>();
        }

        var gray = ToGrayscale(sourceBgra, width, height);
        var mean = Mean(gray);
        var isDark = mean < 140;

        var normalized = isDark ? Invert(gray) : (byte[])gray.Clone();
        normalized = StretchContrast(normalized);

        var candidates = new List<PreparedImage>();
        var scale = ChooseScale(width, height);

        {
            var (w, h, pixels) = UpscaleAndPad(normalized, width, height, scale, pad: 24);
            candidates.Add(new PreparedImage(pixels, w, h, "full-contrast"));
        }

        foreach (var threshold in new byte[] { 150, 180 })
        {
            var binary = Binarize(normalized, threshold);
            var (w, h, pixels) = UpscaleAndPad(binary, width, height, Math.Max(scale, 3), pad: 28);
            candidates.Add(new PreparedImage(pixels, w, h, "full-bin-" + threshold));
        }

        var rows = SegmentRows(normalized, width, height);
        var rowIndex = 0;
        foreach (var row in rows)
        {
            rowIndex++;
            var rowGray = Crop(normalized, width, height, row.Y, row.Height);
            var rowScale = Math.Max(scale + 1, row.Height < 12 ? 5 : 4);
            var padX = Math.Max(32, width + 8);
            var (w, h, pixels) = UpscaleAndPadFlexible(rowGray, width, row.Height, rowScale, padX: padX, padY: 24);
            candidates.Add(new PreparedImage(pixels, w, h, "row-" + rowIndex));

            var rowBin = Binarize(rowGray, 165);
            var bin = UpscaleAndPadFlexible(rowBin, width, row.Height, rowScale, padX: padX, padY: 24);
            candidates.Add(new PreparedImage(bin.bgra, bin.width, bin.height, "row-bin-" + rowIndex));
        }

        return candidates;
    }

    public static void TrySaveDebugPng(PreparedImage image, string fileName)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            var source = BitmapSource.Create(
                image.Width,
                image.Height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                image.BgraPixels,
                image.Width * 4);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = System.IO.File.Create(path);
            encoder.Save(fs);
        }
        catch
        {
            // debug aid only
        }
    }

    private readonly record struct RowBand(int Y, int Height);

    private static List<RowBand> SegmentRows(byte[] gray, int width, int height)
    {
        var ink = new int[height];
        for (var y = 0; y < height; y++)
        {
            var count = 0;
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                if (gray[rowOffset + x] < 200)
                {
                    count++;
                }
            }

            ink[y] = count;
        }

        // Close small vertical gaps so a single glyph is not split into two bands.
        // Example: anti-aliased "8"/"0"/"5" can have a near-empty scanline in the middle.
        var closed = (int[])ink.Clone();
        for (var y = 1; y < height - 1; y++)
        {
            if (closed[y] == 0 && ink[y - 1] > 0 && ink[y + 1] > 0)
            {
                closed[y] = Math.Min(ink[y - 1], ink[y + 1]);
            }
        }

        // Second pass for 2px gaps.
        for (var y = 2; y < height - 2; y++)
        {
            if (closed[y] == 0 && closed[y + 1] == 0 && ink[y - 1] > 0 && ink[y + 2] > 0)
            {
                closed[y] = ink[y - 1];
                closed[y + 1] = ink[y + 2];
            }
        }

        var minInk = Math.Max(2, width / 50);
        var raw = new List<RowBand>();
        var y0 = -1;
        for (var y = 0; y < height; y++)
        {
            var isText = closed[y] >= minInk;
            if (isText && y0 < 0)
            {
                y0 = y;
            }
            else if (!isText && y0 >= 0)
            {
                var bandHeight = y - y0;
                if (bandHeight >= 3)
                {
                    raw.Add(new RowBand(y0, bandHeight));
                }

                y0 = -1;
            }
        }

        if (y0 >= 0)
        {
            var bandHeight = height - y0;
            if (bandHeight >= 3)
            {
                raw.Add(new RowBand(y0, bandHeight));
            }
        }

        if (raw.Count == 0)
        {
            return raw;
        }

        // Merge bands with small gaps relative to typical text height.
        var medianHeight = raw.Select(b => b.Height).OrderBy(h => h).ElementAt(raw.Count / 2);
        var maxGap = Math.Max(3, medianHeight / 3);

        var merged = new List<RowBand>();
        var current = raw[0];
        for (var i = 1; i < raw.Count; i++)
        {
            var next = raw[i];
            var gap = next.Y - (current.Y + current.Height);
            if (gap <= maxGap)
            {
                var bottom = next.Y + next.Height;
                current = new RowBand(current.Y, bottom - current.Y);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);

        // Drop bands that are tiny compared to median (half-glyph slices).
        var finalMedian = merged.Select(b => b.Height).OrderBy(h => h).ElementAt(merged.Count / 2);
        var minHeight = Math.Max(4, (int)(finalMedian * 0.45));
        merged = merged.Where(b => b.Height >= minHeight).ToList();

        // Add 1px margin.
        for (var i = 0; i < merged.Count; i++)
        {
            var b = merged[i];
            var top = Math.Max(0, b.Y - 1);
            var bottom = Math.Min(height, b.Y + b.Height + 1);
            merged[i] = new RowBand(top, bottom - top);
        }

        return merged;
    }

    private static byte[] Crop(byte[] gray, int width, int height, int y, int bandHeight)
    {
        var result = new byte[width * bandHeight];
        Buffer.BlockCopy(gray, y * width, result, 0, result.Length);
        return result;
    }

    private static int ChooseScale(int width, int height)
    {
        var min = Math.Min(width, height);
        if (min < 40) return 4;
        if (min < 80) return 3;
        return 2;
    }

    private static byte[] ToGrayscale(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];
        for (int i = 0, p = 0; i < gray.Length; i++, p += 4)
        {
            var b = bgra[p];
            var g = bgra[p + 1];
            var r = bgra[p + 2];
            gray[i] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
        }

        return gray;
    }

    private static double Mean(byte[] gray)
    {
        if (gray.Length == 0) return 0;
        long sum = 0;
        foreach (var v in gray) sum += v;
        return sum / (double)gray.Length;
    }

    private static byte[] Invert(byte[] gray)
    {
        var result = new byte[gray.Length];
        for (var i = 0; i < gray.Length; i++)
        {
            result[i] = (byte)(255 - gray[i]);
        }

        return result;
    }

    private static byte[] StretchContrast(byte[] gray, double pLow = 0.05, double pHigh = 0.95)
    {
        var hist = new int[256];
        foreach (var v in gray) hist[v]++;

        var total = gray.Length;
        var lowCount = (int)(total * pLow);
        var highCount = (int)(total * pHigh);

        var lo = 0;
        var hi = 255;
        var acc = 0;
        for (var i = 0; i < 256; i++)
        {
            acc += hist[i];
            if (acc >= lowCount)
            {
                lo = i;
                break;
            }
        }

        acc = 0;
        for (var i = 0; i < 256; i++)
        {
            acc += hist[i];
            if (acc >= highCount)
            {
                hi = i;
                break;
            }
        }

        if (hi <= lo)
        {
            return (byte[])gray.Clone();
        }

        var result = new byte[gray.Length];
        var scale = 255.0 / (hi - lo);
        for (var i = 0; i < gray.Length; i++)
        {
            var v = (gray[i] - lo) * scale;
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            result[i] = (byte)v;
        }

        return result;
    }

    private static byte[] Binarize(byte[] gray, byte threshold)
    {
        var result = new byte[gray.Length];
        for (var i = 0; i < gray.Length; i++)
        {
            result[i] = gray[i] >= threshold ? (byte)255 : (byte)0;
        }

        return result;
    }

    private static (int width, int height, byte[] bgra) UpscaleAndPad(byte[] gray, int width, int height, int scale, int pad)
    {
        return UpscaleAndPadFlexible(gray, width, height, scale, padX: pad, padY: pad);
    }

    private static (int width, int height, byte[] bgra) UpscaleAndPadFlexible(
        byte[] gray, int width, int height, int scale, int padX, int padY)
    {
        scale = Math.Max(1, scale);
        padX = Math.Max(0, padX);
        padY = Math.Max(0, padY);

        var scaledW = width * scale;
        var scaledH = height * scale;
        var outW = scaledW + padX * 2;
        var outH = scaledH + padY * 2;
        var bgra = new byte[outW * outH * 4];

        for (var i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = 255;
            bgra[i + 1] = 255;
            bgra[i + 2] = 255;
            bgra[i + 3] = 255;
        }

        for (var y = 0; y < scaledH; y++)
        {
            var srcY = y / scale;
            for (var x = 0; x < scaledW; x++)
            {
                var srcX = x / scale;
                var g = gray[srcY * width + srcX];
                var dstIndex = ((y + padY) * outW + (x + padX)) * 4;
                bgra[dstIndex] = g;
                bgra[dstIndex + 1] = g;
                bgra[dstIndex + 2] = g;
                bgra[dstIndex + 3] = 255;
            }
        }

        return (outW, outH, bgra);
    }
}
