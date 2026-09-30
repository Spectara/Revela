using Spectara.Revela.Core.Models;
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
    /// Builds the Spectre markup choice text for a package index entry.
    /// </summary>
    /// <remarks>
    /// The package ID is the first space-delimited token so callers can map a selection back to it.
    /// </remarks>
    /// <param name="package">The package index entry.</param>
    /// <returns>Markup-safe choice text.</returns>
    public static string FormatPackageChoice(PackageIndexEntry package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return $"{Markup.Escape(package.Id)} [dim]({Markup.Escape(package.Version)})[/] - {Markup.Escape(Truncate(package.Description, 40))}";
    }

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
