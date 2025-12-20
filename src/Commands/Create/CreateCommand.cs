using System.CommandLine;

namespace Spectara.Revela.Commands.Create;

/// <summary>
/// Parent command for content creation operations.
/// </summary>
/// <remarks>
/// Subcommands:
/// - page: Create _index.revela files from templates
///
/// Templates are discovered dynamically via IPageTemplate implementations.
/// Core provides 'gallery', plugins can add more (e.g., 'statistics').
/// </remarks>
public sealed class CreateCommand(CreatePageCommand pageCommand)
{
    /// <summary>
    /// Creates the 'create' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("create", "Create content files");

        command.Subcommands.Add(pageCommand.Create());

        return command;
    }
}
