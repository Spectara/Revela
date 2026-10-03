using System.CommandLine;
using System.Globalization;

using Spectre.Console;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Runs code against a non-interactive, colorless Spectre console and captures its output.
/// </summary>
/// <remarks>
/// Swaps the global <see cref="AnsiConsole.Console"/>, so test classes using it must be marked
/// <c>[DoNotParallelize]</c>. Any Spectre prompt throws on this console, which makes an unexpected
/// prompt fail the test instead of hanging it.
/// </remarks>
public static class ConsoleCapture
{
    /// <summary>
    /// Runs <paramref name="action"/> and returns its exit code and console output.
    /// </summary>
    public static async Task<(int ExitCode, string Output)> RunAsync(Func<Task<int>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            // CI environments (GitHub Actions, Azure Pipelines, …) would switch ANSI back on.
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 240;
        AnsiConsole.Console = console;

        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    /// <summary>
    /// Parses and invokes <paramref name="command"/> with <paramref name="args"/>.
    /// </summary>
    public static Task<(int ExitCode, string Output)> InvokeAsync(Command command, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(command);

        return RunAsync(() =>
        {
            var parseResult = command.Parse(args);
            Assert.IsEmpty(parseResult.Errors, string.Join("; ", parseResult.Errors.Select(e => e.Message)));
            return parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
        });
    }
}
