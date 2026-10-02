using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Guards host commands that would prompt for missing input.
/// </summary>
/// <remarks>
/// Spectre.Console prompts throw on a console without a terminal (CI, pipes, Docker without
/// <c>-it</c>). Commands that ask for values when called without options check
/// <see cref="EnsureInteractive"/> first, so scripts get a clear message naming the options to
/// pass and exit code 1 instead of a stack trace or a hang.
/// </remarks>
public static class InteractiveInput
{
    /// <summary>
    /// Reason for commands that only prompt because no options were given.
    /// </summary>
    public const string NoOptionsGiven = "No options given and this console is not interactive.";

    /// <summary>
    /// Returns whether the console can prompt. If it cannot, shows an "Interactive Input Required"
    /// panel with <paramref name="reason"/> and the non-interactive alternatives.
    /// </summary>
    /// <param name="console">The current console capabilities.</param>
    /// <param name="reason">Why the command would prompt (plain text).</param>
    /// <param name="usages">Commands that work without prompting (plain text, printed literally).</param>
    /// <returns><c>true</c> when prompting is possible; otherwise <c>false</c>.</returns>
    public static bool EnsureInteractive(this IConsoleCapabilities console, string reason, params IReadOnlyList<string> usages)
    {
        ArgumentNullException.ThrowIfNull(console);

        if (console.IsInteractive)
        {
            return true;
        }

        var usageLines = string.Join("\n", usages.Select(usage => $"  [cyan]{Markup.Escape(usage)}[/]"));
        ErrorPanels.ShowError(
            "Interactive Input Required",
            $"[yellow]{Markup.Escape(reason)}[/]\n\n[bold]Use:[/]\n{usageLines}");
        return false;
    }
}
