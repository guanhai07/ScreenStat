using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;
using ScreenStat.Core.Models;

namespace ScreenStat.SmokeTests;

public class ResultViewModelManualCorrectionTests
{
    [Fact]
    public void EditingNumbers_RecalculatesStatistics()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ApplyOcrSuccess(new OcrResult
        {
            FullText = "123\n156\n98",
            Success = true
        });

        Assert.True(viewModel.HasStatistics);
        Assert.Contains("123", viewModel.NumbersText);
        Assert.Contains("98", viewModel.SummaryText);

        viewModel.NumbersText = "10\n20\n30";

        Assert.True(viewModel.HasStatistics);
        Assert.Equal("已识别 3 个数字", viewModel.StatusText);
        Assert.Contains("60", viewModel.SummaryText);
        Assert.Contains("Count     3", viewModel.SummaryText);
    }
}
