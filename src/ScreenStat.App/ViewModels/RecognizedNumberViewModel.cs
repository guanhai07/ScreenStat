using CommunityToolkit.Mvvm.ComponentModel;
using ScreenStat.App.Resources;
using ScreenStat.Core.Dataset;

namespace ScreenStat.App.ViewModels;

public partial class RecognizedNumberViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OriginText))]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OriginText))]
    private bool _isIncluded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReviewText))]
    private bool _hasParseError;

    public RecognizedNumberViewModel(string text, double confidence, bool isLowConfidence)
    {
        _text = text;
        RecognizedText = text;
        Confidence = Math.Clamp(confidence, 0, 1);
        IsLowConfidence = isLowConfidence;
    }

    private RecognizedNumberViewModel()
    {
        _text = string.Empty;
        RecognizedText = null;
        IsAdded = true;
    }

    /// <summary>
    /// A row for a cell OCR never returned. The value is typed by the user, so
    /// there is no confidence to report, but it still has to be reviewed before
    /// it counts.
    /// </summary>
    public static RecognizedNumberViewModel CreateAdded() => new();

    /// <summary>What OCR returned, kept even after <see cref="Text"/> is edited. Null for added rows.</summary>
    public string? RecognizedText { get; }

    public bool IsAdded { get; }
    public double Confidence { get; }
    public bool IsLowConfidence { get; }
    public string ConfidenceText => IsAdded ? "—" : $"{Confidence:P0}";

    /// <summary>
    /// Value parsed from <see cref="Text"/> on the last recalculation, or null
    /// when the row does not hold a single number. Filled in by
    /// <see cref="ResultColumnViewModel"/> so labels record exactly the value
    /// the statistics used.
    /// </summary>
    internal double? ParsedValue { get; set; }

    public bool IsEdited => !IsAdded && !string.Equals(Text, RecognizedText, StringComparison.Ordinal);

    public string OriginText => Origin switch
    {
        LabelOrigin.Added => Strings.RowAdded,
        LabelOrigin.Excluded => Strings.RowExcluded,
        LabelOrigin.Edited => Strings.RowEdited,
        _ => string.Empty
    };

    public string ReviewText => HasParseError
        ? Strings.RowMalformed
        : IsLowConfidence
            ? Strings.RowReview
            : string.Empty;

    internal LabelOrigin Origin
    {
        get
        {
            if (IsAdded)
            {
                return LabelOrigin.Added;
            }

            if (!IsIncluded)
            {
                return LabelOrigin.Excluded;
            }

            return IsEdited ? LabelOrigin.Edited : LabelOrigin.Ocr;
        }
    }

    internal LabeledRow ToLabel() => new()
    {
        Recognized = RecognizedText,
        Corrected = Text,
        Value = ParsedValue,
        Included = IsIncluded,
        Origin = Origin
    };
}
