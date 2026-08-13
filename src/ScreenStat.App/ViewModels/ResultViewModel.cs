using System.Text;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenStat.App.Services;
using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;
using ScreenStat.Core.Statistics;

namespace ScreenStat.App.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly ClipboardService _clipboardService;
    private readonly NumberParser _numberParser = new();

    [ObservableProperty] private string _title = "ScreenStat";
    [ObservableProperty] private string _statusText = "正在识别...";
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private bool _hasStatistics;
    [ObservableProperty] private string _ocrText = string.Empty;
    [ObservableProperty] private string _numbersText = string.Empty;
    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private string? _errorText;

    private StatisticsResult _statistics = StatisticsResult.Empty;
    private IReadOnlyList<NumberValue> _numbers = Array.Empty<NumberValue>();

    public ResultViewModel(ClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public void ShowLoading()
    {
        IsBusy = true;
        HasStatistics = false;
        StatusText = "正在识别...";
        ErrorText = null;
        SummaryText = string.Empty;
        NumbersText = string.Empty;
        OcrText = string.Empty;
    }

    public void ApplyOcrSuccess(OcrResult ocr)
    {
        OcrText = ocr.FullText ?? string.Empty;
        _numbers = _numberParser.Parse(OcrText);
        _statistics = StatisticsCalculator.Calculate(_numbers);
        NumbersText = string.Join(Environment.NewLine, _numbers.Select(n => n.OriginalText ?? n.Value.ToString("G")));
        SummaryText = BuildSummary(_statistics, _numbers);
        IsBusy = false;

        if (_numbers.Count == 0)
        {
            HasStatistics = false;
            StatusText = "未识别到数字";
            ErrorText = string.IsNullOrWhiteSpace(OcrText)
                ? "OCR 未返回文本。"
                : "OCR 成功，但没有提取到可统计的数字。可查看 OCR 原文。";
        }
        else
        {
            HasStatistics = true;
            StatusText = $"已识别 {_numbers.Count} 个数字";
            ErrorText = null;
        }
    }

    public void ApplyFailure(string message)
    {
        IsBusy = false;
        HasStatistics = false;
        StatusText = "识别失败";
        ErrorText = message;
    }

    [RelayCommand]
    private void CopyStatistics()
    {
        if (!HasStatistics)
        {
            return;
        }

        _clipboardService.SetText(SummaryText);
    }

    [RelayCommand]
    private void CopyNumbers()
    {
        if (_numbers.Count == 0)
        {
            return;
        }

        _clipboardService.SetText(NumbersText);
    }

    [RelayCommand]
    private void CopyOcrText()
    {
        if (string.IsNullOrWhiteSpace(OcrText))
        {
            return;
        }

        _clipboardService.SetText(OcrText);
    }

    private static string BuildSummary(StatisticsResult stats, IReadOnlyList<NumberValue> numbers)
    {
        var unit = InferCommonUnit(numbers);
        var unitSuffix = string.IsNullOrEmpty(unit) ? string.Empty : " " + unit;

        var sb = new StringBuilder();
        sb.AppendLine($"Count     {stats.Count}");
        sb.AppendLine($"Sum       {Format(stats.Sum)}{unitSuffix}");
        sb.AppendLine($"Average   {Format(stats.Average)}{unitSuffix}");
        sb.AppendLine($"Min       {Format(stats.Min)}{unitSuffix}");
        sb.AppendLine($"Max       {Format(stats.Max)}{unitSuffix}");
        sb.AppendLine($"Median    {Format(stats.Median)}{unitSuffix}");
        sb.AppendLine($"P90       {Format(stats.P90)}{unitSuffix}");
        sb.AppendLine($"P95       {Format(stats.P95)}{unitSuffix}");
        sb.Append($"P99       {Format(stats.P99)}{unitSuffix}");
        return sb.ToString();
    }

    private static string? InferCommonUnit(IReadOnlyList<NumberValue> numbers)
    {
        var units = numbers.Select(n => n.Unit).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return units.Count == 1 ? units[0] : null;
    }

    private static string Format(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0000001)
        {
            return ((long)Math.Round(value)).ToString();
        }

        return value.ToString("0.##");
    }
}

