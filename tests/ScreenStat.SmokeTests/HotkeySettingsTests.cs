using System.Globalization;
using System.IO;
using System.Windows.Input;
using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;

namespace ScreenStat.SmokeTests;

public sealed class HotkeySettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ScreenStatHotkeyTests",
        Guid.NewGuid().ToString("N"));

    public HotkeySettingsTests() =>
        CultureInfo.CurrentUICulture = new CultureInfo(LocalizationService.EnglishCulture);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.X, true)]
    [InlineData(ModifierKeys.Alt, Key.D1, true)]
    // A bare letter registers globally and then disappears from every other
    // program, which is never what someone means to do.
    [InlineData(ModifierKeys.None, Key.X, false)]
    // A modifier on its own is not a combination.
    [InlineData(ModifierKeys.Control, Key.LeftCtrl, false)]
    [InlineData(ModifierKeys.Control, Key.None, false)]
    public void Validity_RequiresAModifierAndARealKey(ModifierKeys modifiers, Key key, bool expected) =>
        Assert.Equal(expected, new HotkeyDefinition(modifiers, key).IsValid);

    [Fact]
    public void Hotkey_RoundTripsThroughSettings()
    {
        var settings = new AppSettingsService(Path.Combine(_root, "settings.json"));
        var hotkey = new HotkeyDefinition(ModifierKeys.Control | ModifierKeys.Alt, Key.F9);

        settings.Hotkey = hotkey;
        var reloaded = new AppSettingsService(Path.Combine(_root, "settings.json"));

        Assert.Equal(hotkey, reloaded.Hotkey);
        Assert.Equal("Ctrl+Alt+F9", reloaded.Hotkey.ToString());
    }

    [Fact]
    public void Hotkey_FallsBackWhenTheStoredValueIsUnusable()
    {
        // Someone hand-edits settings.json into something that can never
        // register; starting with no working hotkey would be worse.
        Assert.Equal(HotkeyDefinition.Default, HotkeyDefinition.Parse("nonsense"));
        Assert.Equal(HotkeyDefinition.Default, HotkeyDefinition.Parse("None|X"));
        Assert.Equal(HotkeyDefinition.Default, HotkeyDefinition.Parse(null));
    }

    [Fact]
    public void Language_RoundTripsThroughSettings()
    {
        var path = Path.Combine(_root, "settings.json");
        var settings = new AppSettingsService(path);

        settings.Language = AppLanguage.English;

        Assert.Equal(AppLanguage.English, new AppSettingsService(path).Language);
    }

    [Fact]
    public void Saving_KeepsTheOldHotkeyWhenTheNewOneIsRefused()
    {
        var settings = new AppSettingsService(Path.Combine(_root, "settings.json"));
        var original = settings.Hotkey;
        var viewModel = new SettingsViewModel(settings, _ => false, hotkeyIsActive: true);
        viewModel.CaptureHotkey(ModifierKeys.Control | ModifierKeys.Alt, Key.F9);

        var saved = false;
        viewModel.Saved += (_, _) => saved = true;
        viewModel.SaveCommand.Execute(null);

        // Reporting success for a hotkey that never registered would be the
        // worst outcome here: the user would think it was applied.
        Assert.False(saved);
        Assert.True(viewModel.IsMessageError);
        Assert.Equal(original, settings.Hotkey);
        Assert.Equal(original, viewModel.Hotkey);
    }

    [Fact]
    public void Saving_PersistsAnAcceptedHotkey()
    {
        var path = Path.Combine(_root, "settings.json");
        var settings = new AppSettingsService(path);
        var viewModel = new SettingsViewModel(settings, _ => true, hotkeyIsActive: true);
        viewModel.CaptureHotkey(ModifierKeys.Control | ModifierKeys.Alt, Key.F9);

        var saved = false;
        viewModel.Saved += (_, _) => saved = true;
        viewModel.SaveCommand.Execute(null);

        Assert.True(saved);
        Assert.Equal(new HotkeyDefinition(ModifierKeys.Control | ModifierKeys.Alt, Key.F9), new AppSettingsService(path).Hotkey);
    }

    [Fact]
    public void CapturingAModifierAlone_KeepsWaiting()
    {
        var settings = new AppSettingsService(Path.Combine(_root, "settings.json"));
        var viewModel = new SettingsViewModel(settings, _ => true, hotkeyIsActive: true);
        viewModel.IsCapturingHotkey = true;

        // Ctrl is pressed on the way to Ctrl+Shift+X; complaining at that point
        // would make the box unusable.
        viewModel.CaptureHotkey(ModifierKeys.Control, Key.LeftCtrl);

        Assert.True(viewModel.IsCapturingHotkey);
        Assert.Null(viewModel.Message);
    }

    [Fact]
    public void CapturingWithoutAModifier_ExplainsWhyItWasRejected()
    {
        var settings = new AppSettingsService(Path.Combine(_root, "settings.json"));
        var viewModel = new SettingsViewModel(settings, _ => true, hotkeyIsActive: true);

        viewModel.CaptureHotkey(ModifierKeys.None, Key.X);

        Assert.True(viewModel.IsMessageError);
        Assert.Equal("The combination must include Ctrl, Alt or Shift.", viewModel.Message);
        Assert.Equal(HotkeyDefinition.Default, viewModel.Hotkey);
    }

    [Fact]
    public void OpeningSettings_WarnsWhenTheHotkeyIsNotRegistered()
    {
        var settings = new AppSettingsService(Path.Combine(_root, "settings.json"));

        var viewModel = new SettingsViewModel(settings, _ => true, hotkeyIsActive: false);

        Assert.True(viewModel.IsMessageError);
        Assert.Equal("The current hotkey is not registered. Pick another combination.", viewModel.Message);
    }
}
