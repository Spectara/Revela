using System.CommandLine;

using Spectara.Revela.Commands.Projects.Commands;

namespace Spectara.Revela.Commands.Projects;

/// <summary>
/// Parent command for project management in standalone mode.
/// </summary>
/// <remarks>
/// This command is only registered when running in standalone mode
/// (portable installation with projects/ directory).
/// </remarks>
internal sealed class ProjectsCommand(
    ProjectsListCommand listCommand,
    ProjectsCreateCommand createCommand,
    ProjectsDeleteCommand deleteCommand)
{
    /// <summary>
    /// Creates the CLI command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("projects", "Manage project folders");

        command.Subcommands.Add(listCommand.Create());
        command.Subcommands.Add(createCommand.Create());
        command.Subcommands.Add(deleteCommand.Create());

        return command;
    }
}
