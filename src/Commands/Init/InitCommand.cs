using System.CommandLine;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Parent command for initialization operations.
/// </summary>
/// <remarks>
/// Subcommands:
/// - project: Initialize a new Revela project
/// - page: Create _index.revela from template
/// - config: Create plugin configuration file
///
/// Theme initialization has been moved to 'revela theme extract'.
/// </remarks>
public sealed class InitCommand(
    InitProjectCommand projectCommand,
    PageInitCommand pageCommand,
    ConfigInitCommand configCommand)
{
    /// <summary>
    /// Creates the 'init' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("init", "Initialize a new Revela project");

        // Add subcommands (injected via DI)
        command.Subcommands.Add(projectCommand.Create());
        command.Subcommands.Add(pageCommand.Create());
        command.Subcommands.Add(configCommand.Create());

        return command;
    }
}

