using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenStat.App.ViewModels;

public partial class RecognizedNumberViewModel : ObservableObject
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isIncluded = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReviewText))]
    private bool _hasParseError;

    public RecognizedNumberViewModel(string text, double confidence, bool isLowConfidence)
    {
        _text = text;
        Confidence = Math.Clamp(confidence, 0, 1);
        IsLowConfidence = isLowConfidence;
    }

    public double Confidence { get; }
    public bool IsLowConfidence { get; }
    public string ConfidenceText => $"{Confidence:P0}";

    public string ReviewText => HasParseError
        ? "格式无效"
        : IsLowConfidence
            ? "请复核"
            : string.Empty;
}
