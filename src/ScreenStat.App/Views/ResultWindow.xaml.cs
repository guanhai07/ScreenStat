using System.Windows;
using ScreenStat.App.Infrastructure;
using ScreenStat.App.ViewModels;

namespace ScreenStat.App.Views;

public partial class ResultWindow : Window
{
    private readonly ResultViewModel _viewModel;

    public ResultWindow(ResultViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WindowThemeHelper.Attach(this);

        viewModel.ConfirmDiscard = () => System.Windows.MessageBox.Show(
            this,
            "确定删除本次采集的截图和识别结果吗？此操作不可撤销。",
            "ScreenStat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void OnShowOcrTextClick(object sender, RoutedEventArgs e)
    {
        // Topmost result window would otherwise cover its own dialog.
        var wasTopmost = Topmost;
        Topmost = false;
        try
        {
            new OcrTextWindow(_viewModel) { Owner = this }.ShowDialog();
        }
        finally
        {
            Topmost = wasTopmost;
        }
    }
}
