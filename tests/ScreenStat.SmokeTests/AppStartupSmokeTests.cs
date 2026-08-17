using System.Diagnostics;

namespace ScreenStat.SmokeTests;

public class AppStartupSmokeTests
{
    [Fact]
    public void App_Starts_And_WritesStartupLog()
    {
        var appDll = typeof(ScreenStat.App.App).Assembly.Location;
        var appDir = Path.GetDirectoryName(appDll)!;
        var logPath = Path.Combine(Path.GetTempPath(), "ScreenStat-startup.log");

        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }

        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var psi = new ProcessStartInfo(dotnetHost, $"\"{appDll}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = appDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)!;
        try
        {
            var started = WaitUntil(
                () => File.Exists(logPath) && File.ReadAllText(logPath).Contains("Startup OK", StringComparison.Ordinal),
                TimeSpan.FromSeconds(20));

            Assert.False(process.HasExited, "ScreenStat exited before the smoke check completed.");
            Assert.True(started, "ScreenStat did not write Startup OK to its log in time.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch
            {
                // Log file may still be created/being written.
            }

            Thread.Sleep(250);
        }

        return false;
    }
}
