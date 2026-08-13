using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenStat.App.Services;

internal sealed class ScreenCaptureService
{
    public BitmapSource Capture(ScreenStat.App.Infrastructure.ScreenRegion region)
    {
        if (region.IsEmpty)
        {
            throw new ArgumentException("Capture region is empty.", nameof(region));
        }

        var hdcScreen = Infrastructure.NativeMethods.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get screen DC.");
        }

        var hdcMem = Infrastructure.NativeMethods.CreateCompatibleDC(hdcScreen);
        var hBitmap = Infrastructure.NativeMethods.CreateCompatibleBitmap(hdcScreen, region.Width, region.Height);
        var old = Infrastructure.NativeMethods.SelectObject(hdcMem, hBitmap);

        try
        {
            var ok = Infrastructure.NativeMethods.BitBlt(
                hdcMem, 0, 0, region.Width, region.Height,
                hdcScreen, region.X, region.Y,
                Infrastructure.NativeMethods.SrcCopy);

            if (!ok)
            {
                throw new InvalidOperationException("BitBlt failed while capturing the screen.");
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            Infrastructure.NativeMethods.SelectObject(hdcMem, old);
            Infrastructure.NativeMethods.DeleteObject(hBitmap);
            Infrastructure.NativeMethods.DeleteDC(hdcMem);
            Infrastructure.NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    public static byte[] ToBgra32Pixels(BitmapSource source, out int width, out int height)
    {
        BitmapSource converted = source;
        if (source.Format != PixelFormats.Bgra32)
        {
            converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        }

        width = converted.PixelWidth;
        height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}

