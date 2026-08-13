using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using ScreenStat.Core.Abstractions;
using ScreenStat.Core.Models;
using ScreenStat.Core.Parsing;
using Windows.Graphics.Imaging;
using Windows.Globalization;
using Windows.Media.Ocr;
using OcrResult = ScreenStat.Core.Models.OcrResult;

namespace ScreenStat.App.Services;

public sealed class WindowsOcrService : IOcrService
{
    private readonly NumberParser _parser = new();

    public async Task<OcrResult> RecognizeAsync(byte[] bgraPixels, int width, int height, CancellationToken cancellationToken = default)
    {
        if (bgraPixels is null || bgraPixels.Length == 0 || width <= 0 || height <= 0)
        {
            return OcrResult.Failed("截图为空，无法进行 OCR。");
        }

        try
        {
            var engines = CreateEngines();
            if (engines.Count == 0)
            {
                return OcrResult.Failed(
                    "无法创建 Windows OCR 引擎。请在“设置 → 时间和语言 → 语言和区域”中安装英文/中文的“光学字符识别”语言包。");
            }

            var engine = engines[0]; // en-US preferred
            var candidates = OcrImagePreprocessor.BuildCandidates(bgraPixels, width, height);
            if (candidates.Count == 0)
            {
                candidates = new[] { new OcrImagePreprocessor.PreparedImage(bgraPixels, width, height, "raw") };
            }

            // rowIndex -> list of OCR texts for that row
            var rowTexts = new Dictionary<int, List<string>>();
            var fullTexts = new List<string>();
            OcrImagePreprocessor.PreparedImage? debugImage = null;
            var debugScore = int.MinValue;

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await RecognizeOnceAsync(engine, candidate, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var score = Score(text);
                if (score > debugScore)
                {
                    debugScore = score;
                    debugImage = candidate;
                }

                if (TryParseRowIndex(candidate.Profile, out var rowIndex))
                {
                    if (!rowTexts.TryGetValue(rowIndex, out var list))
                    {
                        list = new List<string>();
                        rowTexts[rowIndex] = list;
                    }

                    list.Add(text);
                }
                else
                {
                    fullTexts.Add(text);
                }
            }

            // Optional extra language on one full image.
            var fullContrast = candidates.FirstOrDefault(c => c.Profile == "full-contrast");
            if (fullContrast is not null)
            {
                for (var i = 1; i < engines.Count && i < 2; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = await RecognizeOnceAsync(engines[i], fullContrast, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        fullTexts.Add(text);
                    }
                }
            }

            var merged = BuildResultFromRowsAndFull(rowTexts, fullTexts);
            if (string.IsNullOrWhiteSpace(merged))
            {
                return OcrResult.Failed("OCR 未返回文本。");
            }

            if (debugImage is not null)
            {
                OcrImagePreprocessor.TrySaveDebugPng(debugImage, "ScreenStat-last-ocr.png");
            }

            try
            {
                var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ScreenStat-startup.log");
                var flat = merged.Replace("\r", " ").Replace("\n", " | ");
                var count = _parser.Parse(merged).Count;
                System.IO.File.AppendAllText(
                    log,
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] OCR numbers=" + count +
                    ", rows=" + rowTexts.Count + ", fulls=" + fullTexts.Count +
                    ", text=" + flat + Environment.NewLine);
            }
            catch
            {
                // ignore
            }

            return new OcrResult
            {
                FullText = merged,
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OcrResult.Failed("OCR 识别失败：" + ex.Message);
        }
    }

    private string BuildResultFromRowsAndFull(
        Dictionary<int, List<string>> rowTexts,
        List<string> fullTexts)
    {
        // 1) Each visual row contributes at most ONE number (best candidate for that row).
        var rowNumbers = new List<NumberValue>();
        foreach (var pair in rowTexts.OrderBy(p => p.Key))
        {
            var best = PickBestNumberForRow(pair.Value);
            if (best is not null)
            {
                rowNumbers.Add(best);
            }
        }

        // 2) Best full-frame interpretation (no fragment union).
        string? bestFullText = null;
        var bestFullScore = int.MinValue;
        foreach (var text in fullTexts)
        {
            var cleaned = CleanupFragmentNoise(text);
            var score = Score(cleaned);
            if (score > bestFullScore)
            {
                bestFullScore = score;
                bestFullText = cleaned;
            }
        }

        var fullNumbers = bestFullText is null
            ? new List<NumberValue>()
            : _parser.Parse(bestFullText).ToList();

        // 3) Choose the stronger structured result.
        // Row path is preferred for vertical lists when it finds a reasonable count.
        if (rowNumbers.Count >= fullNumbers.Count && rowNumbers.Count > 0)
        {
            return string.Join(Environment.NewLine, rowNumbers.Select(FormatNumber));
        }

        if (fullNumbers.Count > 0)
        {
            return string.Join(Environment.NewLine, fullNumbers.Select(FormatNumber));
        }

        if (rowNumbers.Count > 0)
        {
            return string.Join(Environment.NewLine, rowNumbers.Select(FormatNumber));
        }

        return bestFullText ?? string.Empty;
    }

    private NumberValue? PickBestNumberForRow(List<string> texts)
    {
        NumberValue? best = null;
        var bestScore = int.MinValue;

        foreach (var text in texts)
        {
            // Collapse a row into one logical line first.
            var line = string.Join(" ", SplitLines(text));
            line = NormalizeLine(line);
            line = CollapseBrokenDigits(line);

            var numbers = _parser.Parse(line);
            if (numbers.Count == 0)
            {
                continue;
            }

            // For a single row crop, prefer the "most complete" number:
            // longest original token, then largest absolute value as weak tie-break.
            var candidate = numbers
                .OrderByDescending(n => (n.OriginalText ?? string.Empty).Count(char.IsDigit))
                .ThenByDescending(n => (n.OriginalText ?? string.Empty).Length)
                .ThenByDescending(n => Math.Abs(n.Value))
                .First();

            var score = (candidate.OriginalText ?? string.Empty).Count(char.IsDigit) * 10
                        + (candidate.OriginalText?.Length ?? 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Drop obvious split fragments when a longer number already exists on the same text,
    /// e.g. keep 145 and remove standalone 1 / 45.
    /// </summary>
    private string CleanupFragmentNoise(string text)
    {
        var lines = SplitLines(text)
            .Select(CleanupLineFragments)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    private string CleanupLineFragments(string line)
    {
        var numbers = _parser.Parse(line);
        if (numbers.Count <= 1)
        {
            return line;
        }

        var kept = new List<NumberValue>();
        foreach (var n in numbers.OrderByDescending(n => DigitsOnly(n.OriginalText ?? n.Value.ToString("G")).Length))
        {
            var digits = DigitsOnly(n.OriginalText ?? n.Value.ToString("G"));
            if (digits.Length == 0)
            {
                continue;
            }

            var isFragment = kept.Any(k =>
            {
                var parent = DigitsOnly(k.OriginalText ?? k.Value.ToString("G"));
                return parent.Length > digits.Length && parent.Contains(digits, StringComparison.Ordinal);
            });

            if (!isFragment)
            {
                kept.Add(n);
            }
        }

        return string.Join(" ", kept.OrderBy(n => n.Position).Select(FormatNumber));
    }

    private static string CollapseBrokenDigits(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return trimmed;
        }

        // "1 56", "14 5", "9 8" on a mostly-numeric line -> join.
        if (Regex.IsMatch(trimmed, @"^[\d\s,.\-]+(?:\s*[A-Za-z%]{1,4})?$"))
        {
            var groups = Regex.Matches(trimmed, @"\d+");
            if (groups.Count >= 2)
            {
                var maxLen = groups.Cast<Match>().Max(m => m.Length);
                // Join when it looks like one number broken apart, not two real stats.
                // Heuristic: no group longer than 3, or total digits <= 6.
                var totalDigits = groups.Cast<Match>().Sum(m => m.Length);
                if (maxLen <= 3 && totalDigits <= 6)
                {
                    trimmed = Regex.Replace(trimmed, @"(?<=\d)\s+(?=\d)", string.Empty);
                    trimmed = Regex.Replace(trimmed, @"(?<=\d)\s+(?=[A-Za-z%])", string.Empty);
                }
            }
        }

        return trimmed;
    }

    private static string DigitsOnly(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string FormatNumber(NumberValue n)
    {
        if (!string.IsNullOrWhiteSpace(n.OriginalText))
        {
            return n.OriginalText.Trim();
        }

        return n.Value.ToString("G");
    }

    private static bool TryParseRowIndex(string profile, out int rowIndex)
    {
        rowIndex = 0;
        // Profiles: row-3, row-bin-3
        var m = Regex.Match(profile, @"^row-(?:bin-)?(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return false;
        }

        return int.TryParse(m.Groups[1].Value, out rowIndex);
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeLine)
            .Where(l => !string.IsNullOrWhiteSpace(l));
    }

    private static async Task<string> RecognizeOnceAsync(
        OcrEngine engine,
        OcrImagePreprocessor.PreparedImage image,
        CancellationToken cancellationToken)
    {
        using var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, image.Width, image.Height, BitmapAlphaMode.Ignore);
        softwareBitmap.CopyFromBuffer(image.BgraPixels.AsBuffer());
        var result = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken).ConfigureAwait(false);
        return ExtractText(result);
    }

    private static string ExtractText(Windows.Media.Ocr.OcrResult? result)
    {
        if (result is null)
        {
            return string.Empty;
        }

        if (result.Lines is { Count: > 0 })
        {
            var lines = new List<string>();
            foreach (var line in result.Lines)
            {
                var normalized = NormalizeLine(line.Text);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    lines.Add(normalized);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        return NormalizeLine(result.Text ?? string.Empty);
    }

    private static string NormalizeLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();

        // Only normalize confusables near digits; doing it globally corrupts
        // words such as "latency" (l -> 1) and "query" (S -> 5 is not in use,
        // but the same principle applies to letters).
        text = Regex.Replace(text, @"(?<=\d)O|O(?=\d)", "0");
        text = Regex.Replace(text, @"(?<=\d)o|o(?=\d)", "0");
        text = Regex.Replace(text, @"(?<=\d)l|l(?=\d)", "1");
        text = Regex.Replace(text, @"(?<=\d)I|I(?=\d)", "1");
        text = Regex.Replace(text, @"(?<=\d)S|S(?=\d)", "5");
        text = Regex.Replace(text, @"(?<=\d)B|B(?=\d)", "8");
        text = Regex.Replace(text, @"(?<=\d)Z|Z(?=\d)", "2");
        text = Regex.Replace(text, @"(?<=\d)\||\|(?=\d)", "1");

        return CollapseBrokenDigits(text.Trim());
    }

    private int Score(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return -100;
        }

        var numbers = _parser.Parse(text);
        // Heavily reward more complete numbers; penalize tiny fragments.
        var score = 0;
        foreach (var n in numbers)
        {
            var digits = (n.OriginalText ?? string.Empty).Count(char.IsDigit);
            if (digits <= 0)
            {
                digits = n.Value.ToString("G").Count(char.IsDigit);
            }

            if (digits <= 1)
            {
                score += 1; // weak
            }
            else if (digits == 2)
            {
                score += 8;
            }
            else
            {
                score += 12 + digits;
            }
        }

        return score;
    }

    private static List<OcrEngine> CreateEngines()
    {
        var list = new List<OcrEngine>();
        foreach (var tag in new[] { "en-US", "en", "zh-Hans", "zh-CN" })
        {
            try
            {
                var language = new Language(tag);
                if (!OcrEngine.IsLanguageSupported(language))
                {
                    continue;
                }

                var engine = OcrEngine.TryCreateFromLanguage(language);
                if (engine is null)
                {
                    continue;
                }

                if (list.All(e => e.RecognizerLanguage.LanguageTag != engine.RecognizerLanguage.LanguageTag))
                {
                    list.Add(engine);
                }
            }
            catch
            {
                // try next
            }
        }

        if (list.Count == 0)
        {
            var fallback = OcrEngine.TryCreateFromUserProfileLanguages();
            if (fallback is not null)
            {
                list.Add(fallback);
            }
        }

        return list;
    }
}
