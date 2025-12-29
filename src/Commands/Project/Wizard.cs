using Spectara.Revela.Commands.Config.Images;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Commands.Config.Theme;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

using Spectre.Console;

namespace Spectara.Revela.Commands.Project;

/// <summary>
/// Project setup wizard for creating a new Revela project.
/// </summary>
/// <remarks>
/// <para>
/// The wizard is triggered when project.json doesn't exist in the current directory.
/// It orchestrates existing config commands to:
/// </para>
/// <list type="number">
/// <item>Configure project settings (name, URL)</item>
/// <item>Select a theme from installed themes</item>
/// <item>Configure site metadata (title, author, etc.)</item>
/// <item>Optionally configure plugin-provided steps (OneDrive, etc.)</item>
/// </list>
/// <para>
/// After completion, the project is ready for adding images and generating the site.
/// </para>
/// </remarks>
public sealed partial class Wizard(
    ILogger<Wizard> logger,
    ConfigProjectCommand configProjectCommand,
    ConfigThemeCommand configThemeCommand,
    ConfigImageCommand configImageCommand,
    ConfigSiteCommand configSiteCommand,
    IEnumerable<IWizardStep> wizardSteps)
{
    /// <summary>
    /// Runs the project setup wizard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code: 0 = success, 1 = error/cancelled.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LogStartingWizard(logger);
        ShowWelcomeScreen();

        // Step 1: Configure project (creates project.json, source/, output/)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 1/4: Project Settings ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Configure your project name and base URL.[/]");
        AnsiConsole.WriteLine();

        var projectResult = await configProjectCommand.ExecuteAsync(null, null, cancellationToken);
        if (projectResult != 0)
        {
            ShowStepFailedError("project configuration");
            return 1;
        }

        // Step 2: Select theme
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 2/4: Select Theme ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Choose a theme for your site.[/]");
        AnsiConsole.WriteLine();

        var themeResult = await configThemeCommand.ExecuteAsync(null, cancellationToken);
        if (themeResult != 0)
        {
            ShowStepFailedError("theme selection");
            return 1;
        }

        // Step 3: Configure image settings (uses theme defaults)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 3/4: Image Settings ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Configure output formats and sizes for your images.[/]");
        AnsiConsole.WriteLine();

        var imageResult = await configImageCommand.ExecuteAsync(null, null, cancellationToken);
        if (imageResult != 0)
        {
            ShowStepFailedError("image configuration");
            return 1;
        }

        // Step 4: Configure site metadata
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 4/4: Site Metadata ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Configure your site title, author, and other metadata.[/]");
        AnsiConsole.WriteLine();

        // IOptionsMonitor cache was invalidated by ConfigThemeCommand, so theme is available
        var siteResult = await configSiteCommand.ExecuteAsync(cancellationToken);
        if (siteResult != 0)
        {
            ShowStepFailedError("site configuration");
            return 1;
        }

        // Optional: Run plugin-provided wizard steps
        await RunOptionalWizardStepsAsync(cancellationToken);

        // Done - show summary
        ShowCompletionSummary();

        LogWizardCompleted(logger);

        return 0;
    }

    /// <summary>
    /// Prompts the user to optionally run plugin-provided wizard steps.
    /// </summary>
    private async Task RunOptionalWizardStepsAsync(CancellationToken cancellationToken)
    {
        // Get available wizard steps that should be prompted
        var availableSteps = wizardSteps
            .Where(step => step.ShouldPrompt())
            .OrderBy(step => step.Order)
            .ToList();

        if (availableSteps.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Optional Configuration ━━━[/]");
        AnsiConsole.MarkupLine("[dim]The following plugins can be configured now (or later via revela config).[/]");
        AnsiConsole.WriteLine();

        // Show multi-select prompt
        var selectedSteps = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IWizardStep>()
                .Title("Select optional configurations:")
                .NotRequired()
                .PageSize(10)
                .InstructionsText("[dim](Press [cyan]<space>[/] to toggle, [cyan]<enter>[/] to continue)[/]")
                .UseConverter(step => $"{step.Name} [dim]- {step.Description}[/]")
                .AddChoices(availableSteps));

        if (selectedSteps.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Skipping optional configuration.[/]");
            return;
        }

        // Run selected steps
        foreach (var step in selectedSteps.OrderBy(s => s.Order))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]━━━ {step.Name} ━━━[/]");
            AnsiConsole.MarkupLine($"[dim]{step.Description}[/]");
            AnsiConsole.WriteLine();

            var result = await step.ExecuteAsync(cancellationToken);
            if (result != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] {step.Name} configuration was not completed.");
                AnsiConsole.MarkupLine($"[dim]You can configure it later via: revela config[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓[/] {step.Name} configured");
            }
        }
    }

    private static void ShowWelcomeScreen()
    {
        AnsiConsole.Clear();

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

        var panel = new Panel(
            new Markup(
                "[bold]Create a New Revela Project[/]\n\n" +
                "This wizard will help you set up a new photo gallery:\n" +
                "  [cyan]1.[/] Project settings (name, URL)\n" +
                "  [cyan]2.[/] Select a theme\n" +
                "  [cyan]3.[/] Image settings (formats, sizes)\n" +
                "  [cyan]4.[/] Site metadata (title, author)\n\n" +
                "[dim]You can change these settings later via:[/] revela config"))
            .WithHeader("[cyan1]New Project[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }

    private static void ShowStepFailedError(string step)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]✗[/] Failed to complete {step}.");
        AnsiConsole.MarkupLine("[dim]Please try again or use individual config commands.[/]");
    }

    private static void ShowCompletionSummary()
    {
        AnsiConsole.WriteLine();

        var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

        var lines = new List<string>
        {
            "[green]✓ Project created successfully![/]",
            "",
            "[bold]Next steps:[/]",
            "  [cyan]1.[/] Add images to the [bold]source/[/] folder",
            "  [cyan]2.[/] Run [bold]revela generate[/] to build your site",
            "  [cyan]3.[/] View your site in [bold]output/[/]",
            "",
            "[dim]Tip: Create subfolders in source/ to organize galleries[/]",
        };

        var panel = new Panel(new Markup(string.Join("\n", lines)))
            .WithHeader("[green]Complete[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Green))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");

        Console.ReadKey(intercept: true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting project setup wizard")]
    private static partial void LogStartingWizard(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project setup wizard completed")]
    private static partial void LogWizardCompleted(ILogger logger);
}
