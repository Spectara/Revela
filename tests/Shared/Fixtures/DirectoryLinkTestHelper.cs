using System.Diagnostics;

namespace Spectara.Revela.Tests.Shared.Fixtures;

public static class DirectoryLinkTestHelper
{
    public static void Create(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(linkPath);
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start junction creation process.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not create test junction: {process.StandardError.ReadToEnd()}");
        }
    }

    public static void Delete(string linkPath)
    {
        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }
    }
}
