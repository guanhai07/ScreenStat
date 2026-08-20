using System.Windows;
using ScreenStat.App.Infrastructure;
using ScreenStat.App.ViewModels;

namespace ScreenStat.App.Views;

/// <summary>
/// Shows the raw recognizer output on demand. It used to live in an expander at
/// the bottom of the result window, where it competed for space with the
/// numbers people actually came for.
/// </summary>
public partial class OcrTextWindow : Window
{
    public OcrTextWindow(ResultViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        WindowThemeHelper.Attach(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
