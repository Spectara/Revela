using System.CommandLine;

using Spectre.Console;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to execute the full generation pipeline.
/// </summary>
/// <remarks>
/// Executes all subcommands of 'generate' in defined order, excluding itself.
/// Order 0 ensures this appears first in the menu.
/// </remarks>
public sealed partial class AllCommand(ILogger<AllCommand> logger)
{
    /// <summary>
    /// Order for this command (first in menu).
    /// </summary>
    public const int Order = 0;

    /// <summary>
    /// Pipeline execution order: scan → statistics → pages → images.
    /// Commands not in this list execute after in registration order.
    /// </summary>
    private static readonly string[] ExecutionOrder = ["scan", "statistics", "pages", "images"];

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Execute full pipeline (scan → statistics → pages → images)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(command, cancellationToken));

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

        // Get all sibling subcommands, excluding 'all' itself, sorted by ExecutionOrder
        var subcommandsByName = parent.Subcommands
            .Where(cmd => cmd.Name != "all")
            .ToDictionary(cmd => cmd.Name, StringComparer.OrdinalIgnoreCase);

        var orderedSubcommands = new List<Command>();

        // First, add commands in defined execution order
        foreach (var name in ExecutionOrder)
        {
            if (subcommandsByName.Remove(name, out var cmd))
            {
                orderedSubcommands.Add(cmd);
            }
        }

        // Then, add any remaining commands (future plugins)
        orderedSubcommands.AddRange(subcommandsByName.Values);

        if (orderedSubcommands.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No subcommands to execute.[/]");
            return 0;
        }

        var pipelineNames = string.Join(" → ", orderedSubcommands.Select(c => c.Name));
        AnsiConsole.MarkupLine($"[blue]Executing pipeline:[/] {pipelineNames}");
        AnsiConsole.WriteLine();

        var startTime = DateTime.UtcNow;

        foreach (var subcommand in orderedSubcommands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnsiConsole.MarkupLine($"[cyan]▶ {subcommand.Name}[/] - {subcommand.Description}");

            // Build args for subcommand: "generate <subcommand>"
            var args = new[] { parent.Name, subcommand.Name };

            // Find root command
            var root = parent;
            while (root.Parents.OfType<Command>().FirstOrDefault() is { } grandparent)
            {
                root = grandparent;
            }

            var parseResult = root.Parse(args);
            var exitCode = await parseResult.InvokeAsync(configuration: null, cancellationToken);

            if (exitCode != 0)
            {
                AnsiConsole.MarkupLine($"[red]✗ {subcommand.Name} failed with exit code {exitCode}[/]");
                LogSubcommandFailed(logger, subcommand.Name, exitCode);
                return exitCode;
            }

            AnsiConsole.WriteLine();
        }

        var duration = DateTime.UtcNow - startTime;
        AnsiConsole.MarkupLine($"[green]✓ Pipeline completed in {duration.TotalSeconds:F2}s[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No parent command found for 'all'")]
    private static partial void LogNoParentCommand(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Subcommand '{Name}' failed with exit code {ExitCode}")]
    private static partial void LogSubcommandFailed(ILogger logger, string name, int exitCode);
}
