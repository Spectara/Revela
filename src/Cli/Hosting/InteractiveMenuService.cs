using System.CommandLine;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

using Spectre.Console;

using ProjectWizard = Spectara.Revela.Commands.Project.Wizard;
using RevelaWizard = Spectara.Revela.Commands.Revela.Wizard;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Service for running the interactive CLI menu.
/// </summary>
internal sealed partial class InteractiveMenuService(
    IOptions<ProjectEnvironment> projectEnvironment,
    CommandPromptBuilder promptBuilder,
    CommandGroupRegistry groupRegistry,
    CommandOrderRegistry orderRegistry,
    IOptionsMonitor<ProjectConfig> projectConfig,
    IConfigService configService,
    RevelaWizard revelaWizard,
    ProjectWizard projectWizard,
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

        // Check if terminal supports interactive mode (TTY available)
        // This fails in Docker without -it, CI/CD pipelines, or piped input
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            LogNonInteractiveTerminal(logger);
            AnsiConsole.MarkupLine("[yellow]Interactive mode requires a terminal.[/]");
            AnsiConsole.MarkupLine("[dim]Use [white]revela --help[/] for available commands.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Tip: In Docker, use [white]docker run -it ...[/] for interactive mode.[/]");
            return 1;
        }

        // Check if this is a fresh installation (no revela.json)
        if (!GlobalConfigManager.ConfigFileExists())
        {
            return await HandleFirstRunAsync(cancellationToken);
        }

        // Check if this directory has no project (no project.json)
        if (!configService.IsProjectInitialized())
        {
            return await HandleNoProjectAsync(cancellationToken);
        }

        ShowWelcomeBanner();

        return await RunMenuLoopAsync(cancellationToken);
    }

    private async Task<int> HandleFirstRunAsync(CancellationToken cancellationToken)
    {
        ConsoleUI.ClearAndShowLogo();
        ConsoleUI.ShowFirstRunPanel();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]What would you like to do?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(["Start Setup Wizard", "Skip (use menu instead)", "Exit"]));

        if (choice == "Exit")
        {
            return ExitWithGoodbye();
        }

        if (choice == "Start Setup Wizard")
        {
            var result = await revelaWizard.RunAsync(cancellationToken);

            // Exit code 2 = packages installed, restart required
            if (result == RevelaWizard.ExitCodeRestartRequired)
            {
                return 0;
            }

            // Exit code 0 = nothing installed, continue to menu
            if (result == 0)
            {
                bannerShown = true; // Don't show banner, wizard already showed welcome
                return await RunMenuLoopAsync(cancellationToken);
            }

            // Wizard failed or was cancelled - continue to menu
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Setup was not completed. Continuing to menu...[/]");
            WaitForKeyPress();
        }

        // User skipped or wizard failed - show normal menu
        bannerShown = true; // Don't show banner again, we already showed first-run screen
        return await RunMenuLoopAsync(cancellationToken);
    }

    private async Task<int> HandleNoProjectAsync(CancellationToken cancellationToken)
    {
        ConsoleUI.ClearAndShowLogo();

        var folderName = projectEnvironment.Value.FolderName;

        var panel = new Panel(
            new Markup(
                $"[bold]No Project Found[/]\n\n" +
                $"This directory ([cyan]{folderName}[/]) doesn't contain a Revela project.\n" +
                "Would you like to create one?"))
            .WithHeader("[cyan1]Create Project[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]What would you like to do?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(["Create New Project", "Skip (use menu instead)", "Exit"]));

        if (choice == "Exit")
        {
            return ExitWithGoodbye();
        }

        if (choice == "Create New Project")
        {
            return await RunProjectWizardAsync(cancellationToken);
        }

        // User skipped or wizard failed - show normal menu
        bannerShown = true; // Don't show banner again
        return await RunMenuLoopAsync(cancellationToken);
    }

    private async Task<int> RunProjectWizardAsync(CancellationToken cancellationToken)
    {
        var result = await projectWizard.RunAsync(cancellationToken);

        if (result == 0)
        {
            bannerShown = true; // Don't show banner, wizard already showed welcome
            return await RunMenuLoopAsync(cancellationToken);
        }

        // Wizard failed or was cancelled - continue to menu
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Project creation was not completed. Continuing to menu...[/]");
        WaitForKeyPress();

        // User skipped or wizard failed - show normal menu
        bannerShown = true; // Don't show banner again
        return await RunMenuLoopAsync(cancellationToken);
    }

    private void ShowWelcomeBanner()
    {
        if (bannerShown)
        {
            return;
        }

        bannerShown = true;

        ConsoleUI.ClearAndShowLogo();

        // Show project name if initialized, otherwise show folder name
        var projectName = projectConfig.CurrentValue.Name;
        var folderName = projectEnvironment.Value.FolderName;

        ConsoleUI.ShowWelcomePanel(projectName, folderName);
    }

    private async Task<int> RunMenuLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await ShowMainMenuAsync(cancellationToken);

            if (result.ShouldExit)
            {
                return ExitWithGoodbye();
            }
        }

        return 0;
    }

    private async Task<MenuResult> ShowMainMenuAsync(CancellationToken cancellationToken)
    {
        var prompt = BuildGroupedSelectionPrompt(
            RootCommand!.Subcommands,
            string.Empty,
            includeSetupWizard: true);

        var selection = AnsiConsole.Prompt(prompt);

        return selection.Action switch
        {
            MenuAction.Exit => new MenuResult(true, 0),
            MenuAction.Back => new MenuResult(false, 0),
            MenuAction.Navigate => await NavigateToCommandAsync(selection.Command!, [selection.Command!.Name], cancellationToken),
            MenuAction.Execute => await ExecuteCommandAsync(selection.Command!, [selection.Command!.Name], cancellationToken),
            MenuAction.RunSetupWizard => await RunSetupWizardAsync(cancellationToken),
            _ => new MenuResult(false, 0),
        };
    }

    private async Task<MenuResult> RunSetupWizardAsync(CancellationToken cancellationToken)
    {
        var result = await revelaWizard.RunAsync(cancellationToken);

        // Exit code 2 = packages installed, restart required
        if (result == RevelaWizard.ExitCodeRestartRequired)
        {
            return new MenuResult(true, 0);
        }

        // Exit code 0 = nothing installed, continue to menu
        if (result == 0)
        {
            return new MenuResult(false, 0);
        }

        // Wizard failed or was cancelled - continue menu
        WaitForKeyPress();
        return new MenuResult(false, 0);
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

                case MenuAction.RunSetupWizard:
                    var wizardResult = await RunSetupWizardAsync(cancellationToken);
                    if (wizardResult.ShouldExit)
                    {
                        return wizardResult;
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
            var parseResult = RootCommand!.Parse(args);
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
    /// Gets whether a project is currently initialized in the current directory.
    /// </summary>
    /// <remarks>
    /// Uses file-based check (project.json exists) instead of IOptions
    /// because IOptions is cached at startup and doesn't reflect changes
    /// made during the session (e.g., after running the wizard).
    /// </remarks>
    private bool HasProject => configService.IsProjectInitialized();

    /// <summary>
    /// Filters commands based on project requirement.
    /// </summary>
    /// <param name="commands">The commands to filter.</param>
    /// <returns>Commands that are available based on current project status.</returns>
    private IEnumerable<Command> FilterCommandsByProject(IEnumerable<Command> commands)
    {
        if (HasProject)
        {
            // Hide commands that are only for "no project" state (e.g., init)
            return commands.Where(cmd => !orderRegistry.ShouldHideWhenProjectExists(cmd));
        }

        // Only show commands that don't require a project
        return commands.Where(cmd => !orderRegistry.RequiresProject(cmd));
    }

    /// <summary>
    /// Builds a grouped selection prompt for commands.
    /// </summary>
    /// <param name="commands">The commands to display.</param>
    /// <param name="title">The prompt title.</param>
    /// <param name="includeBack">Whether to include a "Back" option at the top.</param>
    /// <param name="includeExit">Whether to include an "Exit" option at the bottom.</param>
    /// <param name="includeSetupWizard">Whether to include the Wizard option in the Addons group.</param>
    /// <returns>A configured selection prompt.</returns>
    private SelectionPrompt<MenuChoice> BuildGroupedSelectionPrompt(
        IEnumerable<Command> commands,
        string title,
        bool includeBack = false,
        bool includeExit = true,
        bool includeSetupWizard = false)
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

        // Filter commands based on project status and get grouped commands
        var filteredCommands = FilterCommandsByProject(commands);
        var grouped = orderRegistry.GetGroupedCommands(filteredCommands, groupRegistry);
        var hasGroups = grouped.Any(g => g.GroupName is not null);

        if (hasGroups)
        {
            // Add each group with its commands
            foreach (var (groupName, groupCommands) in grouped)
            {
                // Skip empty groups (can happen after filtering)
                if (groupCommands.Count == 0)
                {
                    continue;
                }

                if (groupName is not null)
                {
                    // Create group header as MenuChoice (will be non-selectable due to Leaf mode)
                    var groupChoice = new MenuChoice(groupName, Action: MenuAction.Navigate);
                    var commandChoices = groupCommands.Select(MenuChoice.FromCommand).ToList();

                    // Add Wizard to the Addons group (as last item)
                    if (includeSetupWizard && groupName == CommandGroups.Addons)
                    {
                        commandChoices.Add(MenuChoice.Wizard);
                    }

                    prompt.AddChoiceGroup(groupChoice, [.. commandChoices]);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Interactive mode unavailable - terminal does not support interactive input")]
    private static partial void LogNonInteractiveTerminal(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exiting interactive mode")]
    private static partial void LogExiting(ILogger logger);

    private int ExitWithGoodbye()
    {
        LogExiting(logger);
        AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Command '{CommandPath}' failed")]
    private static partial void LogCommandFailed(ILogger logger, string commandPath, Exception ex);

    private readonly record struct MenuResult(bool ShouldExit, int ExitCode);
}
