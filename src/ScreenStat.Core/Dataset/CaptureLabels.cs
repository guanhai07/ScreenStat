using System.Text.Json.Serialization;

namespace ScreenStat.Core.Dataset;

/// <summary>
/// Where a labeled row came from. The value is what turns a corrected capture
/// into a diagnosis: each origin names one OCR failure mode, so a regression
/// run can count them without having to re-derive what went wrong.
/// </summary>
public enum LabelOrigin
{
    /// <summary>Recognized correctly and kept as is.</summary>
    Ocr,

    /// <summary>Recognized, but the text was wrong and the user fixed it — a misread.</summary>
    Edited,

    /// <summary>The user typed this row because OCR never produced it — a missed cell.</summary>
    Added,

    /// <summary>Recognized but not part of the ground truth — a spurious detection.</summary>
    Excluded
}

/// <summary>
/// The ground truth for one capture, as reviewed by the user in the result
/// window. Only rows with <see cref="LabeledRow.Included"/> count as expected
/// values; the excluded ones are kept so the dataset also records what the
/// engine should <em>not</em> have returned.
/// </summary>
public sealed class CaptureLabels
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string CaptureId { get; init; }
    public DateTimeOffset LabeledAt { get; init; }

    /// <summary>Free-text scenario note, for example "深色主题 / 125% DPI / 12px".</summary>
    public string? Note { get; init; }

    public IReadOnlyList<LabeledColumn> Columns { get; init; } = Array.Empty<LabeledColumn>();

    [JsonIgnore]
    public int RowCount => Columns.Sum(column => column.Rows.Count);

    public int CountByOrigin(LabelOrigin origin) =>
        Columns.Sum(column => column.Rows.Count(row => row.Origin == origin));
}

public sealed class LabeledColumn
{
    public int Index { get; init; }
    public IReadOnlyList<LabeledRow> Rows { get; init; } = Array.Empty<LabeledRow>();

    [JsonIgnore]
    public IReadOnlyList<LabeledRow> IncludedRows =>
        Rows.Where(row => row.Included && row.Value.HasValue).ToArray();

    [JsonIgnore]
    public IReadOnlyList<double> ExpectedValues =>
        IncludedRows.Select(row => row.Value!.Value).ToArray();
}

public sealed class LabeledRow
{
    /// <summary>What OCR returned for this row, or null when the row was added by hand.</summary>
    public string? Recognized { get; init; }

    /// <summary>What the row should read. Equal to <see cref="Recognized"/> when untouched.</summary>
    public string? Corrected { get; init; }

    /// <summary>Parsed value of <see cref="Corrected"/>, or null when the row is not a number.</summary>
    public double? Value { get; init; }

    public bool Included { get; init; }
    public LabelOrigin Origin { get; init; }
}
