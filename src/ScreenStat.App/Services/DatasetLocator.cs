using System.IO;

namespace ScreenStat.App.Services;

/// <summary>
/// Decides where captured test data is written. In a development build the app
/// runs out of <c>bin/Debug/...</c> inside the repository, so the dataset lands
/// next to the tests that consume it. A published copy has no repository around
/// it and falls back to the user profile.
/// </summary>
public static class DatasetLocator
{
    private const string OverrideVariableName = "SCREENSTAT_DATASET_DIR";
    private const string RepositoryMarker = "ScreenStat.sln";

    /// <summary>
    /// The dataset root. Also used by the regression tests, so both sides agree
    /// on where collected samples live without repeating the rules.
    /// </summary>
    public static string ResolveRoot()
    {
        var configured = Environment.GetEnvironmentVariable(OverrideVariableName);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is not null)
        {
            return Path.Combine(repositoryRoot, "tests", "data", "captures");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenStat",
            "dataset");
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
