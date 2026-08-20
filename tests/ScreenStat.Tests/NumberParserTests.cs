using ScreenStat.Core.Parsing;

namespace ScreenStat.Tests;

public class NumberParserTests
{
    private readonly NumberParser _parser = new();

    [Fact]
    public void Parse_Integers()
    {
        var values = _parser.Parse("123\n456\n789");
        Assert.Equal(new[] { 123d, 456d, 789d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_Decimals_And_Negatives()
    {
        var values = _parser.Parse("12.5\n-3.25\n0.5\n-10\n100.25");
        Assert.Equal(new[] { 12.5, -3.25, 0.5, -10d, 100.25 }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_ThousandsSeparators()
    {
        var values = _parser.Parse("1,234 12,345.67 2,000");
        Assert.Equal(new[] { 1234d, 12345.67, 2000d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_Thousands_SpaceSplit_SameLine()
    {
        var values = _parser.Parse("1 234\n12 345.67\n2 000");
        Assert.Equal(new[] { 1234d, 12345.67, 2000d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_Units_And_Percentage()
    {
        var values = _parser.Parse("123ms 1.23s 456 MB 12.5%");
        Assert.Equal(4, values.Count);
        Assert.Equal(123, values[0].Value);
        Assert.Equal("ms", values[0].Unit);
        Assert.Equal(12.5, values[3].Value);
        Assert.Equal("%", values[3].Unit);
    }

    [Fact]
    public void Parse_LogLine_WithEmbeddedNumbers()
    {
        var text = "query finished cost=123ms\nquery finished cost=156ms\nquery finished cost=98ms";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 123d, 156d, 98d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_FalseMinus_FromEquals()
    {
        var values = _parser.Parse("cost=123ms");
        Assert.Single(values);
        Assert.Equal(123, values[0].Value);
    }

    [Fact]
    public void Parse_FiltersDateTimeAndIp()
    {
        var text = "2026-08-12 10:30:25\n192.168.1.100\nlatency 42";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 42d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_FiltersNoisyDateTimeAndIp_AcrossLines()
    {
        var text = "2026\n08\n12\n10\n30\n25\n192.168.1.100\nlatency 42";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 42d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_FiltersNoisyOcrDateIp_UserLogShape()
    {
        var text = "2026\n08\n30\n25\n192\n16\n100\n42";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 42d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_FiltersPercentileLabel_P95()
    {
        var text = "平均耗时 123ms\n最大耗时 256ms\n最小耗时 45ms\nP95 200ms";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 123d, 256d, 45d, 200d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_DoesNotJoinTwoRealTwoDigitNumbers()
    {
        var text = "最小耗时 45ms\nP95 200ms";
        var values = _parser.Parse(text);
        Assert.Equal(new[] { 45d, 200d }, values.Select(v => v.Value));
        Assert.DoesNotContain(values, v => Math.Abs(v.Value - 4505) < 0.01);
    }

    [Fact]
    public void Parse_DoesNotMergeAdjacentThreeDigitStats()
    {
        var values = _parser.Parse("123\n156\n98\n231\n145");
        Assert.Equal(new[] { 123d, 156d, 98d, 231d, 145d }, values.Select(v => v.Value));
    }

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        Assert.Empty(_parser.Parse(""));
        Assert.Empty(_parser.Parse("   "));
    }

    [Theory]
    [InlineData("O", 0d)]
    [InlineData("o", 0d)]
    [InlineData("Q", 0d)]
    [InlineData("D", 0d)]
    [InlineData("O.O", 0d)]
    [InlineData("I", 1d)]
    [InlineData("l", 1d)]
    [InlineData("1O", 10d)]
    [InlineData("1OO", 100d)]
    public void Parse_IsolatedCell_RepairsStandaloneDigitLookAlikes(string text, double expected)
    {
        var values = _parser.Parse(text, isolatedToken: true);

        var value = Assert.Single(values);
        Assert.Equal(expected, value.Value);
        Assert.True(value.IsCorrected);
    }

    [Fact]
    public void Parse_IsolatedCell_DoesNotFlagCleanNumbers()
    {
        var values = _parser.Parse("12.5%", isolatedToken: true);

        var value = Assert.Single(values);
        Assert.Equal(12.5, value.Value);
        Assert.False(value.IsCorrected);
    }

    [Theory]
    [InlineData("latency")]
    [InlineData("OK")]
    [InlineData("ms")]
    [InlineData("Ill")]
    [InlineData("S")]
    [InlineData("B")]
    public void Parse_IsolatedCell_DoesNotInventNumbersFromText(string text)
    {
        Assert.Empty(_parser.Parse(text, isolatedToken: true));
    }

    [Fact]
    public void Parse_FreeText_KeepsConservativeLookAlikeRule()
    {
        // The prose path must not turn a standalone letter into a digit.
        Assert.Empty(_parser.Parse("O"));
        Assert.Equal(new[] { 42d }, _parser.Parse("latency 42").Select(v => v.Value));
    }

    [Theory]
    [InlineData("7,17%", 7.17)]
    [InlineData("7,01%", 7.01)]
    [InlineData("1,76%", 1.76)]
    [InlineData("6,2", 6.2)]
    [InlineData("7,l7%", 7.17)]
    public void Parse_IsolatedCell_ReadsACommaWithTooFewDigitsAsADecimalPoint(string text, double expected)
    {
        // A percentage column recognized against a light background regularly
        // returns the decimal point as a comma. Left alone, the thousands rule
        // rejects the group and the cell parses as two separate numbers, which
        // also drags a phantom column into the layout analysis.
        var values = _parser.Parse(text, isolatedToken: true);

        var value = Assert.Single(values);
        Assert.Equal(expected, value.Value);
        Assert.True(value.IsCorrected);
    }

    [Theory]
    [InlineData("1,234", 1234d)]
    [InlineData("12,345", 12345d)]
    [InlineData("1,234,567", 1234567d)]
    public void Parse_IsolatedCell_LeavesRealThousandsSeparatorsAlone(string text, double expected)
    {
        var values = _parser.Parse(text, isolatedToken: true);

        var value = Assert.Single(values);
        Assert.Equal(expected, value.Value);
        Assert.False(value.IsCorrected);
    }

    [Fact]
    public void Parse_FreeText_DoesNotRewriteCommas()
    {
        // Outside a single cell a comma is punctuation far more often than a
        // damaged decimal point.
        Assert.Equal(new[] { 7d, 17d }, _parser.Parse("7,17").Select(v => v.Value));
    }
}
