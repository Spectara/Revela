using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Core.Output;

/// <summary>
/// Guards the shared test console: CI runners advertise ANSI support (e.g.
/// <c>GITHUB_ACTIONS=true</c>), and Spectre's profile enrichers would switch escape
/// codes back on, so every output assertion passed locally but failed in CI.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ConsoleCaptureTests
{
    [TestMethod]
    public async Task RunAsync_OnGitHubActions_WritesPlainText()
    {
        var original = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
        try
        {
            var (_, output) = await ConsoleCapture.RunAsync(() =>
            {
                AnsiConsole.MarkupLine("[bold]Checking[/] [dim]dependencies[/]");
                return Task.FromResult(0);
            });

            Assert.AreEqual("Checking dependencies", output.TrimEnd());
            Assert.DoesNotContain("\u001b[", output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", original);
        }
    }
}
