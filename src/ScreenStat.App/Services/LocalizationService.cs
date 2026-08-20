using System.Globalization;

namespace ScreenStat.App.Services;

/// <summary>
/// The languages the UI ships in. <see cref="AppLanguage.System"/> is not a
/// language of its own — it defers to whatever Windows is set to.
/// </summary>
public enum AppLanguage
{
    System,
    English,
    SimplifiedChinese
}

/// <summary>
/// Decides which culture the UI runs in and applies it.
///
/// Nothing else is needed to make a language switch take effect: the generated
/// resource accessors read CultureInfo.CurrentUICulture on every call, and a
/// result window is built fresh for each capture, so the next window picks up
/// the new language on its own. Windows already on screen keep the old one,
/// which is the agreed behaviour.
/// </summary>
public static class LocalizationService
{
    public const string EnglishCulture = "en";
    public const string SimplifiedChineseCulture = "zh-Hans";

    /// <summary>
    /// Maps a stored preference onto a culture. <see cref="AppLanguage.System"/>
    /// resolves against the OS language: any Chinese variant — zh-CN, zh-SG,
    /// even zh-TW — gets Simplified Chinese, since that is the only Chinese
    /// translation available and it beats falling back to English. Everything
    /// else gets English.
    /// </summary>
    public static CultureInfo Resolve(AppLanguage language, CultureInfo systemCulture) => language switch
    {
        AppLanguage.English => new CultureInfo(EnglishCulture),
        AppLanguage.SimplifiedChinese => new CultureInfo(SimplifiedChineseCulture),
        _ => IsChinese(systemCulture)
            ? new CultureInfo(SimplifiedChineseCulture)
            : new CultureInfo(EnglishCulture)
    };

    public static void Apply(AppLanguage language)
    {
        var culture = Resolve(language, CultureInfo.InstalledUICulture);

        // DefaultThreadCurrentUICulture covers threads created later; the
        // current thread has to be set explicitly as well.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static bool IsChinese(CultureInfo culture)
    {
        for (var candidate = culture; !string.IsNullOrEmpty(candidate.Name); candidate = candidate.Parent)
        {
            if (candidate.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
