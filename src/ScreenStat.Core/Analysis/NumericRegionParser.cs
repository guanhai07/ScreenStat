using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;

namespace ScreenStat.Core.Analysis;

public sealed class NumericRegionParser
{
    private readonly NumberParser _numberParser = new();

    public IReadOnlyList<NumericToken> Parse(OcrDocument document)
    {
        if (!document.Success || document.Regions.Count == 0)
        {
            return Array.Empty<NumericToken>();
        }

        var tokens = new List<NumericToken>();
        foreach (var region in document.Regions)
        {
            var numbers = _numberParser.Parse(region.Text);
            for (var index = 0; index < numbers.Count; index++)
            {
                var number = numbers[index];
                tokens.Add(new NumericToken
                {
                    Value = number.Value,
                    OriginalText = number.OriginalText ?? number.Value.ToString("G"),
                    Unit = number.Unit,
                    Bounds = ProjectBounds(region.Bounds, index, numbers.Count),
                    Confidence = Math.Clamp(region.Confidence, 0, 1),
                    SourceOrder = region.SourceOrder
                });
            }
        }

        return tokens;
    }

    private static OcrBounds ProjectBounds(OcrBounds bounds, int index, int count)
    {
        if (count <= 1)
        {
            return bounds;
        }

        var width = bounds.Width / count;
        return new OcrBounds(bounds.Left + width * index, bounds.Top, width, bounds.Height);
    }
}
