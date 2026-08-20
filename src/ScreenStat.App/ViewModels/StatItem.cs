namespace ScreenStat.App.ViewModels;

/// <summary>
/// One statistic as it is shown in the results header: a label and an already
/// formatted value. Following the stat-tile contract, the label is sentence
/// case with no trailing colon, and the unit travels with the value.
/// </summary>
/// <param name="Label">Short name, for example "Average".</param>
/// <param name="Value">Formatted value including its unit, for example "12.8 ms".</param>
public sealed record StatItem(string Label, string Value);
