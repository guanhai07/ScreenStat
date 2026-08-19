using System.Windows;
using System.Windows.Data;
using ScreenStat.App.ViewModels;

namespace ScreenStat.App.Views;

public partial class ResultWindow : Window
{
    public ResultWindow(ResultViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ConfirmDiscard = () => System.Windows.MessageBox.Show(
            this,
            "确定删除本次采集的截图和识别结果吗？此操作不可撤销。",
            "ScreenStat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}

