using System.CommandLine;

namespace Spectara.Revela.Features.Init;

/// <summary>
/// Parent command for initialization operations
/// </summary>
public static class InitCommand
{
    /// <summary>
    /// Creates the 'init' command with subcommands
    /// </summary>
    public static Command Create()
    {
        var command = new Command("init", "Initialize new project or theme");

        // Add subcommands
        command.Subcommands.Add(InitProjectCommand.Create());
        command.Subcommands.Add(InitThemeCommand.Create());

        return command;
    }
}

