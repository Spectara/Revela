using System.CommandLine;

namespace Spectara.Revela.Commands.Packages;

/// <summary>
/// Parent command for package management (refresh, search).
/// </summary>
/// <remarks>
/// Combines package index functionality:
/// - refresh: Update local package index from all feeds
/// - search: Search packages in local index
/// 
/// Feed configuration is under 'config feed'.
/// </remarks>
public sealed class PackagesCommand(
    RefreshCommand refreshCommand,
    SearchCommand searchCommand)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("packages", "Manage package index and search");

        command.Subcommands.Add(refreshCommand.Create());
        command.Subcommands.Add(searchCommand.Create());

        return command;
    }
}
