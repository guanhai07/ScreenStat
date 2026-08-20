using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Windows.UI.ViewManagement;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;

namespace ScreenStat.App.Services;

/// <summary>
/// Keeps the app on the same theme and accent colour as Windows. Swaps the
/// token dictionary in place rather than rebuilding any UI, which is why every
/// style refers to its brushes with DynamicResource.
/// </summary>
public sealed class SystemThemeService : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private readonly WpfApplication _application;
    private UISettings? _uiSettings;
    private bool _disposed;

    public SystemThemeService(WpfApplication application)
    {
        _application = application;
        try
        {
            _uiSettings = new UISettings();
        }
        catch
        {
            // Falls back to the palette defaults baked into the token files.
            _uiSettings = null;
        }

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool IsDark { get; private set; }

    /// <summary>Raised after the resources have been swapped, on the UI thread.</summary>
    public event EventHandler? ThemeChanged;

    public void Apply()
    {
        var isDark = ReadIsSystemDark();
        var tokensUri = new Uri(
            isDark
                ? "pack://application:,,,/Themes/Tokens.Dark.xaml"
                : "pack://application:,,,/Themes/Tokens.Light.xaml",
            UriKind.Absolute);

        IsDark = isDark;

        // The token dictionary is always merged first; Controls.xaml follows and
        // must stay put, so replace by index instead of clearing.
        var merged = _application.Resources.MergedDictionaries;
        var tokens = new ResourceDictionary { Source = tokensUri };
        if (merged.Count == 0)
        {
            merged.Add(tokens);
        }
        else
        {
            merged[0] = tokens;
        }

        ApplyAccent(isDark);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _uiSettings = null;
    }

    /// <summary>
    /// Takes the accent straight from the system. Windows publishes ready-made
    /// light and dark variants of the user's accent, so there is no need to
    /// lighten or darken it by hand: dark mode wants AccentLight2, light mode
    /// AccentDark1, which is what WinUI itself uses.
    /// </summary>
    private void ApplyAccent(bool isDark)
    {
        if (_uiSettings is null)
        {
            return;
        }

        try
        {
            var accent = _uiSettings.GetColorValue(isDark ? UIColorType.AccentLight2 : UIColorType.AccentDark1);
            var color = WpfColor.FromArgb(accent.A, accent.R, accent.G, accent.B);

            // Set on Application.Resources directly: those win over merged
            // dictionaries, so this survives the next token swap being merged in.
            _application.Resources["AccentFillColorDefaultBrush"] = Frozen(color, 255);
            _application.Resources["AccentFillColorSecondaryBrush"] = Frozen(color, 230);
            _application.Resources["AccentFillColorTertiaryBrush"] = Frozen(color, 204);
            _application.Resources["AccentFillColorSelectionBrush"] = Frozen(color, 51);
            _application.Resources["TextOnAccentFillColorPrimaryBrush"] =
                Frozen(IsLight(color) ? Colors.Black : Colors.White, 255);
        }
        catch
        {
            // Keep the palette defaults.
        }
    }

    private static SolidColorBrush Frozen(WpfColor color, byte alpha)
    {
        var brush = new SolidColorBrush(WpfColor.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>Rec. 601 luma, the usual test for which ink an accent needs.</summary>
    private static bool IsLight(WpfColor color) =>
        (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) > 150;

    private static bool ReadIsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            // The value is absent on editions where personalization is locked
            // down; Windows treats that as light.
            return key?.GetValue(AppsUseLightThemeValue) is int useLight && useLight == 0;
        }
        catch
        {
            return false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        // Fires on a system thread, and repeatedly for one theme switch.
        _application.Dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                Apply();
            }
        });
    }
}
