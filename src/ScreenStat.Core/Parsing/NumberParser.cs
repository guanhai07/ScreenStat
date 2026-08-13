using System.Globalization;
using System.Text.RegularExpressions;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;

namespace ScreenStat.Core.Parsing;

public sealed class NumberParser : INumberParser
{
    private static readonly Regex IpAddressRegex = new(
        @"\b\d{1,3}(?:\.\d{1,3}){3}\b",
        RegexOptions.Compiled);

    // Only for clearly private-looking broken triples: 192 16 100
    private static readonly Regex NoisyIpTripleRegex = new(
        @"(?<![\d.-])(10|127|192|172)(?:[\s.]+)(\d{1,3})(?:[\s.]+)(\d{1,3})\b",
        RegexOptions.Compiled);

    private static readonly Regex IsoDateRegex = new(
        @"\b\d{4}[/-]\d{1,2}[/-]\d{1,2}\b",
        RegexOptions.Compiled);

    private static readonly Regex SlashDateRegex = new(
        @"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b",
        RegexOptions.Compiled);

    // Year + 2..5 small parts (date/time OCR without punctuation), parts must be 1-2 digits.
    private static readonly Regex NoisyDateTimeRegex = new(
        @"\b((?:19|20)\d{2})(?:[\s./-]+(\d{1,2})){2,5}\b",
        RegexOptions.Compiled);

    private static readonly Regex TimeRegex = new(
        @"\b\d{1,2}:\d{2}(?::\d{2})?(?:\.\d+)?\b",
        RegexOptions.Compiled);

    private static readonly Regex NoisyTimeRegex = new(
        @"\b\d{1,2}[:\s]\d{2}[:\s]\d{2}(?:[.,]\d+)?\b",
        RegexOptions.Compiled);

    private static readonly Regex PercentileLabelRegex = new(
        @"\bP\d{2,3}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NumberRegex = new(
        @"(?<![A-Za-z0-9.])(?<number>-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)(?:\s*(?<unit>%|ms|us|μs|ns|sec|secs|second|seconds|KB|MB|GB|TB|kb|mb|gb|tb|m|h|s|[A-Za-z]{1,6}))?(?![A-Za-z0-9.])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<NumberValue> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<NumberValue>();
        }

        var normalized = OcrTextNormalizer.Normalize(text);
        var flat = OcrTextNormalizer.FlattenWhitespace(normalized);
        var sanitized = MaskNonStatPatterns(flat);

        var values = new List<NumberValue>();
        foreach (Match match in NumberRegex.Matches(sanitized))
        {
            var rawNumber = match.Groups["number"].Value;
            if (!TryParseNumber(rawNumber, out var value))
            {
                continue;
            }

            var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value.Trim() : null;
            values.Add(new NumberValue
            {
                Value = value,
                OriginalText = match.Value.Trim(),
                Unit = string.IsNullOrWhiteSpace(unit) ? null : unit,
                Position = match.Index
            });
        }

        // Only rejoin ",000" style tails without commas present as tokens.
        return ReassembleZeroPaddedThousands(values);
    }

    internal static IReadOnlyList<NumberValue> ReassembleZeroPaddedThousands(IReadOnlyList<NumberValue> values)
    {
        if (values.Count < 2)
        {
            return values;
        }

        var result = new List<NumberValue>();
        for (var i = 0; i < values.Count; i++)
        {
            var a = values[i];
            var aDigits = DigitsOnly(a.OriginalText ?? a.Value.ToString("G"));
            if (i + 1 < values.Count &&
                a.Value >= 0 &&
                string.IsNullOrEmpty(a.Unit) &&
                aDigits.Length is >= 1 and <= 3 &&
                IsPureInt(values[i + 1], out var tail) &&
                tail.Length == 3 &&
                tail.All(ch => ch == '0') &&
                string.IsNullOrEmpty(values[i + 1].Unit))
            {
                var combined = aDigits + tail;
                if (TryParseNumber(combined, out var merged))
                {
                    result.Add(new NumberValue
                    {
                        Value = merged,
                        OriginalText = combined,
                        Position = a.Position
                    });
                    i++;
                    continue;
                }
            }

            result.Add(a);
        }

        return result;
    }

    private static bool IsPureInt(NumberValue value, out string digits)
    {
        digits = DigitsOnly(value.OriginalText ?? value.Value.ToString("G"));
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        if ((value.OriginalText ?? string.Empty).Contains('.'))
        {
            return false;
        }

        return Math.Abs(value.Value % 1) < double.Epsilon;
    }

    private static string DigitsOnly(string text) => new string(text.Where(char.IsDigit).ToArray());

    private static string MaskNonStatPatterns(string text)
    {
        var masked = PercentileLabelRegex.Replace(text, " ");
        masked = IpAddressRegex.Replace(masked, " ");
        masked = NoisyIpTripleRegex.Replace(masked, m =>
        {
            for (var i = 1; i <= 3; i++)
            {
                if (!int.TryParse(m.Groups[i].Value, out var n) || n > 255)
                {
                    return m.Value;
                }
            }

            return " ";
        });
        masked = IsoDateRegex.Replace(masked, " ");
        masked = SlashDateRegex.Replace(masked, " ");
        masked = MaskNoisyDateTime(masked);
        masked = TimeRegex.Replace(masked, " ");
        masked = NoisyTimeRegex.Replace(masked, " ");
        return masked;
    }

    private static string MaskNoisyDateTime(string text)
    {
        return NoisyDateTimeRegex.Replace(text, m =>
        {
            var parts = Regex.Matches(m.Value, @"\d+").Cast<Match>()
                .Select(x => int.Parse(x.Value)).ToList();
            if (parts.Count < 3 || parts[0] is < 1900 or > 2100)
            {
                return m.Value;
            }

            for (var i = 1; i < parts.Count; i++)
            {
                if (parts[i] > 59)
                {
                    return m.Value;
                }
            }

            // In noisy OCR there is often no label, and the final numeric
            // token is the statistic we want to keep. When the date/time
            // match reaches the end of the text, keep that final token.
            if (m.Index + m.Length == text.Length)
            {
                var lastNumber = Regex.Match(m.Value, @"\d+\s*$");
                if (lastNumber.Success && lastNumber.Length < m.Value.Length)
                {
                    var prefixLength = m.Value.Length - lastNumber.Length;
                    return Regex.Replace(m.Value.Substring(0, prefixLength), @"\S", " ") + lastNumber.Value;
                }
            }

            return " ";
        });
    }

    private static bool TryParseNumber(string raw, out double value)
    {
        var normalized = raw.Replace(",", string.Empty);
        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }
}
