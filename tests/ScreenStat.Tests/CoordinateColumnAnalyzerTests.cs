using ScreenStat.Core.Analysis;
using ScreenStat.Core.Models;

namespace ScreenStat.Tests;

public class CoordinateColumnAnalyzerTests
{
    [Fact]
    public void Analyze_RightAlignedSingleColumn_KeepsAllSixteenRows()
    {
        var tokens = Enumerable.Range(0, 16)
            .Select(index => Token(
                value: index + 1,
                left: 100 - (index % 3) * 8,
                top: index * 22,
                width: 40 + (index % 3) * 8))
            .OrderByDescending(token => token.Bounds.Top)
            .ToArray();

        var columns = new CoordinateColumnAnalyzer().Analyze(tokens);

        var column = Assert.Single(columns);
        Assert.Equal(16, column.Tokens.Count);
        Assert.Equal(Enumerable.Range(1, 16).Select(value => (double)value), column.Tokens.Select(token => token.Value));
        Assert.Equal(136, column.Statistics.Sum);
    }

    [Fact]
    public void Analyze_ThreeColumns_SeparatesValuesOnSameRows()
    {
        var tokens = new List<NumericToken>();
        for (var row = 0; row < 6; row++)
        {
            tokens.Add(Token(10 + row, 20, row * 24, 34));
            tokens.Add(Token(100 + row, 110, row * 24 + 1, 42));
            tokens.Add(Token(1000 + row, 220, row * 24 - 1, 52));
        }

        var shuffled = tokens
            .OrderByDescending(token => token.Bounds.Top)
            .ThenByDescending(token => token.Bounds.Left)
            .ToArray();
        var columns = new CoordinateColumnAnalyzer().Analyze(shuffled);

        Assert.Equal(3, columns.Count);
        Assert.Equal(new[] { 6, 6, 6 }, columns.Select(column => column.Tokens.Count));
        Assert.Equal(new[] { 10d, 11, 12, 13, 14, 15 }, columns[0].Tokens.Select(token => token.Value));
        Assert.Equal(new[] { 100d, 101, 102, 103, 104, 105 }, columns[1].Tokens.Select(token => token.Value));
        Assert.Equal(new[] { 1000d, 1001, 1002, 1003, 1004, 1005 }, columns[2].Tokens.Select(token => token.Value));
    }

    [Fact]
    public void Analyze_MissingCell_DoesNotMergeNeighborColumns()
    {
        var tokens = new List<NumericToken>();
        for (var row = 0; row < 5; row++)
        {
            tokens.Add(Token(row + 1, 20, row * 22, 35));
            if (row != 2)
            {
                tokens.Add(Token(row + 101, 105, row * 22, 44));
            }
        }

        var columns = new CoordinateColumnAnalyzer().Analyze(tokens);

        Assert.Equal(2, columns.Count);
        Assert.Equal(5, columns[0].Tokens.Count);
        Assert.Equal(4, columns[1].Tokens.Count);
    }

    [Fact]
    public void Analyze_TracksLowConfidenceCount()
    {
        var tokens = new[]
        {
            Token(1, 20, 0, 30, confidence: 0.98),
            Token(2, 20, 22, 30, confidence: 0.60)
        };

        var column = Assert.Single(new CoordinateColumnAnalyzer().Analyze(tokens));

        Assert.Equal(1, column.LowConfidenceCount);
    }

    [Fact]
    public void NumericRegionParser_ProjectsMultipleNumbersWithinRegion()
    {
        var document = new OcrDocument
        {
            Success = true,
            Engine = "test",
            Regions = new[]
            {
                new OcrRegion
                {
                    Text = "12.5 99%",
                    Bounds = new OcrBounds(10, 20, 100, 18),
                    Confidence = 0.82
                }
            }
        };

        var tokens = new NumericRegionParser().Parse(document);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(new[] { 12.5, 99d }, tokens.Select(token => token.Value));
        Assert.Equal(50, tokens[0].Bounds.Width);
        Assert.Equal(60, tokens[1].Bounds.Left);
        Assert.Equal("%", tokens[1].Unit);
    }

    [Fact]
    public void NumericRegionParser_KeepsZeroCellsReadAsLetterO()
    {
        // PP-OCRv5 has no context inside a one-glyph box, so a lone "0" cell
        // frequently comes back as "O". Those rows used to disappear entirely.
        var texts = new[] { "1200", "O", "845", "O", "1130", "0", "970", "O", "1005" };
        var document = new OcrDocument
        {
            Success = true,
            Engine = "test",
            Regions = texts
                .Select((text, index) => new OcrRegion
                {
                    Text = text,
                    Bounds = new OcrBounds(40, index * 22, 46, 16),
                    Confidence = 0.93,
                    SourceOrder = index
                })
                .ToArray()
        };

        var tokens = new NumericRegionParser().Parse(document);
        var column = Assert.Single(new CoordinateColumnAnalyzer().Analyze(tokens));

        Assert.Equal(9, column.Tokens.Count);
        Assert.Equal(
            new[] { 1200d, 0, 845, 0, 1130, 0, 970, 0, 1005 },
            column.Tokens.Select(token => token.Value));

        // Repaired cells stay visible for review; the clean "0" does not.
        Assert.Equal(3, column.Tokens.Count(token => token.IsCorrected));
        Assert.Equal(3, column.LowConfidenceCount);
        Assert.False(column.Tokens[5].IsCorrected);
    }

    private static NumericToken Token(
        double value,
        double left,
        double top,
        double width,
        double confidence = 0.95) => new()
    {
        Value = value,
        OriginalText = value.ToString("G"),
        Bounds = new OcrBounds(left, top, width, 16),
        Confidence = confidence
    };
}
