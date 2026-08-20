using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;
using ScreenStat.Core.Models;

namespace ScreenStat.SmokeTests;

public sealed class ResultViewModelLayoutTests
{
    [Fact]
    public void LayoutResult_SeparatesColumnsAndCalculatesEachColumn()
    {
        var viewModel = new ResultViewModel(new ClipboardService());

        viewModel.ApplyLayoutSuccess(CreateTwoColumnDocument());

        Assert.Equal(2, viewModel.Columns.Count);
        Assert.Equal(new[] { "10", "20", "30" }, viewModel.Columns[0].Items.Select(item => item.Text));
        Assert.Contains("Sum       60", viewModel.Columns[0].SummaryText);
        Assert.Contains("Sum       600", viewModel.Columns[1].SummaryText);
        Assert.Contains("10\t100", viewModel.NumbersText);
        Assert.Equal("已识别 2 列，共 6 个数字，1 项请复核", viewModel.StatusText);
    }

    [Fact]
    public void ExcludingLowConfidenceItem_RecalculatesOnlyItsColumn()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ApplyLayoutSuccess(CreateTwoColumnDocument());
        var lowConfidenceItem = viewModel.Columns[0].Items[1];

        Assert.True(lowConfidenceItem.IsLowConfidence);
        lowConfidenceItem.IsIncluded = false;

        Assert.Contains("Count     2", viewModel.Columns[0].SummaryText);
        Assert.Contains("Sum       40", viewModel.Columns[0].SummaryText);
        Assert.Contains("Sum       600", viewModel.Columns[1].SummaryText);
        Assert.Equal("已识别 2 列，共 5 个数字，1 项请复核", viewModel.StatusText);
    }

    [Fact]
    public void EditingRecognizedItem_RecalculatesAndFlagsInvalidText()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ApplyLayoutSuccess(CreateTwoColumnDocument());
        var item = viewModel.Columns[1].Items[0];

        item.Text = "400";

        Assert.Contains("Sum       900", viewModel.Columns[1].SummaryText);

        item.Text = "not-a-number";

        Assert.True(item.HasParseError);
        Assert.Equal("格式无效", item.ReviewText);
        Assert.Contains("Count     2", viewModel.Columns[1].SummaryText);
        Assert.Equal("使用 2 个数字，1 项格式无效", viewModel.Columns[1].StatusText);
    }

    [Fact]
    public void LayoutResult_SelectsTheFirstColumn()
    {
        // A TabControl bound to a collection filled after binding leaves nothing
        // selected, so the window came up blank until a tab was clicked.
        var viewModel = new ResultViewModel(new ClipboardService());

        viewModel.ApplyLayoutSuccess(CreateTwoColumnDocument());

        Assert.Same(viewModel.Columns[0], viewModel.SelectedColumn);
    }

    [Fact]
    public void FailedResult_ClearsTheSelection()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ApplyLayoutSuccess(CreateTwoColumnDocument());

        viewModel.ApplyFailure("识别失败");

        Assert.Null(viewModel.SelectedColumn);
    }

    [Fact]
    public void ColumnStatistics_ArePublishedAsStatTiles()
    {
        var viewModel = new ResultViewModel(new ClipboardService());

        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocumentWithUnits());
        var column = Assert.Single(viewModel.Columns);

        Assert.Equal(
            new[] { "Count", "Sum", "Average", "Median" },
            column.PrimaryStats.Select(stat => stat.Label));
        Assert.Equal(
            new[] { "Min", "Max", "P90", "P95", "P99" },
            column.SecondaryStats.Select(stat => stat.Label));

        // The unit travels with the value, except on Count, which is a
        // cardinality rather than a measurement.
        Assert.Equal("3", column.PrimaryStats[0].Value);
        Assert.Equal("60 ms", column.PrimaryStats[1].Value);
        Assert.Equal("20 ms", column.PrimaryStats[2].Value);
    }

    [Fact]
    public void ExcludingEveryRow_EmptiesTheStatTiles()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocumentWithUnits());
        var column = viewModel.Columns[0];

        foreach (var item in column.Items)
        {
            item.IsIncluded = false;
        }

        Assert.Empty(column.PrimaryStats);
        Assert.Empty(column.SecondaryStats);
    }

    private static OcrDocument CreateSingleColumnDocumentWithUnits() => new()
    {
        Success = true,
        Engine = "test",
        Regions =
        [
            Region("10ms", 10, 0, 0.99, 0),
            Region("20ms", 10, 24, 0.99, 1),
            Region("30ms", 10, 48, 0.99, 2)
        ]
    };

    private static OcrDocument CreateTwoColumnDocument()
    {
        var regions = new List<OcrRegion>();
        var leftValues = new[] { "10", "20", "30" };
        var rightValues = new[] { "100", "200", "300" };
        for (var row = 0; row < 3; row++)
        {
            regions.Add(Region(leftValues[row], 10, row * 24, row == 1 ? 0.52 : 0.96, row * 2));
            regions.Add(Region(rightValues[row], 110, row * 24, 0.97, row * 2 + 1));
        }

        return new OcrDocument
        {
            Success = true,
            Engine = "test",
            Regions = regions
        };
    }

    private static OcrRegion Region(string text, double left, double top, double confidence, int order) => new()
    {
        Text = text,
        Bounds = new OcrBounds(left, top, 30, 18),
        Confidence = confidence,
        Engine = "test",
        SourceOrder = order
    };
}
