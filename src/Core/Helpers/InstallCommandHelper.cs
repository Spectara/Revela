using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Shared helper methods for install commands (theme, plugin).
/// </summary>
public static class InstallCommandHelper
{
    /// <summary>
    /// Choice text for selecting all items in a multi-select prompt.
    /// </summary>
    public const string SelectAllChoice = "[yellow]» All «[/]";

    /// <summary>
    /// Truncates text to a maximum length with ellipsis.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum length including ellipsis.</param>
    /// <returns>Truncated text or original if shorter than maxLength.</returns>
    public static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    /// <summary>
    /// Shows the restart required notice after installing packages.
    /// </summary>
    /// <param name="what">What was installed (e.g., "plugins", "themes").</param>
    public static void ShowRestartNotice(string what)
    {
        AnsiConsole.WriteLine();
        ErrorPanels.ShowRestartRequired(what);
    }
}
