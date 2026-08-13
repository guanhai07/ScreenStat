using System.Text;
using System.Text.RegularExpressions;

namespace ScreenStat.Core.Parsing;

/// <summary>
/// Normalizes noisy OCR text before number extraction.
/// </summary>
public static class OcrTextNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Confusable replacements ONLY near digits, never inside plain words like "latency".
        var normalized = Regex.Replace(text, @"(?<=\d)O|O(?=\d)", "0");
        normalized = Regex.Replace(normalized, @"(?<=\d)o|o(?=\d)", "0");
        normalized = Regex.Replace(normalized, @"(?<=\d)l|l(?=\d)", "1");
        normalized = Regex.Replace(normalized, @"(?<=\d)I|I(?=\d)", "1");
        normalized = Regex.Replace(normalized, @"(?<=\d)S|S(?=\d)", "5");
        normalized = Regex.Replace(normalized, @"(?<=\d)B|B(?=\d)", "8");
        normalized = Regex.Replace(normalized, @"(?<=\d)Z|Z(?=\d)", "2");

        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (ch == '\uFF0C') sb.Append(',');
            else if (ch == '\u3002' || ch == '\uFF0E') sb.Append('.');
            else if (ch == '\uFF0D' || ch == '\u2013' || ch == '\u2014' || ch == '\u2212') sb.Append('-');
            else if (ch == '\uFF05') sb.Append('%');
            else if (ch == '|') sb.Append('1'); // pipe often is 1 in numeric OCR, rare in prose here
            else sb.Append(ch);
        }

        normalized = sb.ToString();

        // False minus from cost=123 / cost-123
        normalized = Regex.Replace(normalized, @"=\s*-", "=");
        normalized = Regex.Replace(normalized, @"(?<=[A-Za-z])\s*-\s*(?=\d)", " ");
        normalized = Regex.Replace(normalized, @"=\s*(?=\d)", " ");

        // Comma thousands: 1,234 / 12,345.67
        normalized = Regex.Replace(
            normalized,
            @"\b(\d{1,3}(?:,\d{3})+)([.]\d+)?\b",
            m => Regex.Replace(m.Groups[1].Value, ",", string.Empty) + m.Groups[2].Value);

        // Space thousands ONLY 1-2 digit head + exactly 3 digits: "1 234", "12 345.67"
        normalized = Regex.Replace(
            normalized,
            @"\b(\d{1,2}) (\d{3})([.]\d+)?\b",
            m => m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value);

        // Broken decimals
        normalized = Regex.Replace(normalized, @"(\d)\s*[.]\s*(\d)", "$1.$2");
        normalized = Regex.Replace(normalized, @"(?<![\d.])[.]\s*(\d+)", "0.$1");

        // ms split
        normalized = Regex.Replace(normalized, @"(\d)\s*m\s*s\b", "$1ms", RegexOptions.IgnoreCase);

        normalized = CollapseBrokenSingleNumbers(normalized);
        return normalized;
    }

    public static string FlattenWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return Regex.Replace(text.Trim(), @"\s+", " ");
    }

    private static string CollapseBrokenSingleNumbers(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // single-digit broken pieces only
            line = Regex.Replace(line, @"\b(\d)\s+(\d{1,2})\b", "$1$2");

            if (!Regex.IsMatch(line, @"^[\d\s,.\-]+(?:\s*[A-Za-z%]{1,4})?$"))
            {
                lines[i] = line;
                continue;
            }

            var groups = Regex.Matches(line, @"\d+");
            if (groups.Count < 2)
            {
                lines[i] = line;
                continue;
            }

            var total = groups.Cast<Match>().Sum(g => g.Length);
            var allSingle = groups.Cast<Match>().All(g => g.Length == 1);
            var hasSingleton = groups.Cast<Match>().Any(g => g.Length == 1);
            var maxLen = groups.Cast<Match>().Max(g => g.Length);

            var shouldJoin =
                (allSingle && total <= 5) ||
                (groups.Count == 2 && hasSingleton && total <= 4) ||
                (groups.Count == 2 && maxLen <= 2 && total <= 3);

            if (shouldJoin)
            {
                line = Regex.Replace(line, @"(?<=\d)\s+(?=\d)", string.Empty);
                line = Regex.Replace(line, @"(?<=\d)\s+(?=[A-Za-z%])", string.Empty);
            }

            lines[i] = line;
        }

        return string.Join(Environment.NewLine, lines);
    }
}
