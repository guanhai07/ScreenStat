using System.IO;
using System.Text;
using ScreenStat.App.Services;
using ScreenStat.Core.Analysis;
using ScreenStat.Core.Dataset;
using Xunit.Abstractions;

namespace ScreenStat.DatasetTests;

/// <summary>
/// Replays every labeled capture through the current recognition pipeline and
/// compares the result against what the user said it should be.
///
/// Captures listed in the dataset's <c>baseline.json</c> are reported but do not
/// fail the run: they are the known-broken backlog. Anything else that stops
/// matching is a regression.
/// </summary>
public sealed class DatasetRegressionTests
{
    private const double Tolerance = 1e-9;
    private const string SeedCaptureId = "seed-dark-theme";

    private readonly ITestOutputHelper _output;

    public DatasetRegressionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task LabeledCaptures_MatchGroundTruth()
    {
        var roots = DatasetRoots();
        _output.WriteLine("数据集根目录：");
        foreach (var root in roots)
        {
            _output.WriteLine($"  {root}{(Directory.Exists(root) ? string.Empty : "（不存在）")}");
        }

        var captures = roots
            .SelectMany(DatasetStore.EnumerateCaptures)
            .Where(capture => capture.IsLabeled)
            .ToArray();

        // Without this the whole test passes vacuously when the seed stops being
        // copied, which would hide every regression it exists to catch.
        Assert.True(
            captures.Any(capture => capture.Id == SeedCaptureId),
            $"种子样本 {SeedCaptureId} 没有加载，检查 ScreenStat.DatasetTests.csproj 里 SeedCapture 的复制规则。");

        if (captures.Length == 1)
        {
            _output.WriteLine(string.Empty);
            _output.WriteLine("目前只有种子样本。在托盘菜单勾选“采集测试数据”后框选，再在结果窗口保存标注，即可扩充数据集。");
        }

        var baseline = MergeBaselines(roots);
        using var service = new RapidLayoutOcrService();
        var analyzer = new CoordinateColumnAnalyzer();
        var parser = new NumericRegionParser();

        var outcomes = new List<CaptureOutcome>();
        foreach (var capture in captures)
        {
            var (pixels, width, height) = DatasetImageLoader.Load(capture.ImagePath);
            var document = await service.RecognizeLayoutAsync(pixels, width, height);
            outcomes.Add(CaptureOutcome.Evaluate(capture, analyzer.Analyze(parser.Parse(document))));
        }

        Report(outcomes, baseline);

        var regressions = outcomes
            .Where(outcome => !outcome.Passed && !baseline.IsKnownFailure(outcome.CaptureId))
            .ToArray();
        Assert.True(regressions.Length == 0, BuildFailureMessage(regressions));
    }

    private void Report(IReadOnlyList<CaptureOutcome> outcomes, DatasetBaseline baseline)
    {
        var expectedTotal = outcomes.Sum(outcome => outcome.ExpectedCount);
        var recoveredTotal = outcomes.Sum(outcome => outcome.RecoveredCount);

        _output.WriteLine(string.Empty);
        _output.WriteLine(
            Pad("样本", 26) + Pad("结果", 10) + Pad("命中", 12) + Pad("标注时的失败", 18) + "备注");
        foreach (var outcome in outcomes)
        {
            var known = baseline.IsKnownFailure(outcome.CaptureId);
            var verdict = outcome.Passed
                ? "通过"
                : known ? "已知失败" : "回归";
            _output.WriteLine(
                Pad(outcome.CaptureId, 26) +
                Pad(verdict, 10) +
                Pad($"{outcome.RecoveredCount}/{outcome.ExpectedCount}", 12) +
                Pad(outcome.LabeledFailureSummary, 18) +
                outcome.Note);
        }

        _output.WriteLine(string.Empty);
        _output.WriteLine(
            $"合计 {outcomes.Count} 个样本，" +
            $"{outcomes.Count(outcome => outcome.Passed)} 个通过，" +
            $"数值命中 {recoveredTotal}/{expectedTotal}" +
            (expectedTotal == 0 ? string.Empty : $"（{(double)recoveredTotal / expectedTotal:P1}）"));

        foreach (var outcome in outcomes.Where(outcome => !outcome.Passed))
        {
            _output.WriteLine(string.Empty);
            _output.WriteLine(outcome.Describe(baseline.ReasonFor(outcome.CaptureId)));
        }

        var stale = outcomes
            .Where(outcome => outcome.Passed && baseline.IsKnownFailure(outcome.CaptureId))
            .ToArray();
        if (stale.Length > 0)
        {
            _output.WriteLine(string.Empty);
            _output.WriteLine("以下样本已经能通过，可从 baseline.json 移除：");
            foreach (var outcome in stale)
            {
                _output.WriteLine($"  {outcome.CaptureId}");
            }
        }
    }

    private static string BuildFailureMessage(IReadOnlyList<CaptureOutcome> regressions)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{regressions.Count} 个样本与标注不一致，且不在 baseline.json 中：");
        foreach (var outcome in regressions)
        {
            builder.AppendLine();
            builder.AppendLine(outcome.Describe(null));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Pads to a column width measured in terminal cells, not characters. The
    /// report mixes ids with Chinese verdicts, and CJK glyphs take two cells.
    /// </summary>
    private static string Pad(string text, int width)
    {
        var used = text.Sum(character => IsFullWidth(character) ? 2 : 1);
        return text + new string(' ', Math.Max(1, width - used));
    }

    private static bool IsFullWidth(char character) =>
        character is (>= '⺀' and <= '鿿')   // CJK radicals through ideographs
            or (>= '＀' and <= '｠')         // full-width forms
            or (>= '￠' and <= '￦');

    /// <summary>
    /// The committed seed sample plus wherever the app writes collected
    /// captures, resolved with the same rules the app uses.
    /// </summary>
    private static IReadOnlyList<string> DatasetRoots()
    {
        var roots = new List<string> { Path.Combine(AppContext.BaseDirectory, "Dataset") };
        var collected = DatasetLocator.ResolveRoot();
        if (!roots.Contains(collected, StringComparer.OrdinalIgnoreCase))
        {
            roots.Add(collected);
        }

        return roots;
    }

    private static DatasetBaseline MergeBaselines(IEnumerable<string> roots) => new()
    {
        KnownFailures = roots
            .SelectMany(root => DatasetStore.ReadBaseline(root).KnownFailures)
            .ToArray()
    };

    private sealed class CaptureOutcome
    {
        private CaptureOutcome(
            DatasetCapture capture,
            IReadOnlyList<IReadOnlyList<double>> expected,
            IReadOnlyList<IReadOnlyList<double>> actual)
        {
            CaptureId = capture.Id;
            Note = capture.Labels?.Note ?? string.Empty;
            LabeledFailureSummary = BuildLabeledFailureSummary(capture.Labels);
            Expected = expected;
            Actual = actual;
        }

        public string CaptureId { get; }
        public string Note { get; }

        /// <summary>What the engine got wrong when the sample was labeled, by origin.</summary>
        public string LabeledFailureSummary { get; }

        public IReadOnlyList<IReadOnlyList<double>> Expected { get; }
        public IReadOnlyList<IReadOnlyList<double>> Actual { get; }

        public int ExpectedCount => Expected.Sum(column => column.Count);

        /// <summary>
        /// How many expected values came back at all, ignoring which column they
        /// landed in. A single missed row shifts every later row, so a strict
        /// positional count would understate what actually still works.
        /// </summary>
        public int RecoveredCount
        {
            get
            {
                var remaining = Actual.SelectMany(column => column).Select(Key).ToList();
                var recovered = 0;
                foreach (var value in Expected.SelectMany(column => column).Select(Key))
                {
                    if (remaining.Remove(value))
                    {
                        recovered++;
                    }
                }

                return recovered;
            }
        }

        public bool Passed =>
            Expected.Count == Actual.Count &&
            Expected.Zip(Actual).All(pair => SequenceMatches(pair.First, pair.Second));

        public static CaptureOutcome Evaluate(
            DatasetCapture capture,
            IReadOnlyList<ScreenStat.Core.Models.NumericColumn> columns)
        {
            var expected = capture.Labels!.Columns
                .Select(column => column.ExpectedValues)
                .Where(values => values.Count > 0)
                .ToArray();
            var actual = columns
                .Select(column => (IReadOnlyList<double>)column.Tokens.Select(token => token.Value).ToArray())
                .ToArray();
            return new CaptureOutcome(capture, expected, actual);
        }

        public string Describe(string? knownReason)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"样本 {CaptureId}{(string.IsNullOrEmpty(Note) ? string.Empty : $"（{Note}）")}");
            if (!string.IsNullOrEmpty(knownReason))
            {
                builder.AppendLine($"  已知原因：{knownReason}");
            }

            builder.AppendLine($"  期望 {Expected.Count} 列，实际 {Actual.Count} 列");
            for (var index = 0; index < Math.Max(Expected.Count, Actual.Count); index++)
            {
                var expected = index < Expected.Count ? Format(Expected[index]) : "（无此列）";
                var actual = index < Actual.Count ? Format(Actual[index]) : "（无此列）";
                if (expected == actual)
                {
                    continue;
                }

                builder.AppendLine($"  第 {index + 1} 列");
                builder.AppendLine($"    期望：{expected}");
                builder.AppendLine($"    实际：{actual}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildLabeledFailureSummary(CaptureLabels? labels)
        {
            if (labels is null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var misread = labels.CountByOrigin(LabelOrigin.Edited);
            var missed = labels.CountByOrigin(LabelOrigin.Added);
            var spurious = labels.CountByOrigin(LabelOrigin.Excluded);
            if (misread > 0)
            {
                parts.Add($"误读{misread}");
            }

            if (missed > 0)
            {
                parts.Add($"漏检{missed}");
            }

            if (spurious > 0)
            {
                parts.Add($"误检{spurious}");
            }

            return parts.Count == 0 ? "无" : string.Join(" ", parts);
        }

        private static bool SequenceMatches(IReadOnlyList<double> expected, IReadOnlyList<double> actual) =>
            expected.Count == actual.Count &&
            expected.Zip(actual).All(pair => Math.Abs(pair.First - pair.Second) < Tolerance);

        private static string Format(IReadOnlyList<double> values) =>
            values.Count == 0 ? "（空）" : string.Join(", ", values.Select(value => value.ToString("0.####")));

        private static double Key(double value) => Math.Round(value, 9);
    }
}
