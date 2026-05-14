using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;
using ProjectWizard = Spectara.Revela.Commands.Project.Wizard;

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
    IGlobalConfigManager globalConfigManager,
    IPackageContext packageContext,
    IEnumerable<ISetupWizard> setupWizards,
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
        // Skip if plugins are already loaded (development mode via ProjectReferences)
        if (!globalConfigManager.ConfigFileExists() && packageContext.Plugins.Count == 0)
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
        ConsoleUI.ClearConsole();
        ConsoleUI.ShowFirstRunPanel();

        var wizard = setupWizards.FirstOrDefault();
        if (wizard is null)
        {
            // No setup wizard available (embedded mode) — skip to menu
            return await ContinueToMenuAsync(cancellationToken);
        }

        var choice = PromptForAction("Start Setup Wizard");

        if (choice == "Exit")
        {
            return ExitWithGoodbye();
        }

        if (choice == "Start Setup Wizard")
        {
            var result = await wizard.RunAsync(cancellationToken);

            // Exit code 2 = packages installed, restart required
            if (result == ISetupWizard.ExitCodeRestartRequired)
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
        ConsoleUI.ClearConsole();

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
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(wizardName)} was not completed. Continuing to menu...[/]");
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

        ConsoleUI.ClearConsole();

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
            includeSetupWizard: setupWizards.Any());

        var selection = AnsiConsole.Prompt(prompt);

        // Top-level dispatch: Exit/Back/Wizard handled here; Navigate/Execute
        // delegate to HandleMenuActionAsync so CommandPathOverride from inlined
        // entries (e.g. "Plugins" → ["info","plugins"]) is honored.
        return selection.Action switch
        {
            MenuAction.Exit => new MenuResult(true, 0),
            MenuAction.Back => new MenuResult(false, 0),
            MenuAction.RunSetupWizard => await RunSetupWizardAsync(cancellationToken),
            MenuAction.Navigate or MenuAction.Execute or _ => await HandleMenuActionAsync(selection, [], cancellationToken),
        };
    }

    private async Task<MenuResult> RunSetupWizardAsync(CancellationToken cancellationToken)
    {
        var wizard = setupWizards.FirstOrDefault();
        if (wizard is null)
        {
            return new MenuResult(false, 0);
        }

        var result = await wizard.RunAsync(cancellationToken);

        // Exit code 2 = packages installed, restart required
        if (result == ISetupWizard.ExitCodeRestartRequired)
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

            // Add pipeline legend if this parent has pipeline steps
            var hasPipelineSteps = parent.Subcommands.Any(c => orderRegistry.IsPipelineStep(c));
            var legend = hasPipelineSteps ? "\n[dim]  [cyan]●[/] = included in [bold]all[/][/]" : "";

            var prompt = BuildGroupedSelectionPrompt(
                parent.Subcommands,
                $"\n[cyan]{pathDisplay}[/] - [dim]Select a command:[/]{legend}",
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

        var extendedPath = selection.CommandPathOverride is not null
            ? [.. selection.CommandPathOverride]
            : new List<string>(commandPath) { selection.Command!.Name };

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

        // Calculate column width from longest command name (+ 2 for " →" arrow)
        var allCommands = grouped.SelectMany(g => g.Commands).ToList();
        var nameWidth = allCommands.Count > 0
            ? allCommands.Max(c => MaxRenderedNameLength(c)) + 2
            : 0;

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
                    var commandChoices = new List<MenuChoice>();
                    foreach (var cmd in groupCommands)
                    {
                        AddGroupCommandChoices(commandChoices, cmd, nameWidth);
                    }

                    // Add Wizard to the Addons group (as last item)
                    if (includeSetupWizard && groupName == CommandGroups.Addons)
                    {
                        commandChoices.Add(MenuChoice.CreateWizard(nameWidth));
                    }

                    prompt.AddChoiceGroup(groupChoice, [.. commandChoices]);
                }
                else
                {
                    // Ungrouped commands - add directly
                    foreach (var cmd in groupCommands)
                    {
                        AddUngroupedCommandChoices(prompt, cmd, nameWidth);
                    }
                }
            }
        }
        else
        {
            // No groups - just add sorted commands
            foreach (var cmd in orderRegistry.Sort(commands))
            {
                prompt.AddChoice(MenuChoice.FromCommand(cmd, orderRegistry.IsPipelineStep(cmd), nameWidth));
            }
        }

        // Add Exit at the end (only for top-level menu)
        if (includeExit)
        {
            prompt.AddChoice(MenuChoice.Exit);
        }

        return prompt;
    }

    /// <summary>
    /// Adds menu choices for a command appearing in a grouped section.
    /// Inlined parents are expanded into a virtual default-action entry plus
    /// each visible subcommand (with absolute path overrides).
    /// </summary>
    private void AddGroupCommandChoices(List<MenuChoice> choices, Command cmd, int nameWidth)
    {
        if (TryAddInlined(cmd, nameWidth, choices.Add))
        {
            return;
        }
        choices.Add(MenuChoice.FromCommand(cmd, orderRegistry.IsPipelineStep(cmd), nameWidth));
    }

    /// <summary>
    /// Adds menu choices for an ungrouped command (rare).
    /// Inlined parents behave the same way as in grouped sections.
    /// </summary>
    private void AddUngroupedCommandChoices(SelectionPrompt<MenuChoice> prompt, Command cmd, int nameWidth)
    {
        if (TryAddInlined(cmd, nameWidth, choice => prompt.AddChoice(choice)))
        {
            return;
        }
        prompt.AddChoice(MenuChoice.FromCommand(cmd, orderRegistry.IsPipelineStep(cmd), nameWidth));
    }

    private bool TryAddInlined(Command cmd, int nameWidth, Action<MenuChoice> add)
    {
        var label = orderRegistry.GetInlineDefaultActionLabel(cmd);
        if (label is null)
        {
            return false;
        }

        // Virtual default-action entry: label runs the parent without subcommand
        add(MenuChoice.CreateInlinedDefaultAction(cmd, label, nameWidth));

        // Each visible subcommand that itself has at least one visible sub-subcommand
        // (extension entries registered via ParentCommand: "<parent> <sub>").
        // Subcommands without any extension are skipped — the parent's default
        // action already covers their content (e.g. count/list summary).
        // Absolute path override (parent + sub) ensures correct CLI dispatch.
        foreach (var sub in cmd.Subcommands
            .Where(IsInlinableSub)
            .OrderBy(orderRegistry.GetOrder)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            add(MenuChoice.FromCommand(
                sub,
                orderRegistry.IsPipelineStep(sub),
                nameWidth,
                commandPathOverride: [cmd.Name, sub.Name]));
        }
        return true;
    }

    private static bool IsInlinableSub(Command sub) =>
        !sub.Hidden && sub.Subcommands.Any(s => !s.Hidden);

    /// <summary>
    /// Computes the rendered name length used for column alignment, accounting
    /// for the inline expansion of parents into a default-action label plus
    /// subcommand entries (each with the " →" arrow when navigable).
    /// </summary>
    private int MaxRenderedNameLength(Command cmd)
    {
        var label = orderRegistry.GetInlineDefaultActionLabel(cmd);
        if (label is null)
        {
            return cmd.Name.Length + (cmd.Subcommands.Any(s => !s.Hidden) ? 2 : 0);
        }

        var max = label.Length;
        foreach (var sub in cmd.Subcommands.Where(IsInlinableSub))
        {
            var len = sub.Name.Length + 2; // always navigable when shown
            if (len > max)
            {
                max = len;
            }
        }
        return max;
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



