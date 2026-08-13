using System.Windows;

namespace ScreenStat.App.Infrastructure;

public readonly record struct ScreenRegion(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static ScreenRegion FromPoints(NativeMethods.Point a, NativeMethods.Point b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X, b.X);
        var bottom = Math.Max(a.Y, b.Y);
        return new ScreenRegion(left, top, right - left, bottom - top);
    }

    public Rect ToDipRect(double scaleX, double scaleY, double originXDip, double originYDip)
    {
        return new Rect(
            (X / scaleX) - originXDip,
            (Y / scaleY) - originYDip,
            Width / scaleX,
            Height / scaleY);
    }
}

public sealed class MonitorInfo
{
    public required NativeMethods.Rect PixelBounds { get; init; }
    public required double ScaleX { get; init; }
    public required double ScaleY { get; init; }
    public required bool IsPrimary { get; init; }
    public required string DeviceName { get; init; }
}

public static class VirtualScreenHelper
{
    public static ScreenRegion GetVirtualScreenPixels()
    {
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);
        return new ScreenRegion(x, y, width, height);
    }

    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.Rect rect, IntPtr data) =>
            {
                var info = new NativeMethods.MonitorInfoEx();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>();
                if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    return true;
                }

                double scaleX = 1.0;
                double scaleY = 1.0;
                if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MonitorDpiType.MdtEffectiveDpi, out var dpiX, out var dpiY) == 0)
                {
                    scaleX = dpiX / 96.0;
                    scaleY = dpiY / 96.0;
                }

                list.Add(new MonitorInfo
                {
                    PixelBounds = info.rcMonitor,
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    IsPrimary = (info.dwFlags & NativeMethods.MonitorInfoFPrimary) != 0,
                    DeviceName = info.szDevice
                });
                return true;
            }, IntPtr.Zero);

        return list;
    }
}

