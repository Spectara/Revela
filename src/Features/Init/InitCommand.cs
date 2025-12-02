using System.CommandLine;

namespace Spectara.Revela.Features.Init;

/// <summary>
/// Parent command for initialization operations.
/// </summary>
public sealed class InitCommand(
    InitProjectCommand projectCommand,
    InitThemeCommand themeCommand)
{
    /// <summary>
    /// Creates the 'init' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("init", "Initialize new project or theme");

        // Add subcommands (injected via DI)
        command.Subcommands.Add(projectCommand.Create());
        command.Subcommands.Add(themeCommand.Create());

        return command;
    }
}

