using System.Globalization;
using System.Text;
using ScreenStat.Core.Parsing;

namespace ScreenStat.Tests;

public class NumberParserFuzzTests
{
    private readonly NumberParser _parser = new();

    [Fact]
    public void Parse_RoundTripsManyRandomDecimals()
    {
        var rng = new Random(12345);

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var count = rng.Next(1, 25);
            var expected = new List<double>();
            var text = new StringBuilder();

            for (var i = 0; i < count; i++)
            {
                var whole = rng.Next(-9999, 10000);
                var fraction = rng.Next(0, 1000);
                var value = whole + fraction / 1000.0;
                expected.Add(value);

                if (i > 0)
                {
                    text.Append(rng.Next(2) == 0 ? "\n" : "  ");
                }

                // Always keep a decimal point so random values cannot be
                // misread as date/IP noise.
                text.Append(value.ToString("0.000", CultureInfo.InvariantCulture));
            }

            var actual = _parser.Parse(text.ToString())
                .Select(v => Math.Round(v.Value, 3))
                .OrderBy(v => v)
                .ToList();

            var expectedRounded = expected
                .Select(v => Math.Round(v, 3))
                .OrderBy(v => v)
                .ToList();

            Assert.Equal(expectedRounded, actual);
        }
    }

    [Fact]
    public void Parse_RandomNoise_DoesNotThrow()
    {
        var rng = new Random(54321);
        const string chars = "0123456789.,-:/\n abcdePp%ms";

        for (var i = 0; i < 500; i++)
        {
            var length = rng.Next(0, 160);
            var text = new StringBuilder(length);
            for (var j = 0; j < length; j++)
            {
                text.Append(chars[rng.Next(chars.Length)]);
            }

            _ = _parser.Parse(text.ToString());
        }
    }
}
