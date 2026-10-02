using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

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
        var panel = new Panel($"The installed {Markup.Escape(what)} will be available after restarting Revela.")
            .WithHeader("[bold yellow]Restart Required[/]")
            .WithWarningStyle();
        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Prints the outcome of a plugin or theme install.
    /// </summary>
    /// <param name="result">The install result.</param>
    /// <param name="packageId">The requested package ID.</param>
    /// <param name="kind">Either <c>plugin</c> or <c>theme</c>.</param>
    /// <param name="nextStep">Markup hint printed after a successful install.</param>
    /// <returns>The exit code (0 on success).</returns>
    public static int ReportInstall(PackageInstallResult result, string packageId, string kind, string nextStep)
    {
        ArgumentNullException.ThrowIfNull(result);

        var id = Markup.Escape(result.Package?.Id ?? packageId);
        var label = char.ToUpperInvariant(kind[0]) + kind[1..];
        switch (result.Status)
        {
            case PackageInstallStatus.Installed:
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} {label} [cyan]{id}[/] [dim]{Markup.Escape(result.Package!.Version)}[/] installed successfully.");
                AnsiConsole.MarkupLine(nextStep);
                return 0;

            case PackageInstallStatus.WrongPackageType:
                var types = result.Package!.PackageTypes.Count == 0 ? "none" : string.Join(", ", result.Package.PackageTypes);
                var other = kind == "theme" ? "plugin" : "theme";
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Package [cyan]{id}[/] is not a {kind} (package types: {Markup.Escape(types)}).");
                AnsiConsole.MarkupLine($"  Use [cyan]revela {other} install[/] for {other}s.");
                return 1;

            case PackageInstallStatus.DeclarationFailed:
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} {label} [cyan]{id}[/] [dim]{Markup.Escape(result.Package!.Version)}[/] was installed, but declaring it in dependencies.packages failed:");
                AnsiConsole.MarkupLine($"  {Markup.Escape(result.Error ?? "unknown error")}");
                AnsiConsole.MarkupLine("  The installed files were kept; fix the configuration file and install again.");
                return 1;

            case PackageInstallStatus.InstallerUnavailable:
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Installing packages is not available in this edition.");
                return 1;

            case PackageInstallStatus.Failed:
            default:
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to install {kind} [cyan]{id}[/].");
                return 1;
        }
    }
}
