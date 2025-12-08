using System.CommandLine;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Parent command for theme management.
/// </summary>
/// <remarks>
/// Subcommands:
/// - list: Show available themes (local + installed)
/// - extract: Extract a theme to themes/ folder for customization
///
/// Future:
/// - add: Install theme from NuGet
/// - remove: Uninstall theme
/// </remarks>
public sealed class ThemeCommand(
    ThemeListCommand listCommand,
    ThemeExtractCommand extractCommand)
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
        command.Subcommands.Add(extractCommand.Create());

        return command;
    }
}
