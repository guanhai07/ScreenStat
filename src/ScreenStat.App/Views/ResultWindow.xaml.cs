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
    }
}

