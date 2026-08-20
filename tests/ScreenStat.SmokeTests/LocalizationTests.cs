using System.Globalization;
using System.Resources;
using ScreenStat.App.Resources;
using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;

namespace ScreenStat.SmokeTests;

public sealed class LocalizationTests
{
    [Fact]
    public void ResultViewModel_SpeaksTheCurrentUiLanguage()
    {
        // The whole chain in one assertion: UI culture, resx lookup, satellite
        // assembly, and the view model that formats the text.
        CultureInfo.CurrentUICulture = new CultureInfo(LocalizationService.EnglishCulture);
        var english = new ResultViewModel(new ClipboardService());
        english.ApplyFailure("boom");

        CultureInfo.CurrentUICulture = new CultureInfo(LocalizationService.SimplifiedChineseCulture);
        var chinese = new ResultViewModel(new ClipboardService());
        chinese.ApplyFailure("boom");

        Assert.Equal("Recognition failed", english.StatusText);
        Assert.Equal("识别失败", chinese.StatusText);
    }

    [Theory]
    [InlineData("zh-CN", LocalizationService.SimplifiedChineseCulture)]
    [InlineData("zh-Hans", LocalizationService.SimplifiedChineseCulture)]
    [InlineData("zh-SG", LocalizationService.SimplifiedChineseCulture)]
    // Traditional Chinese has no translation of its own, and Simplified is a
    // far better guess for those users than falling back to English.
    [InlineData("zh-TW", LocalizationService.SimplifiedChineseCulture)]
    [InlineData("en-US", LocalizationService.EnglishCulture)]
    [InlineData("de-DE", LocalizationService.EnglishCulture)]
    [InlineData("ja-JP", LocalizationService.EnglishCulture)]
    public void SystemLanguage_ResolvesAgainstTheOsCulture(string systemCulture, string expected) =>
        Assert.Equal(
            expected,
            LocalizationService.Resolve(AppLanguage.System, new CultureInfo(systemCulture)).Name);

    [Theory]
    [InlineData(AppLanguage.English, LocalizationService.EnglishCulture)]
    [InlineData(AppLanguage.SimplifiedChinese, LocalizationService.SimplifiedChineseCulture)]
    public void ExplicitLanguage_IgnoresTheOsCulture(AppLanguage language, string expected)
    {
        // A user who picked a language keeps it whatever Windows says.
        Assert.Equal(expected, LocalizationService.Resolve(language, new CultureInfo("de-DE")).Name);
        Assert.Equal(expected, LocalizationService.Resolve(language, new CultureInfo("zh-CN")).Name);
    }

    [Fact]
    public void Strings_AreTranslatedForBothCultures()
    {
        var english = Strings.ResourceManager.GetString(
            nameof(Strings.ResultRecognitionFailed),
            new CultureInfo(LocalizationService.EnglishCulture));
        var chinese = Strings.ResourceManager.GetString(
            nameof(Strings.ResultRecognitionFailed),
            new CultureInfo(LocalizationService.SimplifiedChineseCulture));

        Assert.Equal("Recognition failed", english);
        Assert.Equal("识别失败", chinese);
    }

    [Fact]
    public void EveryKey_HasASimplifiedChineseTranslation()
    {
        // A key missing from the satellite falls back to English with no error,
        // so a half-translated build looks fine until someone reads it.
        var neutral = LoadKeys(CultureInfo.InvariantCulture);
        var chinese = LoadKeys(new CultureInfo(LocalizationService.SimplifiedChineseCulture));

        Assert.NotEmpty(neutral);
        var missing = neutral.Except(chinese).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "缺少中文翻译：" + string.Join(", ", missing));

        var extra = chinese.Except(neutral).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Assert.True(extra.Length == 0, "中文资源里有多余的键：" + string.Join(", ", extra));
    }

    /// <summary>
    /// Reads the keys actually present in one culture's resource set, without
    /// the parent-culture fallback that would otherwise hide a missing entry.
    /// </summary>
    private static HashSet<string> LoadKeys(CultureInfo culture)
    {
        var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in set!)
        {
            keys.Add((string)entry.Key);
        }

        return keys;
    }
}
