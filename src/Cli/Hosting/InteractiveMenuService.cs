using System.CommandLine;
using System.Reflection;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Service for running the interactive CLI menu.
/// </summary>
internal sealed partial class InteractiveMenuService(
    CommandPromptBuilder promptBuilder,
    CommandOrderRegistry orderRegistry,
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
            AnsiConsole.MarkupLine("[red]Error: RootCommand not set.[/]");
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

        // ASCII logo
        var logoLines = new[]
        {
            @" ____                _       ",
            @"|  _ \ _____   _____| | __ _ ",
            @"| |_) / _ \ \ / / _ \ |/ _` |",
            @"|  _ <  __/\ V /  __/ | (_| |",
            @"|_| \_\___| \_/ \___|_|\__,_|",
        };

        foreach (var line in logoLines)
        {
            AnsiConsole.MarkupLine("[cyan]" + line + "[/]");
        }

        AnsiConsole.WriteLine();

        // Version and description panel
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";

        var panel = new Panel(
            new Markup(
                $"[bold]Version {version}[/]\n" +
                "[dim]Modern static site generator for photographers[/]\n\n" +
                "[blue]Navigate with[/] [bold]↑↓[/][blue], select with[/] [bold]Enter[/]"))
            .Header("[cyan]Welcome[/]")
            .HeaderAlignment(Justify.Center)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
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
        var choices = new List<MenuChoice>();

        // Add all top-level commands (sorted by order, then name)
        foreach (var command in orderRegistry.Sort(RootCommand!.Subcommands))
        {
            choices.Add(MenuChoice.FromCommand(command));
        }

        // Add separator and exit
        choices.Add(MenuChoice.Exit);

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<MenuChoice>()
                .Title("\n[cyan]What would you like to do?[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .AddChoices(choices));

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
            var choices = new List<MenuChoice> { MenuChoice.Back };

            foreach (var command in orderRegistry.Sort(parent.Subcommands))
            {
                choices.Add(MenuChoice.FromCommand(command));
            }

            choices.Add(MenuChoice.Exit);

            var pathDisplay = string.Join(" → ", commandPath);
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<MenuChoice>()
                    .Title($"\n[cyan]{pathDisplay}[/] - [dim]Select a command:[/]")
                    .PageSize(15)
                    .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                    .AddChoices(choices));

            switch (selection.Action)
            {
                case MenuAction.Exit:
                    return new MenuResult(true, 0);

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
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            exitCode = 1;
        }

        // Show result
        AnsiConsole.WriteLine();
        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Command completed successfully[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Command failed with exit code {exitCode}[/]");
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
            .Header("[yellow]⚠ Not Available[/]")
            .HeaderAlignment(Justify.Center)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    private static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(intercept: true);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "RootCommand not set")]
    private static partial void LogRootCommandNotSet(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exiting interactive mode")]
    private static partial void LogExiting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command '{CommandPath}' failed")]
    private static partial void LogCommandFailed(ILogger logger, string commandPath, Exception ex);

    private readonly record struct MenuResult(bool ShouldExit, int ExitCode);
}
