using System.CommandLine;
using System.Reflection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Service for running the interactive CLI menu.
/// </summary>
internal sealed partial class InteractiveMenuService(
    CommandPromptBuilder promptBuilder,
    CommandGroupRegistry groupRegistry,
    CommandOrderRegistry orderRegistry,
    IOptions<ProjectConfig> projectConfig,
    ILogger<InteractiveMenuService> logger) : IInteractiveMenuService
{
    private bool bannerShown;

    /// <inheritdoc />
    public RootCommand? RootCommand { get; set; }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (RootCommand is null)
        {
            LogRootCommandNotSet(logger);
            ErrorPanels.ShowError("Internal Error", "[yellow]RootCommand not set.[/]\n\n[dim]This is a bug in Revela. Please report it.[/]");
            return 1;
        }

        ShowWelcomeBanner();

        return await RunMenuLoopAsync(cancellationToken);
    }

    private void ShowWelcomeBanner()
    {
        if (bannerShown)
        {
            return;
        }

        bannerShown = true;

        AnsiConsole.Clear();

        // ASCII logo (2 spaces indent to align with panel border)
        var logoLines = new[]
        {
            @"   ____                _       ",
            @"  |  _ \ _____   _____| | __ _ ",
            @"  | |_) / _ \ \ / / _ \ |/ _` |",
            @"  |  _ <  __/\ V /  __/ | (_| |",
            @"  |_| \_\___| \_/ \___|_|\__,_|",
        };

        foreach (var line in logoLines)
        {
            AnsiConsole.MarkupLine("[cyan1]" + line + "[/]");
        }

        AnsiConsole.WriteLine();

        // Version and description panel
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        var workingDir = Directory.GetCurrentDirectory();
        var folderName = Path.GetFileName(workingDir);

        // Show project name if initialized, otherwise show folder name
        var contextLine = string.IsNullOrEmpty(projectConfig.Value.Name)
            ? $"[dim]Directory:[/] {folderName}"
            : $"[blue]Project:[/] {projectConfig.Value.Name}";

        var panel = new Panel(
            new Markup(
                $"[bold]Version {version}[/]\n" +
                "[dim]Modern static site generator for photographers[/]\n\n" +
                $"{contextLine}\n\n" +
                "[blue]Navigate with[/] [bold]↑↓[/][blue], select with[/] [bold]Enter[/]"))
            .WithHeader("[cyan1]Welcome[/]")
            .WithInfoStyle();

        AnsiConsole.Write(panel);
    }

    private async Task<int> RunMenuLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await ShowMainMenuAsync(cancellationToken);

            if (result.ShouldExit)
            {
                LogExiting(logger);
                AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
                return result.ExitCode;
            }
        }

        return 0;
    }

    private async Task<MenuResult> ShowMainMenuAsync(CancellationToken cancellationToken)
    {
        var prompt = BuildGroupedSelectionPrompt(
            RootCommand!.Subcommands,
            string.Empty);

        var selection = AnsiConsole.Prompt(prompt);

        return selection.Action switch
        {
            MenuAction.Exit => new MenuResult(true, 0),
            MenuAction.Back => new MenuResult(false, 0),
            MenuAction.Navigate => await NavigateToCommandAsync(selection.Command!, [selection.Command!.Name], cancellationToken),
            MenuAction.Execute => await ExecuteCommandAsync(selection.Command!, [selection.Command!.Name], cancellationToken),
            _ => new MenuResult(false, 0),
        };
    }

    private async Task<MenuResult> NavigateToCommandAsync(
        Command parent,
        List<string> commandPath,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pathDisplay = string.Join(" → ", commandPath);
            var prompt = BuildGroupedSelectionPrompt(
                parent.Subcommands,
                $"\n[cyan]{pathDisplay}[/] - [dim]Select a command:[/]",
                includeBack: true,
                includeExit: false);

            var selection = AnsiConsole.Prompt(prompt);

            switch (selection.Action)
            {
                case MenuAction.Back:
                    return new MenuResult(false, 0);

                case MenuAction.Navigate:
                    var subPath = new List<string>(commandPath) { selection.Command!.Name };
                    var result = await NavigateToCommandAsync(selection.Command!, subPath, cancellationToken);
                    if (result.ShouldExit)
                    {
                        return result;
                    }

                    break;

                case MenuAction.Execute:
                    var execPath = new List<string>(commandPath) { selection.Command!.Name };
                    var execResult = await ExecuteCommandAsync(selection.Command!, execPath, cancellationToken);
                    if (execResult.ShouldExit)
                    {
                        return execResult;
                    }

                    break;

                case MenuAction.Exit:
                default:
                    break;
            }
        }

        return new MenuResult(false, 0);
    }

    private async Task<MenuResult> ExecuteCommandAsync(
        Command command,
        List<string> commandPath,
        CancellationToken cancellationToken)
    {
        // Special case: plugin uninstall is not available in interactive mode
        if (IsPluginUninstall(commandPath))
        {
            ShowPluginUninstallWarning();
            WaitForKeyPress();
            return new MenuResult(false, 0);
        }

        var pathDisplay = string.Join(" ", commandPath);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"── [cyan]{pathDisplay}[/] ──");
        AnsiConsole.WriteLine();

        // Prompt for arguments and options
        var arguments = await CommandPromptBuilder.PromptForArgumentsAsync(command, cancellationToken);
        var options = await CommandPromptBuilder.PromptForOptionsAsync(command, cancellationToken);

        // Build args array
        var args = promptBuilder.BuildArgsArray(commandPath, arguments, options);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Executing: revela {string.Join(" ", args)}[/]");
        AnsiConsole.WriteLine();

        // Execute the command
        int exitCode;
        try
        {
            var parseResult = RootCommand!.Parse(args);
            exitCode = await parseResult.InvokeAsync(configuration: null, cancellationToken);
        }
        catch (Exception ex)
        {
            LogCommandFailed(logger, pathDisplay, ex);
            ErrorPanels.ShowException(ex);
            exitCode = 1;
        }

        // Show result only for success
        // Errors should be shown by the command itself using ErrorPanels
        AnsiConsole.WriteLine();
        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Command completed successfully[/]");
        }

        AnsiConsole.WriteLine();
        return new MenuResult(false, exitCode);
    }

    private static bool IsPluginUninstall(List<string> commandPath) =>
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

    private static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// Builds a grouped selection prompt for commands.
    /// </summary>
    /// <param name="commands">The commands to display.</param>
    /// <param name="title">The prompt title.</param>
    /// <param name="includeBack">Whether to include a "Back" option at the top.</param>
    /// <param name="includeExit">Whether to include an "Exit" option at the bottom.</param>
    /// <returns>A configured selection prompt.</returns>
    private SelectionPrompt<MenuChoice> BuildGroupedSelectionPrompt(
        IEnumerable<Command> commands,
        string title,
        bool includeBack = false,
        bool includeExit = true)
    {
        var prompt = new SelectionPrompt<MenuChoice>()
            .Title(title)
            .PageSize(20)
            .WrapAround()
            .Mode(SelectionMode.Leaf)
            .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold));

        // Set disabled style for group headers (dimmed)
        prompt.DisabledStyle = new Style(Color.Grey);

        // Add Back option if requested
        if (includeBack)
        {
            prompt.AddChoice(MenuChoice.Back);
        }

        // Get grouped commands
        var grouped = orderRegistry.GetGroupedCommands(commands, groupRegistry);
        var hasGroups = grouped.Any(g => g.GroupName is not null);

        if (hasGroups)
        {
            // Add each group with its commands
            foreach (var (groupName, groupCommands) in grouped)
            {
                if (groupName is not null)
                {
                    // Create group header as MenuChoice (will be non-selectable due to Leaf mode)
                    var groupChoice = new MenuChoice(groupName, Action: MenuAction.Navigate);
                    var commandChoices = groupCommands.Select(MenuChoice.FromCommand).ToArray();

                    prompt.AddChoiceGroup(groupChoice, commandChoices);
                }
                else
                {
                    // Ungrouped commands - add directly
                    foreach (var cmd in groupCommands)
                    {
                        prompt.AddChoice(MenuChoice.FromCommand(cmd));
                    }
                }
            }
        }
        else
        {
            // No groups - just add sorted commands
            foreach (var cmd in orderRegistry.Sort(commands))
            {
                prompt.AddChoice(MenuChoice.FromCommand(cmd));
            }
        }

        // Add Exit at the end (only for top-level menu)
        if (includeExit)
        {
            prompt.AddChoice(MenuChoice.Exit);
        }

        return prompt;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "RootCommand not set")]
    private static partial void LogRootCommandNotSet(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exiting interactive mode")]
    private static partial void LogExiting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command '{CommandPath}' failed")]
    private static partial void LogCommandFailed(ILogger logger, string commandPath, Exception ex);

    private readonly record struct MenuResult(bool ShouldExit, int ExitCode);
}
