using System.IO;
using ScreenStat.App.Services;
using ScreenStat.App.ViewModels;
using ScreenStat.Core.Dataset;
using ScreenStat.Core.Models;

namespace ScreenStat.SmokeTests;

/// <summary>
/// Covers the whole review loop a collected sample goes through: recognition is
/// recorded as it happened, the user's corrections become ground truth, and the
/// origin of every row is preserved so the dataset says <em>how</em> OCR failed.
/// </summary>
public sealed class ResultViewModelDatasetTests : IDisposable
{
    private const string CaptureId = "20260819-120000-abcd";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ScreenStatViewModelDatasetTests",
        Guid.NewGuid().ToString("N"));

    private readonly string _captureDirectory;

    public ResultViewModelDatasetTests()
    {
        _captureDirectory = Path.Combine(_root, CaptureId);
        Directory.CreateDirectory(_captureDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ApplyingALayoutResult_RecordsWhatTheEngineProduced()
    {
        var viewModel = CreateAttachedViewModel();

        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocument(), TimeSpan.FromMilliseconds(1234));

        var record = DatasetStore.ReadCapture(_captureDirectory);
        Assert.NotNull(record);
        Assert.Equal(CaptureId, record!.CaptureId);
        Assert.Equal("test", record.Ocr.Engine);
        Assert.Equal(1234, record.Ocr.ElapsedMs);
        Assert.Equal(new[] { "10", "20", "30" }, record.Ocr.Regions.Select(region => region.Text));
        var column = Assert.Single(record.Columns);
        Assert.Equal(new[] { 10d, 20d, 30d }, column.Tokens.Select(token => token.Value));
        Assert.Equal(120, record.Image.Width);
        Assert.Equal("ScreenStat · 采集中", viewModel.Title);
    }

    [Fact]
    public void FailedRecognition_IsStillRecorded()
    {
        var viewModel = CreateAttachedViewModel();

        viewModel.ApplyLayoutFailure(
            OcrDocument.Failed("test", "未检测到文字区域。"),
            TimeSpan.FromMilliseconds(90));

        var record = DatasetStore.ReadCapture(_captureDirectory);
        Assert.NotNull(record);
        Assert.False(record!.Ocr.Success);
        Assert.Equal("未检测到文字区域。", record.Ocr.Error);
        Assert.Empty(record.Columns);
        Assert.Equal("识别失败", viewModel.StatusText);
    }

    [Fact]
    public void SavingLabels_KeepsCorrectionsInRowOrderWithTheirOrigin()
    {
        var viewModel = CreateAttachedViewModel();
        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocument(), TimeSpan.FromMilliseconds(500));
        var column = viewModel.Columns[0];
        var recognized = column.Items.ToArray();

        recognized[1].Text = "22";              // misread, corrected by hand
        recognized[2].IsIncluded = false;       // detected but not part of the report
        column.SelectedItem = recognized[0];
        column.InsertRowCommand.Execute(null);  // a row OCR never returned
        var inserted = column.Items[1];
        inserted.Text = "15";
        viewModel.DatasetNote = "深色主题 / 125% DPI";

        viewModel.SaveDatasetLabelsCommand.Execute(null);

        var labels = DatasetStore.ReadLabels(_captureDirectory);
        Assert.NotNull(labels);
        Assert.Equal("深色主题 / 125% DPI", labels!.Note);
        var labeled = Assert.Single(labels.Columns);
        Assert.Equal(
            new[] { LabelOrigin.Ocr, LabelOrigin.Added, LabelOrigin.Edited, LabelOrigin.Excluded },
            labeled.Rows.Select(row => row.Origin));
        Assert.Equal(new[] { 10d, 15d, 22d }, labeled.ExpectedValues);
        Assert.Null(labeled.Rows[1].Recognized);
        Assert.Equal("20", labeled.Rows[2].Recognized);
        Assert.Equal("22", labeled.Rows[2].Corrected);
        Assert.Contains(CaptureId, viewModel.DatasetStatusText);
    }

    [Fact]
    public void DiscardingACapture_DeletesItAndHidesThePanel()
    {
        var viewModel = CreateAttachedViewModel();
        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocument(), TimeSpan.FromMilliseconds(500));

        viewModel.DiscardDatasetCaptureCommand.Execute(null);

        Assert.False(Directory.Exists(_captureDirectory));
        Assert.False(viewModel.HasDatasetSession);
        Assert.Equal("ScreenStat", viewModel.Title);
    }

    [Fact]
    public void DiscardingACapture_DoesNothingWhenNotConfirmed()
    {
        var viewModel = CreateAttachedViewModel();
        viewModel.ConfirmDiscard = () => false;
        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocument(), TimeSpan.FromMilliseconds(500));

        viewModel.DiscardDatasetCaptureCommand.Execute(null);

        Assert.True(Directory.Exists(_captureDirectory));
        Assert.True(viewModel.HasDatasetSession);
    }

    [Fact]
    public void WithoutASession_NothingIsWritten()
    {
        var viewModel = new ResultViewModel(new ClipboardService());

        viewModel.ApplyLayoutSuccess(CreateSingleColumnDocument(), TimeSpan.FromMilliseconds(500));
        viewModel.SaveDatasetLabelsCommand.Execute(null);

        Assert.False(viewModel.HasDatasetSession);
        Assert.Empty(Directory.GetFiles(_captureDirectory));
    }

    private ResultViewModel CreateAttachedViewModel()
    {
        var viewModel = new ResultViewModel(new ClipboardService());
        viewModel.ShowLoading();
        viewModel.AttachDatasetSession(new DatasetCaptureSession(
            CaptureId,
            _captureDirectory,
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.FromHours(8)),
            120,
            200));
        return viewModel;
    }

    private static OcrDocument CreateSingleColumnDocument() => new()
    {
        Success = true,
        Engine = "test",
        Regions =
        [
            Region("10", 0),
            Region("20", 1),
            Region("30", 2)
        ]
    };

    private static OcrRegion Region(string text, int row) => new()
    {
        Text = text,
        Bounds = new OcrBounds(10, row * 24, 30, 18),
        Confidence = 0.97,
        Engine = "test",
        SourceOrder = row
    };
}
