namespace ScreenStat.Core.Dataset;

/// <summary>
/// The ledger of captures the current algorithm is known to get wrong. A
/// capture listed here is reported but does not fail the regression, so newly
/// collected samples can be committed to the backlog without turning the whole
/// suite red. Fixing a case means deleting its entry.
/// </summary>
public sealed class DatasetBaseline
{
    public static DatasetBaseline Empty { get; } = new();

    public IReadOnlyList<KnownFailure> KnownFailures { get; init; } = Array.Empty<KnownFailure>();

    public bool IsKnownFailure(string captureId) =>
        KnownFailures.Any(failure => string.Equals(failure.CaptureId, captureId, StringComparison.OrdinalIgnoreCase));

    public string? ReasonFor(string captureId) => KnownFailures
        .FirstOrDefault(failure => string.Equals(failure.CaptureId, captureId, StringComparison.OrdinalIgnoreCase))
        ?.Reason;
}

public sealed class KnownFailure
{
    public required string CaptureId { get; init; }
    public string? Reason { get; init; }
}
