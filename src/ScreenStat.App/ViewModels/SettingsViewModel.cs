using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenStat.App.Resources;
using ScreenStat.App.Services;

namespace ScreenStat.App.ViewModels;

/// <summary>One entry in the language picker.</summary>
public sealed record LanguageOption(AppLanguage Value, string Display);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;

    /// <summary>
    /// Registers a hotkey and reports whether Windows accepted it. Passed in
    /// rather than taking HotkeyService directly, so the decision logic here can
    /// be tested without touching the real global hotkey table.
    /// </summary>
    private readonly Func<HotkeyDefinition, bool> _tryApplyHotkey;

    [ObservableProperty] private HotkeyDefinition _hotkey;
    [ObservableProperty] private bool _isCapturingHotkey;
    [ObservableProperty] private AppLanguage _language;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _isMessageError;

    public SettingsViewModel(AppSettingsService settings, Func<HotkeyDefinition, bool> tryApplyHotkey, bool hotkeyIsActive)
    {
        _settings = settings;
        _tryApplyHotkey = tryApplyHotkey;
        _hotkey = settings.Hotkey;
        _language = settings.Language;

        Languages =
        [
            new LanguageOption(AppLanguage.System, Strings.SettingsLanguageSystem),
            // Language names stay in their own language, as everywhere else.
            new LanguageOption(AppLanguage.SimplifiedChinese, "简体中文"),
            new LanguageOption(AppLanguage.English, "English")
        ];

        if (!hotkeyIsActive)
        {
            Message = Strings.SettingsHotkeyInactive;
            IsMessageError = true;
        }
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public string HotkeyText => IsCapturingHotkey ? Strings.SettingsHotkeyPressPrompt : Hotkey.ToString();

    /// <summary>Raised when the dialog should close because the changes were saved.</summary>
    public event EventHandler? Saved;

    partial void OnHotkeyChanged(HotkeyDefinition value) => OnPropertyChanged(nameof(HotkeyText));

    partial void OnIsCapturingHotkeyChanged(bool value) => OnPropertyChanged(nameof(HotkeyText));

    /// <summary>
    /// Takes a combination pressed while the hotkey box has focus. Rejects
    /// anything Windows would accept but the user would regret — a bare letter
    /// would be swallowed system-wide.
    /// </summary>
    public void CaptureHotkey(ModifierKeys modifiers, Key key)
    {
        var candidate = new HotkeyDefinition(modifiers, key);
        if (!candidate.IsValid)
        {
            // Modifier-only presses are the normal way through a combination,
            // so keep waiting rather than complaining about each one.
            if (modifiers != ModifierKeys.None)
            {
                return;
            }

            Message = Strings.SettingsHotkeyNeedsModifier;
            IsMessageError = true;
            return;
        }

        Hotkey = candidate;
        IsCapturingHotkey = false;
        Message = null;
        IsMessageError = false;
    }

    [RelayCommand]
    private void Save()
    {
        if (_settings.Hotkey != Hotkey && !_tryApplyHotkey(Hotkey))
        {
            // Stay open with the old hotkey still working, rather than
            // reporting success for something that did not take.
            Message = string.Format(Strings.SettingsHotkeyTaken, Hotkey);
            IsMessageError = true;
            Hotkey = _settings.Hotkey;
            return;
        }

        _settings.Hotkey = Hotkey;
        _settings.Language = Language;
        LocalizationService.Apply(Language);
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
