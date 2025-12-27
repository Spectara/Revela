using System.CommandLine;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Parent command for theme management.
/// </summary>
/// <remarks>
/// Subcommands:
/// - list: Show available themes (local + installed)
/// - files: List all theme files with source information
/// - extract: Extract a theme to themes/ folder for customization
/// - install: Install theme from NuGet package index
/// - uninstall: Remove an installed theme
/// </remarks>
public sealed class ThemeCommand(
    ThemeListCommand listCommand,
    ThemeFilesCommand filesCommand,
    ThemeExtractCommand extractCommand,
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

        // Add subcommands
        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(filesCommand.Create());
        command.Subcommands.Add(extractCommand.Create());
        command.Subcommands.Add(installCommand.Create());
        command.Subcommands.Add(uninstallCommand.Create());

        return command;
    }
}
