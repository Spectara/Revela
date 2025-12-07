using System.CommandLine;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Parent command for theme management
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
public static class ThemeCommand
{
    /// <summary>
    /// Creates the theme command with all subcommands
    /// </summary>
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("theme", "Manage themes for your Revela site");

        // Add subcommands
        command.Subcommands.Add(ThemeListCommand.Create(services));
        command.Subcommands.Add(ThemeExtractCommand.Create(services));

        return command;
    }
}
