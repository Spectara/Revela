using System.CommandLine;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Executes CLI commands from the interactive menu with Ctrl+C support.
/// </summary>
internal sealed partial class CommandExecutor(
    ILogger<CommandExecutor> logger)
{
    /// <summary>
    /// Executes a command interactively, prompting for arguments and options.
    /// </summary>
    /// <param name="rootCommand">The root command for parsing.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="commandPath">The full command path (e.g., ["source", "onedrive", "download"]).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The exit code from the command execution.</returns>
    public async Task<int> ExecuteAsync(
        RootCommand rootCommand,
        Command command,
        IReadOnlyList<string> commandPath,
        CancellationToken cancellationToken)
    {
        // Special case: plugin uninstall is not available in interactive mode
        if (IsPluginUninstall(commandPath))
        {
            ShowPluginUninstallWarning();
            return 0;
        }

        var pathDisplay = string.Join(" ", commandPath);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"── [cyan]{Markup.Escape(pathDisplay)}[/] ──");
        AnsiConsole.WriteLine();

        // Prompt for arguments and options
        var arguments = CommandPromptBuilder.PromptForArguments(command);
        var options = CommandPromptBuilder.PromptForOptions(command);

        // Build args array
        var args = CommandPromptBuilder.BuildArgsArray(commandPath, arguments, options);
        var argsDisplay = string.Join(" ", args);
        LogBuiltArgs(logger, argsDisplay);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Executing: revela {Markup.Escape(argsDisplay)}[/]");
        AnsiConsole.WriteLine();

        // Create a linked token source that can be cancelled by Ctrl+C
        // This allows long-running commands (like serve) to be stopped gracefully
        using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Setup Ctrl+C handler for this command execution
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Don't terminate the process
            commandCts.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        // Execute the command
        int exitCode;
        try
        {
            var parseResult = rootCommand.Parse(args);
            exitCode = await parseResult.InvokeAsync(configuration: null, commandCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Command was cancelled by Ctrl+C - this is expected
            exitCode = 0;
        }
        catch (Exception ex)
        {
            LogCommandFailed(logger, pathDisplay, ex);
            ErrorPanels.ShowException(ex);
            exitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        // Show result only for success (and not cancelled)
        // Errors should be shown by the command itself using ErrorPanels
        AnsiConsole.WriteLine();
        if (exitCode == 0 && !commandCts.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Command completed successfully");
        }

        AnsiConsole.WriteLine();
        return exitCode;
    }

    private static bool IsPluginUninstall(IReadOnlyList<string> commandPath) =>
        commandPath.Count >= 2 &&
        commandPath[0].Equals("plugin", StringComparison.OrdinalIgnoreCase) &&
        commandPath[1].Equals("uninstall", StringComparison.OrdinalIgnoreCase);

    private static void ShowPluginUninstallWarning()
    {
        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Markup(
                "[yellow]Plugin uninstall is not available in interactive mode.[/]\n\n" +
                "The plugin assembly is loaded in memory and cannot be deleted.\n" +
                "Please use the command line instead:\n\n" +
                "[cyan]revela plugin uninstall <plugin-name>[/]"))
            .WithHeader("[yellow]⚠ Not Available[/]")
            .WithWarningStyle()
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Command '{CommandPath}' failed")]
    private static partial void LogCommandFailed(ILogger logger, string commandPath, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Built args: {Args}")]
    private static partial void LogBuiltArgs(ILogger logger, string args);
}
