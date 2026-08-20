using System.Windows;
using System.Windows.Interop;
using ScreenStat.App.Services;

namespace ScreenStat.App.Infrastructure;

/// <summary>
/// Keeps a window's title bar in step with the app theme. WPF only styles the
/// client area, so the frame has to be told separately.
/// </summary>
public static class WindowThemeHelper
{
    private static SystemThemeService? _themeService;

    /// <summary>Called once at startup so later windows can theme themselves.</summary>
    public static void Initialize(SystemThemeService themeService) => _themeService = themeService;

    /// <summary>
    /// Applies the current theme to the frame and keeps applying it while the
    /// window lives. Safe to call before the handle exists.
    /// </summary>
    public static void Attach(Window window)
    {
        if (_themeService is null)
        {
            return;
        }

        void OnThemeChanged(object? sender, EventArgs e) => ApplyFrame(window);

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            ApplyFrame(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => ApplyFrame(window);
        }

        _themeService.ThemeChanged += OnThemeChanged;
        window.Closed += (_, _) => _themeService.ThemeChanged -= OnThemeChanged;
    }

    private static void ApplyFrame(Window window)
    {
        if (_themeService is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var useDark = _themeService.IsDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(
                handle,
                NativeMethods.DwmwaUseImmersiveDarkMode,
                ref useDark,
                sizeof(int));
        }
        catch
        {
            // Unsupported on older builds; the client area is still themed.
        }
    }
}
