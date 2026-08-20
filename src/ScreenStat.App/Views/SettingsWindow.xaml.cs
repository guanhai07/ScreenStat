using System.Windows;
using System.Windows.Input;
using ScreenStat.App.Infrastructure;
using ScreenStat.App.ViewModels;

namespace ScreenStat.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WindowThemeHelper.Attach(this);

        viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void OnHotkeyBoxClick(object sender, MouseButtonEventArgs e)
    {
        HotkeyBox.Focus();
        e.Handled = true;
    }

    private void OnHotkeyBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.IsCapturingHotkey = true;

    private void OnHotkeyBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.IsCapturingHotkey = false;

    private void OnHotkeyBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Escape and Tab have to keep working, or the box becomes a trap: with
        // every key swallowed there would be no way out with the keyboard.
        if (e.Key is Key.Escape or Key.Tab)
        {
            return;
        }

        e.Handled = true;

        // A dead key reports itself as ImeProcessed or System; the real key is
        // in SystemKey, which is also where Alt combinations land.
        var key = e.Key is Key.System or Key.ImeProcessed ? e.SystemKey : e.Key;
        _viewModel.CaptureHotkey(Keyboard.Modifiers, key);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
