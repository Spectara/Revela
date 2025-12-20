using System.CommandLine;

using Spectre.Console;

namespace Spectara.Revela.Commands.Clean.Commands;

/// <summary>
/// Cleans both output and cache directories.
/// </summary>
public sealed partial class CleanAllCommand(ILogger<CleanAllCommand> logger)
{
    /// <summary>Order for this command (first in menu).</summary>
    public const int Order = 0;

    /// <summary>Execution order for subcommands.</summary>
    private static readonly string[] ExecutionOrder = ["output", "cache"];

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Clean output and cache (full clean)");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            return await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(Command allCommand, CancellationToken cancellationToken)
    {
        var parent = allCommand.Parents.OfType<Command>().FirstOrDefault();
        if (parent is null)
        {
            LogNoParentCommand(logger);
            return 1;
        }

        // Get all sibling subcommands, excluding 'all' itself, in execution order
        var subcommandsByName = parent.Subcommands
            .Where(cmd => cmd.Name != "all")
            .ToDictionary(cmd => cmd.Name, StringComparer.OrdinalIgnoreCase);

        var orderedSubcommands = new List<Command>();
        foreach (var name in ExecutionOrder)
        {
            if (subcommandsByName.Remove(name, out var cmd))
            {
                orderedSubcommands.Add(cmd);
            }
        }

        orderedSubcommands.AddRange(subcommandsByName.Values);

        if (orderedSubcommands.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No subcommands to execute.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[blue]Cleaning all generated files...[/]");
        AnsiConsole.WriteLine();

        foreach (var subcommand in orderedSubcommands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find root command
            var root = parent;
            while (root.Parents.OfType<Command>().FirstOrDefault() is { } grandparent)
            {
                root = grandparent;
            }

            var args = new[] { parent.Name, subcommand.Name };
            var parseResult = root.Parse(args);
            var exitCode = await parseResult.InvokeAsync(configuration: null, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                LogSubcommandFailed(logger, subcommand.Name, exitCode);
                return exitCode;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ All clean operations completed[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No parent command found for 'all'")]
    private static partial void LogNoParentCommand(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Subcommand '{Name}' failed with exit code {ExitCode}")]
    private static partial void LogSubcommandFailed(ILogger logger, string name, int exitCode);
}
