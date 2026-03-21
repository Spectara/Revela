using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Commands.Config.Site;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Commands.Project;

/// <summary>
/// Project setup wizard for creating a new Revela project.
/// </summary>
/// <remarks>
/// <para>
/// The wizard is triggered when project.json doesn't exist in the current directory.
/// It runs required <see cref="IWizardStep"/> implementations from plugins in Order sequence,
/// then offers optional steps as checkboxes.
/// </para>
/// <para>
/// This approach keeps the host completely decoupled from plugin types.
/// Plugins register their wizard steps via DI, and the wizard discovers them at runtime.
/// </para>
/// </remarks>
internal sealed partial class Wizard(
    ILogger<Wizard> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    ConfigProjectCommand configProjectCommand,
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

        // Collect required and optional steps from plugins
        var requiredSteps = wizardSteps
            .Where(s => s.IsRequired && s.ShouldPrompt())
            .OrderBy(s => s.Order)
            .ToList();

        var optionalSteps = wizardSteps
            .Where(s => !s.IsRequired && s.ShouldPrompt())
            .OrderBy(s => s.Order)
            .ToList();

        // Total steps = 1 (project) + required plugin steps + 1 (site)
        var totalSteps = 2 + requiredSteps.Count;
        var currentStep = 1;

        // Step 1: Configure project (creates project.json) — always host-owned
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]━━━ Step {currentStep}/{totalSteps}: Project Settings ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Configure your project name and base URL.[/]");
        AnsiConsole.WriteLine();

        var projectResult = await configProjectCommand.ExecuteAsync(null, null, cancellationToken);
        if (projectResult != 0)
        {
            ShowStepFailedError("project configuration");
            return 1;
        }

        currentStep++;

        // Required plugin steps (paths, theme, images — in Order sequence)
        foreach (var step in requiredSteps)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]━━━ Step {currentStep}/{totalSteps}: {Markup.Escape(step.Name)} ━━━[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(step.Description)}.[/]");
            AnsiConsole.WriteLine();

            var result = await step.ExecuteAsync(cancellationToken);
            if (result != 0)
            {
                ShowStepFailedError(step.Name);
                return 1;
            }

            currentStep++;
        }

        // Last required step: Configure site metadata — always host-owned
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]━━━ Step {currentStep}/{totalSteps}: Site Metadata ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Configure your site title, author, and other metadata.[/]");
        AnsiConsole.WriteLine();

        var siteResult = await configSiteCommand.ExecuteAsync(cancellationToken);
        if (siteResult != 0)
        {
            ShowStepFailedError("site configuration");
            return 1;
        }

        // Optional: Run plugin-provided optional wizard steps
        await RunOptionalWizardStepsAsync(optionalSteps, cancellationToken);

        // Done - show summary
        ShowCompletionSummary();

        LogWizardCompleted(logger);

        return 0;
    }

    /// <summary>
    /// Runs optional plugin-provided wizard steps as checkboxes.
    /// </summary>
    private static async Task RunOptionalWizardStepsAsync(
        List<IWizardStep> availableSteps,
        CancellationToken cancellationToken)
    {
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
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {step.Name} configuration was not completed.");
                AnsiConsole.MarkupLine($"[dim]You can configure it later via: revela config[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} {step.Name} configured");
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
        AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to complete {step}.");
        AnsiConsole.MarkupLine("[dim]Please try again or use individual config commands.[/]");
    }

    private void ShowCompletionSummary()
    {
        AnsiConsole.WriteLine();

        var folderName = projectEnvironment.Value.FolderName;

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
