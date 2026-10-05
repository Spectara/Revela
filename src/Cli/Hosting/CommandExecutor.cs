using System.CommandLine;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Executes CLI commands from the interactive menu with Ctrl+C support.
/// </summary>
internal sealed partial class CommandExecutor
{
    private readonly ILogger<CommandExecutor> logger;
    private readonly Func<CancellationToken, CommandCancellation> listenForCancellation;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutor"/> class.
    /// </summary>
    public CommandExecutor(ILogger<CommandExecutor> logger)
        : this(logger, CommandCancellation.Listen)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom Ctrl+C owner (tests).
    /// </summary>
    internal CommandExecutor(
        ILogger<CommandExecutor> logger,
        Func<CancellationToken, CommandCancellation> listenForCancellation)
    {
        this.logger = logger;
        this.listenForCancellation = listenForCancellation;
    }

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
        // Install and uninstall replace or delete package files. The menu process has those
        // assemblies loaded (locked on Windows, stale in memory elsewhere), so a reinstall,
        // upgrade or removal could leave mixed versions on disk. The menu's setup wizard only
        // adds packages that are not installed yet and then exits for a restart.
        if (PackageManagementCommands.ModifiesPackageFiles(commandPath))
        {
            ShowPackageChangeNotAvailable(commandPath);
            return ExitCodes.Success;
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

        // Ctrl+C cancels only this command (e.g. stops serve) and returns to the menu
        using var cancellation = listenForCancellation(cancellationToken);

        int exitCode;
        try
        {
            exitCode = await cancellation.InvokeAsync(rootCommand.Parse(args));
        }
        catch (OptionsValidationException ex)
        {
            // A configuration value is invalid (e.g. a stray project.language — #75).
            // Show the same friendly panel as the non-interactive path.
            ErrorPanels.ShowConfigurationProblem(ex.Failures);
            exitCode = ExitCodes.ConfigurationProblem;
        }
        catch (Exception ex)
        {
            LogCommandFailed(logger, pathDisplay, ex);
            ErrorPanels.ShowException(ex);
            exitCode = ExitCodes.Error;
        }

        // Show result only for success; errors are shown by the command itself using ErrorPanels
        AnsiConsole.WriteLine();
        if (exitCode == ExitCodes.Success)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Command completed successfully");
        }

        AnsiConsole.WriteLine();
        return exitCode;
    }

    private static void ShowPackageChangeNotAvailable(IReadOnlyList<string> commandPath)
    {
        AnsiConsole.WriteLine();

        // Menu paths are the registered (lower-case) command names
        var kind = commandPath[0];
        var action = commandPath[1];
        var isInstall = action.Equals("install", StringComparison.OrdinalIgnoreCase);
        var verb = isInstall ? "Installing" : "Uninstalling";
        var wizardHint = isInstall
            ? "\n\n[dim]To add packages that are not installed yet, use [white]wizard[/] in the Addons menu.[/]"
            : string.Empty;
        var panel = new Panel(
            new Markup(
                $"[yellow]{verb} a {Markup.Escape(kind)} is not available in interactive mode.[/]\n\n" +
                $"Loaded {Markup.Escape(kind)} files cannot be replaced or deleted while Revela is running.\n" +
                "Please use the command line instead:\n\n" +
                $"[cyan]revela {Markup.Escape(kind)} {Markup.Escape(action)} <{Markup.Escape(kind)}-name>[/]" +
                wizardHint))
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
