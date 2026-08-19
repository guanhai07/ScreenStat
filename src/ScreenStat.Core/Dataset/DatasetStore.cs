using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenStat.Core.Dataset;

/// <summary>
/// Reads and writes the on-disk capture dataset. One capture is one directory
/// holding the screenshot plus <c>capture.json</c> (what the engine produced)
/// and, once reviewed, <c>labels.json</c> (what it should have produced).
/// </summary>
public static class DatasetStore
{
    public const string ImageFileName = "capture.png";
    public const string CaptureFileName = "capture.json";
    public const string LabelsFileName = "labels.json";
    public const string BaselineFileName = "baseline.json";

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Scenario notes are written in Chinese. The default encoder escapes
        // every non-ASCII character, which makes the files unreadable when
        // reviewing a dataset by hand.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string ImagePath(string captureDirectory) => Path.Combine(captureDirectory, ImageFileName);

    public static void WriteCapture(string captureDirectory, CaptureRecord record) =>
        WriteJson(Path.Combine(captureDirectory, CaptureFileName), record);

    public static CaptureRecord? ReadCapture(string captureDirectory) =>
        ReadJson<CaptureRecord>(Path.Combine(captureDirectory, CaptureFileName));

    public static void WriteLabels(string captureDirectory, CaptureLabels labels) =>
        WriteJson(Path.Combine(captureDirectory, LabelsFileName), labels);

    public static CaptureLabels? ReadLabels(string captureDirectory) =>
        ReadJson<CaptureLabels>(Path.Combine(captureDirectory, LabelsFileName));

    public static DatasetBaseline ReadBaseline(string datasetRoot) =>
        ReadJson<DatasetBaseline>(Path.Combine(datasetRoot, BaselineFileName)) ?? DatasetBaseline.Empty;

    public static void WriteBaseline(string datasetRoot, DatasetBaseline baseline) =>
        WriteJson(Path.Combine(datasetRoot, BaselineFileName), baseline);

    /// <summary>
    /// Every capture directory under <paramref name="datasetRoot"/> that has a
    /// readable <c>capture.json</c>, ordered by id so runs are reproducible.
    /// Returns an empty list when the root does not exist yet.
    /// </summary>
    public static IReadOnlyList<DatasetCapture> EnumerateCaptures(string datasetRoot)
    {
        if (string.IsNullOrWhiteSpace(datasetRoot) || !Directory.Exists(datasetRoot))
        {
            return Array.Empty<DatasetCapture>();
        }

        var captures = new List<DatasetCapture>();
        foreach (var directory in Directory.EnumerateDirectories(datasetRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            var record = ReadCapture(directory);
            if (record is null)
            {
                continue;
            }

            captures.Add(new DatasetCapture(record.CaptureId, directory, record, ReadLabels(directory)));
        }

        return captures;
    }

    private static void WriteJson<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write through a temporary file so a crash mid-write cannot leave a
        // truncated document that later breaks the whole regression run.
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, SerializerOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static T? ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException exception)
        {
            // These files get hand-edited, so say which one is broken.
            throw new JsonException($"{path} 不是有效的数据集文件：{exception.Message}", exception);
        }
    }
}

/// <summary>One capture directory: what was recognized, and the labels if reviewed.</summary>
public sealed record DatasetCapture(string Id, string Directory, CaptureRecord Record, CaptureLabels? Labels)
{
    public string ImagePath => Path.Combine(Directory, Record.Image.File);
    public bool IsLabeled => Labels is not null;
}
