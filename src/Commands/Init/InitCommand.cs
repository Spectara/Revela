using System.CommandLine;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Parent command for initialization operations.
/// </summary>
/// <remarks>
/// Subcommands:
/// - project: Initialize a new Revela project
///
/// Theme initialization has been moved to 'revela theme extract'.
/// </remarks>
public sealed class InitCommand(InitProjectCommand projectCommand)
{
    /// <summary>
    /// Creates the 'init' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("init", "Initialize a new Revela project");

        // Add subcommands (injected via DI)
        command.Subcommands.Add(projectCommand.Create());

        return command;
    }
}

