using System.CommandLine;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Features.Theme.Commands;

/// <summary>
/// Parent command for theme management.
/// </summary>
/// <remarks>
/// Subcommands:
/// - list: Show available themes (local + installed)
/// - files: List all theme files with source information
/// - extract: Extract a theme to themes/ folder for customization
/// - install: Install theme from NuGet (only when Packages feature is loaded)
/// - uninstall: Remove an installed theme (only when Packages feature is loaded)
/// </remarks>
internal sealed class ThemeCommand(
    ThemeListCommand listCommand,
    ThemeFilesCommand filesCommand,
    ThemeExtractCommand extractCommand,
    PackageInstallService packageInstallService,
    ThemeInstallCommand installCommand,
    ThemeUninstallCommand uninstallCommand)
{
    /// <summary>
    /// Creates the theme command with all subcommands.
    /// </summary>
    /// <returns>The configured theme command.</returns>
    public Command Create()
    {
        var command = new Command("theme", "Manage themes for your Revela site");

        // Always available
        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(filesCommand.Create());
        command.Subcommands.Add(extractCommand.Create());

        // Only available when the Packages feature is loaded (Cli, not Cli.Embedded — like 'plugin')
        if (packageInstallService.IsAvailable)
        {
            command.Subcommands.Add(installCommand.Create());
            command.Subcommands.Add(uninstallCommand.Create());
        }

        return command;
    }
}

