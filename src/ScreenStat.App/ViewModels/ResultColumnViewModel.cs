using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenStat.Core.Dataset;
using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;
using ScreenStat.Core.Statistics;

namespace ScreenStat.App.ViewModels;

public partial class ResultColumnViewModel : ObservableObject
{
    private readonly NumberParser _parser = new();
    private IReadOnlyList<NumberValue> _includedNumbers = Array.Empty<NumberValue>();

    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _hasStatistics;
    [ObservableProperty] private RecognizedNumberViewModel? _selectedItem;

    /// <summary>
    /// The headline figures, shown as a row of stat tiles. Replaced wholesale on
    /// every recalculation so the bound ItemsControl refreshes.
    /// </summary>
    [ObservableProperty] private IReadOnlyList<StatItem> _primaryStats = Array.Empty<StatItem>();

    /// <summary>The spread, shown compactly under the tiles.</summary>
    [ObservableProperty] private IReadOnlyList<StatItem> _secondaryStats = Array.Empty<StatItem>();

    public ResultColumnViewModel(NumericColumn column)
    {
        Index = column.Index;
        Header = $"第 {Index} 列";
        Items = new ObservableCollection<RecognizedNumberViewModel>(
            column.Tokens.Select(token => new RecognizedNumberViewModel(
                token.OriginalText,
                token.Confidence,
                token.IsLowConfidence)));

        foreach (var item in Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        Recalculate();
    }

    public int Index { get; }
    public string Header { get; }
    public ObservableCollection<RecognizedNumberViewModel> Items { get; }
    public int LowConfidenceCount => Items.Count(item => item.IsLowConfidence);
    public int IncludedCount => _includedNumbers.Count;
    public IReadOnlyList<NumberValue> IncludedNumbers => _includedNumbers;

    public event EventHandler? Changed;

    /// <summary>
    /// Adds an empty row for a cell OCR missed entirely. It goes after the
    /// selected row rather than at the end, because a dataset row only means
    /// something if it sits in the right position in the column.
    /// </summary>
    [RelayCommand]
    private void InsertRow()
    {
        var row = RecognizedNumberViewModel.CreateAdded();
        var selectedIndex = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        var insertAt = selectedIndex < 0 ? Items.Count : selectedIndex + 1;

        row.PropertyChanged += OnItemPropertyChanged;
        Items.Insert(insertAt, row);
        SelectedItem = row;
        Recalculate();
    }

    internal LabeledColumn ToLabel() => new()
    {
        Index = Index,
        Rows = Items.Select(item => item.ToLabel()).ToArray()
    };

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecognizedNumberViewModel.Text) or nameof(RecognizedNumberViewModel.IsIncluded))
        {
            Recalculate();
        }
    }

    private void Recalculate()
    {
        var included = new List<NumberValue>();
        foreach (var item in Items)
        {
            // Each row holds one cell, so the same look-alike repair the region
            // parser applies is valid for manual edits too.
            var parsed = _parser.Parse(item.Text, isolatedToken: true);
            item.HasParseError = parsed.Count != 1;
            item.ParsedValue = parsed.Count == 1 ? parsed[0].Value : null;
            if (item.IsIncluded && parsed.Count == 1)
            {
                included.Add(parsed[0]);
            }
        }

        _includedNumbers = included;
        OnPropertyChanged(nameof(IncludedCount));

        if (included.Count == 0)
        {
            HasStatistics = false;
            SummaryText = string.Empty;
            PrimaryStats = Array.Empty<StatItem>();
            SecondaryStats = Array.Empty<StatItem>();
            StatusText = "本列没有启用的有效数字";
        }
        else
        {
            var statistics = StatisticsCalculator.Calculate(included);
            var unit = InferCommonUnit(included);
            HasStatistics = true;
            SummaryText = BuildSummary(statistics, included);
            PrimaryStats = BuildPrimaryStats(statistics, unit);
            SecondaryStats = BuildSecondaryStats(statistics, unit);
            var invalidCount = Items.Count(item => item.HasParseError);
            StatusText = invalidCount == 0
                ? $"使用 {included.Count} 个数字"
                : $"使用 {included.Count} 个数字，{invalidCount} 项格式无效";
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<StatItem> BuildPrimaryStats(StatisticsResult statistics, string? unit) =>
    [
        // Count is a cardinality, so it never carries the column's unit.
        new StatItem("Count", statistics.Count.ToString()),
        new StatItem("Sum", WithUnit(Format(statistics.Sum), unit)),
        new StatItem("Average", WithUnit(Format(statistics.Average), unit)),
        new StatItem("Median", WithUnit(Format(statistics.Median), unit))
    ];

    private static IReadOnlyList<StatItem> BuildSecondaryStats(StatisticsResult statistics, string? unit) =>
    [
        new StatItem("Min", WithUnit(Format(statistics.Min), unit)),
        new StatItem("Max", WithUnit(Format(statistics.Max), unit)),
        new StatItem("P90", WithUnit(Format(statistics.P90), unit)),
        new StatItem("P95", WithUnit(Format(statistics.P95), unit)),
        new StatItem("P99", WithUnit(Format(statistics.P99), unit))
    ];

    private static string WithUnit(string value, string? unit) =>
        string.IsNullOrEmpty(unit) ? value : $"{value} {unit}";

    internal static string BuildSummary(StatisticsResult statistics, IReadOnlyList<NumberValue> numbers)
    {
        var unit = InferCommonUnit(numbers);
        var unitSuffix = string.IsNullOrEmpty(unit) ? string.Empty : " " + unit;
        var builder = new StringBuilder();
        builder.AppendLine($"Count     {statistics.Count}");
        builder.AppendLine($"Sum       {Format(statistics.Sum)}{unitSuffix}");
        builder.AppendLine($"Average   {Format(statistics.Average)}{unitSuffix}");
        builder.AppendLine($"Min       {Format(statistics.Min)}{unitSuffix}");
        builder.AppendLine($"Max       {Format(statistics.Max)}{unitSuffix}");
        builder.AppendLine($"Median    {Format(statistics.Median)}{unitSuffix}");
        builder.AppendLine($"P90       {Format(statistics.P90)}{unitSuffix}");
        builder.AppendLine($"P95       {Format(statistics.P95)}{unitSuffix}");
        builder.Append($"P99       {Format(statistics.P99)}{unitSuffix}");
        return builder.ToString();
    }

    private static string? InferCommonUnit(IReadOnlyList<NumberValue> numbers)
    {
        var units = numbers
            .Select(number => number.Unit)
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
