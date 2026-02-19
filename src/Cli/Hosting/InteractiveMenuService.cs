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
    CommandExecutor commandExecutor,
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

        var choice = PromptForAction("Start Setup Wizard");

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

            // Exit code != 0 = wizard failed or was cancelled
            if (result != 0)
            {
                ShowWizardIncompleteMessage("Setup");
            }
        }

        return await ContinueToMenuAsync(cancellationToken);
    }

    private async Task<int> HandleNoProjectAsync(CancellationToken cancellationToken)
    {
        ConsoleUI.ClearAndShowLogo();

        var folderName = projectEnvironment.Value.FolderName;

        var panel = new Panel(
            new Markup(
                $"[bold]No Project Found[/]\n\n" +
                $"This directory ([cyan]{Markup.Escape(folderName)}[/]) doesn't contain a Revela project.\n" +
                "Would you like to create one?"))
            .WithHeader("[cyan1]Create Project[/]")
            .WithInfoStyle()
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var choice = PromptForAction("Create New Project");

        if (choice == "Exit")
        {
            return ExitWithGoodbye();
        }

        if (choice == "Create New Project")
        {
            var result = await projectWizard.RunAsync(cancellationToken);

            if (result != 0)
            {
                ShowWizardIncompleteMessage("Project creation");
            }
        }

        return await ContinueToMenuAsync(cancellationToken);
    }

    /// <summary>
    /// Shows a message when a wizard didn't complete successfully, then waits for key press.
    /// </summary>
    private static void ShowWizardIncompleteMessage(string wizardName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow]{wizardName} was not completed. Continuing to menu...[/]");
        WaitForKeyPress();
    }

    /// <summary>
    /// Continues to the main menu loop, suppressing the welcome banner.
    /// </summary>
    private async Task<int> ContinueToMenuAsync(CancellationToken cancellationToken)
    {
        bannerShown = true;
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

            if (selection.Action == MenuAction.Back)
            {
                return new MenuResult(false, 0);
            }

            var result = await HandleMenuActionAsync(selection, commandPath, cancellationToken);
            if (result.ShouldExit)
            {
                return result;
            }
        }

        return new MenuResult(false, 0);
    }

    /// <summary>
    /// Dispatches a menu selection to the appropriate handler.
    /// </summary>
    private async Task<MenuResult> HandleMenuActionAsync(
        MenuChoice selection,
        List<string> commandPath,
        CancellationToken cancellationToken)
    {
        if (selection.Action == MenuAction.RunSetupWizard)
        {
            return await RunSetupWizardAsync(cancellationToken);
        }

        var extendedPath = new List<string>(commandPath) { selection.Command!.Name };

        return selection.Action switch
        {
            MenuAction.Navigate => await NavigateToCommandAsync(selection.Command!, extendedPath, cancellationToken),
            MenuAction.Execute => await ExecuteCommandAsync(selection.Command!, extendedPath, cancellationToken),
            MenuAction.Back or MenuAction.Exit or MenuAction.RunSetupWizard or _ => new MenuResult(false, 0),
        };
    }

    private async Task<MenuResult> ExecuteCommandAsync(
        Command command,
        List<string> commandPath,
        CancellationToken cancellationToken)
    {
        var exitCode = await commandExecutor.ExecuteAsync(RootCommand!, command, commandPath, cancellationToken);
        return new MenuResult(false, exitCode);
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
            .HighlightStyle(ConsoleUI.PromptBoldHighlightStyle);

        // Set disabled style for group headers (dimmed)
        prompt.DisabledStyle = ConsoleUI.GroupHeaderStyle;

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

    /// <summary>
    /// Prompts the user with a primary action, skip, and exit choices.
    /// </summary>
    /// <param name="primaryChoice">Label for the primary action (e.g. "Start Setup Wizard").</param>
    /// <returns>The selected choice string.</returns>
    private static string PromptForAction(string primaryChoice) =>
        AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]What would you like to do?[/]")
                .HighlightStyle(ConsoleUI.PromptHighlightStyle)
                .AddChoices([primaryChoice, "Skip (use menu instead)", "Exit"]));

    private int ExitWithGoodbye()
    {
        LogExiting(logger);
        AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");
        return 0;
    }

    private readonly record struct MenuResult(bool ShouldExit, int ExitCode);
}
