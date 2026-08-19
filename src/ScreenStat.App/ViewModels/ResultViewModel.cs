using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenStat.App.Services;
using ScreenStat.Core.Analysis;
using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;
using ScreenStat.Core.Statistics;

namespace ScreenStat.App.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly ClipboardService _clipboardService;
    private readonly NumberParser _numberParser = new();
    private readonly NumericRegionParser _regionParser = new();
    private readonly CoordinateColumnAnalyzer _columnAnalyzer = new();
    private DatasetCaptureSession? _datasetSession;
    private bool _suppressNumbersRebuild;
    private bool _isLayoutResult;
    private string? _recognitionWarning;

    [ObservableProperty] private string _title = "ScreenStat";
    [ObservableProperty] private string _statusText = "正在识别...";
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private bool _hasStatistics;
    [ObservableProperty] private bool _hasColumns;
    [ObservableProperty] private string _ocrText = string.Empty;
    [ObservableProperty] private string _numbersText = string.Empty;
    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private bool _hasDatasetSession;
    [ObservableProperty] private string _datasetNote = string.Empty;
    [ObservableProperty] private string _datasetStatusText = string.Empty;

    public ResultViewModel(ClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public ObservableCollection<ResultColumnViewModel> Columns { get; } = new();

    /// <summary>
    /// Confirmation gate for discarding a capture, which deletes files. The
    /// window replaces it with a real dialog; leaving it here keeps the view
    /// model free of WPF dialogs.
    /// </summary>
    public Func<bool> ConfirmDiscard { get; set; } = () => true;

    /// <summary>
    /// Binds this result to the capture directory that was just created, so
    /// recognition output and the user's corrections end up on disk together.
    /// </summary>
    public void AttachDatasetSession(DatasetCaptureSession session)
    {
        _datasetSession = session;
        HasDatasetSession = true;
        Title = "ScreenStat · 采集中";
        DatasetStatusText = $"采集中 → {session.CaptureId}";
    }

    public void ShowLoading()
    {
        ClearColumns();
        _isLayoutResult = false;
        _recognitionWarning = null;
        IsBusy = true;
        HasStatistics = false;
        HasColumns = false;
        StatusText = "正在识别...";
        ErrorText = null;
        SummaryText = string.Empty;
        SetNumbersText(string.Empty);
        OcrText = string.Empty;
    }

    public void ApplyOcrSuccess(OcrResult ocr)
    {
        OcrText = ocr.FullText ?? string.Empty;
        _isLayoutResult = false;
        _recognitionWarning = null;
        ApplyLegacyText(OcrText);
    }

    public void ApplyLayoutSuccess(OcrDocument document) => ApplyLayoutSuccess(document, TimeSpan.Zero);

    public void ApplyLayoutSuccess(OcrDocument document, TimeSpan ocrElapsed)
    {
        OcrText = document.FullText;
        _isLayoutResult = true;
        _recognitionWarning = document.WarningMessage;

        var numericColumns = _columnAnalyzer.Analyze(_regionParser.Parse(document));
        RecordRecognition(document, numericColumns, ocrElapsed);
        if (numericColumns.Count == 0)
        {
            ClearColumns();
            IsBusy = false;
            HasColumns = false;
            HasStatistics = false;
            StatusText = "未识别到数字";
            ErrorText = string.IsNullOrWhiteSpace(OcrText)
                ? "OCR 未返回文本。"
                : "OCR 返回了文字，但没有可统计的数字。可展开查看 OCR 原文。";
            SummaryText = string.Empty;
            SetNumbersText(string.Empty);
            return;
        }

        ApplyColumns(numericColumns);
    }

    partial void OnNumbersTextChanged(string value)
    {
        if (_suppressNumbersRebuild)
        {
            return;
        }

        _isLayoutResult = false;
        _recognitionWarning = null;
        ApplyLegacyText(value);
    }

    public void ApplyFailure(string message)
    {
        ClearColumns();
        IsBusy = false;
        HasColumns = false;
        HasStatistics = false;
        StatusText = "识别失败";
        ErrorText = message;
    }

    /// <summary>
    /// Failure path that still records the capture. A screenshot the engine
    /// could not read at all is the most useful kind of sample to keep.
    /// </summary>
    public void ApplyLayoutFailure(OcrDocument document, TimeSpan ocrElapsed)
    {
        OcrText = document.FullText;
        RecordRecognition(document, Array.Empty<NumericColumn>(), ocrElapsed);
        ApplyFailure(document.ErrorMessage ?? "OCR 失败");
    }

    [RelayCommand]
    private void SaveDatasetLabels()
    {
        if (_datasetSession is null)
        {
            return;
        }

        try
        {
            _datasetSession.SaveLabels(DatasetNote, Columns.Select(column => column.ToLabel()).ToArray());
            DatasetStatusText = $"已保存标注 → {_datasetSession.CaptureId}";
        }
        catch (Exception exception)
        {
            DatasetStatusText = $"保存标注失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private void DiscardDatasetCapture()
    {
        if (_datasetSession is null || !ConfirmDiscard())
        {
            return;
        }

        try
        {
            _datasetSession.Discard();
            DatasetStatusText = $"已删除样本 {_datasetSession.CaptureId}";
            _datasetSession = null;
            HasDatasetSession = false;
            Title = "ScreenStat";
        }
        catch (Exception exception)
        {
            DatasetStatusText = $"删除样本失败：{exception.Message}";
        }
    }

    private void RecordRecognition(
        OcrDocument document,
        IReadOnlyList<NumericColumn> columns,
        TimeSpan ocrElapsed)
    {
        if (_datasetSession is null)
        {
            return;
        }

        try
        {
            _datasetSession.SaveRecognition(document, columns, ocrElapsed);
            DatasetStatusText = $"已记录识别结果 → {_datasetSession.CaptureId}";
        }
        catch (Exception exception)
        {
            DatasetStatusText = $"记录识别结果失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private void CopyStatistics()
    {
        if (HasStatistics)
        {
            _clipboardService.SetText(SummaryText);
        }
    }

    [RelayCommand]
    private void CopyNumbers()
    {
        var text = BuildTabSeparatedNumbers();
        if (!string.IsNullOrWhiteSpace(text))
        {
            _clipboardService.SetText(text);
        }
    }

    [RelayCommand]
    private void CopyOcrText()
    {
        if (!string.IsNullOrWhiteSpace(OcrText))
        {
            _clipboardService.SetText(OcrText);
        }
    }

    private void ApplyLegacyText(string? text)
    {
        var numbers = _numberParser.Parse(text ?? string.Empty);
        if (numbers.Count == 0)
        {
            ClearColumns();
            IsBusy = false;
            HasColumns = false;
            HasStatistics = false;
            SummaryText = string.Empty;
            StatusText = "未识别到数字";
            ErrorText = "请每行输入一个数字，例如：12.5 或 123ms。";
            SetNumbersText(text ?? string.Empty);
            return;
        }

        var tokens = numbers
            .Select((number, index) => new NumericToken
            {
                Value = number.Value,
                OriginalText = number.OriginalText ?? number.Value.ToString("G"),
                Unit = number.Unit,
                Bounds = new OcrBounds(0, index * 20, 60, 18),
                Confidence = 1,
                SourceOrder = index
            })
            .ToArray();
        ApplyColumns(
        [
            new NumericColumn
            {
                Index = 1,
                Tokens = tokens,
                Statistics = StatisticsCalculator.Calculate(tokens.Select(token => token.Value).ToArray())
            }
        ]);
    }

    private void ApplyColumns(IReadOnlyList<NumericColumn> numericColumns)
    {
        ClearColumns();
        foreach (var numericColumn in numericColumns)
        {
            var column = new ResultColumnViewModel(numericColumn);
            column.Changed += OnColumnChanged;
            Columns.Add(column);
        }

        IsBusy = false;
        HasColumns = Columns.Count > 0;
        RefreshAggregate();
    }

    private void OnColumnChanged(object? sender, EventArgs e) => RefreshAggregate();

    private void RefreshAggregate()
    {
        HasStatistics = Columns.Any(column => column.HasStatistics);
        var includedCount = Columns.Sum(column => column.IncludedCount);
        var lowConfidenceCount = Columns.Sum(column => column.LowConfidenceCount);

        SummaryText = string.Join(
            Environment.NewLine + Environment.NewLine,
            Columns
                .Where(column => column.HasStatistics)
                .Select(column => Columns.Count == 1
                    ? column.SummaryText
                    : $"[{column.Header}]{Environment.NewLine}{column.SummaryText}"));
        SetNumbersText(BuildTabSeparatedNumbers());

        if (_isLayoutResult)
        {
            StatusText = $"已识别 {Columns.Count} 列，共 {includedCount} 个数字";
            if (lowConfidenceCount > 0)
            {
                StatusText += $"，{lowConfidenceCount} 项请复核";
            }
        }
        else
        {
            StatusText = $"已识别 {includedCount} 个数字";
        }

        ErrorText = _recognitionWarning;
    }

    private string BuildTabSeparatedNumbers()
    {
        if (Columns.Count == 0)
        {
            return string.Empty;
        }

        var valuesByColumn = Columns
            .Select(column => column.Items
                .Where(item => item.IsIncluded && !item.HasParseError)
                .Select(item => item.Text.Trim())
                .ToArray())
            .ToArray();
        var rowCount = valuesByColumn.Max(values => values.Length);
        var builder = new StringBuilder();
        for (var row = 0; row < rowCount; row++)
        {
            if (row > 0)
            {
                builder.AppendLine();
            }

            for (var column = 0; column < valuesByColumn.Length; column++)
            {
                if (column > 0)
                {
                    builder.Append('\t');
                }

                if (row < valuesByColumn[column].Length)
                {
                    builder.Append(valuesByColumn[column][row]);
                }
            }
        }

        return builder.ToString();
    }

    private void SetNumbersText(string value)
    {
        _suppressNumbersRebuild = true;
        try
        {
            NumbersText = value;
        }
        finally
        {
            _suppressNumbersRebuild = false;
        }
    }

    private void ClearColumns()
    {
        foreach (var column in Columns)
        {
            column.Changed -= OnColumnChanged;
        }

        Columns.Clear();
    }
}
