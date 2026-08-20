using ScreenStat.Core.Dataset;

namespace ScreenStat.Tests;

public sealed class DatasetStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ScreenStatDatasetTests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Labels_SurviveRoundTrip()
    {
        var captureDirectory = Path.Combine(_root, "20260818-143012-7f3a");
        var labels = new CaptureLabels
        {
            CaptureId = "20260818-143012-7f3a",
            LabeledAt = new DateTimeOffset(2026, 8, 18, 14, 32, 40, TimeSpan.FromHours(8)),
            Note = "深色主题 / 125% DPI / 12px",
            Columns =
            [
                new LabeledColumn
                {
                    Index = 1,
                    Rows =
                    [
                        new LabeledRow { Recognized = "1037", Corrected = "1037", Value = 1037, Included = true, Origin = LabelOrigin.Ocr },
                        new LabeledRow { Recognized = "O", Corrected = "0", Value = 0, Included = true, Origin = LabelOrigin.Edited },
                        new LabeledRow { Recognized = null, Corrected = "1", Value = 1, Included = true, Origin = LabelOrigin.Added },
                        new LabeledRow { Recognized = "合计", Corrected = "合计", Value = null, Included = false, Origin = LabelOrigin.Excluded }
                    ]
                }
            ]
        };

        DatasetStore.WriteLabels(captureDirectory, labels);
        var loaded = DatasetStore.ReadLabels(captureDirectory);

        Assert.NotNull(loaded);
        Assert.Equal(labels.CaptureId, loaded!.CaptureId);
        Assert.Equal(labels.LabeledAt, loaded.LabeledAt);
        Assert.Equal("深色主题 / 125% DPI / 12px", loaded.Note);
        var column = Assert.Single(loaded.Columns);
        Assert.Equal(new[] { 1037d, 0d, 1d }, column.ExpectedValues);
        Assert.Null(column.Rows[2].Recognized);
        Assert.Equal(LabelOrigin.Added, column.Rows[2].Origin);
        Assert.False(column.Rows[3].Included);
        Assert.Equal(1, loaded.CountByOrigin(LabelOrigin.Edited));
    }

    [Fact]
    public void Notes_AreWrittenWithoutEscapingChineseText()
    {
        var captureDirectory = Path.Combine(_root, "note-readability");
        DatasetStore.WriteLabels(captureDirectory, new CaptureLabels
        {
            CaptureId = "note-readability",
            Note = "浅色报表"
        });

        var json = File.ReadAllText(Path.Combine(captureDirectory, DatasetStore.LabelsFileName));

        Assert.Contains("浅色报表", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void EnumerateCaptures_SkipsDirectoriesWithoutACaptureFile()
    {
        var recorded = Path.Combine(_root, "20260818-090000-aaaa");
        Directory.CreateDirectory(Path.Combine(_root, "not-a-capture"));
        DatasetStore.WriteCapture(recorded, new CaptureRecord
        {
            CaptureId = "20260818-090000-aaaa",
            CapturedAt = DateTimeOffset.Now,
            Image = CaptureImageInfo.Create(DatasetStore.ImageFileName, 46, 126),
            Ocr = new CaptureOcrInfo { Engine = "test", Success = true }
        });

        var captures = DatasetStore.EnumerateCaptures(_root);

        var capture = Assert.Single(captures);
        Assert.Equal("20260818-090000-aaaa", capture.Id);
        Assert.False(capture.IsLabeled);
        Assert.Equal(Math.Round(46d / 126d, 4), capture.Record.Image.AspectRatio);
    }

    [Fact]
    public void EnumerateCaptures_ReturnsEmptyWhenRootIsMissing() =>
        Assert.Empty(DatasetStore.EnumerateCaptures(Path.Combine(_root, "never-created")));

    [Fact]
    public void ReadBaseline_FallsBackToEmpty()
    {
        Directory.CreateDirectory(_root);

        var baseline = DatasetStore.ReadBaseline(_root);

        Assert.Empty(baseline.KnownFailures);
        Assert.False(baseline.IsKnownFailure("anything"));
    }

    [Fact]
    public void Baseline_LooksUpKnownFailuresCaseInsensitively()
    {
        DatasetStore.WriteBaseline(_root, new DatasetBaseline
        {
            KnownFailures = [new KnownFailure { CaptureId = "20260818-143012-7F3A", Reason = "孤立 1 漏检" }]
        });

        var baseline = DatasetStore.ReadBaseline(_root);

        Assert.True(baseline.IsKnownFailure("20260818-143012-7f3a"));
        Assert.Equal("孤立 1 漏检", baseline.ReasonFor("20260818-143012-7f3a"));
    }
}
