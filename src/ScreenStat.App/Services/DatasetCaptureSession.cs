using System.IO;
using ScreenStat.Core.Dataset;
using ScreenStat.Core.Models;

namespace ScreenStat.App.Services;

/// <summary>
/// One capture directory being filled in. The screenshot is already on disk by
/// the time a session exists; recognition and labels arrive later as the user
/// works through the result window.
/// </summary>
public sealed class DatasetCaptureSession
{
    public DatasetCaptureSession(
        string captureId,
        string directoryPath,
        DateTimeOffset capturedAt,
        int width,
        int height)
    {
        CaptureId = captureId;
        DirectoryPath = directoryPath;
        CapturedAt = capturedAt;
        Width = width;
        Height = height;
    }

    public string CaptureId { get; }
    public string DirectoryPath { get; }
    public DateTimeOffset CapturedAt { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsDiscarded { get; private set; }

    public void SaveRecognition(
        OcrDocument document,
        IReadOnlyList<NumericColumn> columns,
        TimeSpan elapsed)
    {
        if (IsDiscarded)
        {
            return;
        }

        DatasetStore.WriteCapture(
            DirectoryPath,
            CaptureRecord.Create(
                CaptureId,
                CapturedAt,
                DatasetStore.ImageFileName,
                Width,
                Height,
                document,
                columns,
                elapsed));
    }

    public void SaveLabels(string? note, IReadOnlyList<LabeledColumn> columns)
    {
        if (IsDiscarded)
        {
            return;
        }

        DatasetStore.WriteLabels(
            DirectoryPath,
            new CaptureLabels
            {
                CaptureId = CaptureId,
                LabeledAt = DateTimeOffset.Now,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Columns = columns
            });
    }

    /// <summary>Deletes the whole capture directory. Used for mis-triggered captures.</summary>
    public void Discard()
    {
        if (IsDiscarded)
        {
            return;
        }

        IsDiscarded = true;
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
