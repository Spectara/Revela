using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Revela;

/// <summary>
/// Setup wizard for first-time Revela configuration.
/// </summary>
/// <remarks>
/// <para>
/// The wizard is triggered automatically when revela.json doesn't exist
/// (fresh installation). It offers two modes:
/// </para>
/// <list type="bullet">
/// <item><b>Full Installation (recommended):</b> Installs all available themes and plugins</item>
/// <item><b>Custom Installation:</b> User selects specific packages to install</item>
/// </list>
/// <para>
/// After completion:
/// - If packages were installed: exits for plugin reload
/// - If all packages already installed: continues to menu
/// </para>
/// </remarks>
public sealed partial class Wizard(
    ILogger<Wizard> logger,
    RefreshCommand packagesRefreshCommand,
    ThemeInstallCommand themeInstallCommand,
    PluginInstallCommand pluginInstallCommand)
{
    /// <summary>
    /// Exit code indicating packages were installed and restart is required.
    /// </summary>
    public const int ExitCodeRestartRequired = 2;

    private const string FullInstallation = "full";
    private const string CustomInstallation = "custom";

    /// <summary>
    /// Runs the setup wizard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Exit code:
    /// 0 = completed, nothing new installed (continue to menu),
    /// 1 = error/cancelled,
    /// 2 = packages installed (restart required).
    /// </returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LogStartingWizard(logger);
        ShowWelcomeScreen();

        // Refresh package index
        AnsiConsole.MarkupLine("[dim]Refreshing package index...[/]");
        AnsiConsole.WriteLine();

        var refreshResult = await packagesRefreshCommand.RefreshAsync(cancellationToken);
        if (refreshResult != 0)
        {
            ShowRefreshFailedError();
            return 1;
        }

        // Get available packages
        var availableThemes = await themeInstallCommand.GetAvailableThemesAsync(cancellationToken);
        var availablePlugins = await pluginInstallCommand.GetAvailablePluginsAsync(cancellationToken);

        var totalAvailable = availableThemes.Count + availablePlugins.Count;

        // All already installed?
        if (totalAvailable == 0)
        {
            ShowAlreadyInstalledMessage();
            return 0;
        }

        // Show setup mode selection
        var mode = PromptSetupMode(availableThemes.Count, availablePlugins.Count);

        InstallResult themeResult;
        InstallResult pluginResult;

        if (mode == FullInstallation)
        {
            // Full installation - install everything
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Installing all packages...[/]");
            AnsiConsole.WriteLine();

            themeResult = await themeInstallCommand.InstallAllAsync(showRestartNotice: false, cancellationToken);
            pluginResult = await pluginInstallCommand.InstallAllAsync(showRestartNotice: false, cancellationToken);
        }
        else
        {
            // Custom installation - show combined multi-select
            var (selectedThemes, selectedPlugins) = PromptCustomSelection(availableThemes, availablePlugins);

            // Must have at least one theme
            if (selectedThemes.Count == 0)
            {
                var installedThemes = await GlobalConfigManager.GetThemesAsync(cancellationToken);
                if (installedThemes.Count == 0)
                {
                    ShowNoThemesError();
                    return 1;
                }
            }

            // Install selected packages
            themeResult = await InstallSelectedAsync(
                selectedThemes,
                themeInstallCommand.ExecuteAsync,
                cancellationToken);

            pluginResult = await InstallSelectedAsync(
                selectedPlugins,
                pluginInstallCommand.ExecuteFromNuGetAsync,
                cancellationToken);
        }

        // Determine if restart is needed
        var anythingInstalled = themeResult.HasInstalled || pluginResult.HasInstalled;

        // Done - show summary
        ShowCompletionSummary(themeResult, pluginResult);

        LogWizardCompleted(logger, themeResult.Installed.Count, pluginResult.Installed.Count);

        return anythingInstalled ? ExitCodeRestartRequired : 0;
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
                "[bold]Welcome to the Revela Setup Wizard![/]\n\n" +
                "This wizard will help you install themes and plugins.\n\n" +
                "[dim]You can re-run this wizard later via:[/] Addons → wizard"))
            .WithHeader("[cyan1]Setup[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string PromptSetupMode(int themeCount, int pluginCount)
    {
        var totalCount = themeCount + pluginCount;
        var themesText = themeCount == 1 ? "1 theme" : $"{themeCount} themes";
        var pluginsText = pluginCount == 1 ? "1 plugin" : $"{pluginCount} plugins";

        AnsiConsole.WriteLine();

        var fullLabel = $"[green]⭐ Full Installation[/] [dim](recommended)[/]\n   Install all {themesText} and {pluginsText}";
        var customLabel = "[blue]🔧 Custom Installation[/]\n   Choose which packages to install";

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]How would you like to set up Revela?[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(fullLabel, customLabel));

        return selection.Contains("Full", StringComparison.Ordinal) ? FullInstallation : CustomInstallation;
    }

    private static (List<string> Themes, List<string> Plugins) PromptCustomSelection(
        IReadOnlyList<PackageIndexEntry> availableThemes,
        IReadOnlyList<PackageIndexEntry> availablePlugins)
    {
        AnsiConsole.WriteLine();

        // Build choices with group structure
        var allChoices = new List<string>();
        var themeChoices = new List<string>();
        var pluginChoices = new List<string>();

        foreach (var theme in availableThemes)
        {
            var shortName = theme.Id.Replace("Spectara.Revela.Theme.", "", StringComparison.Ordinal);
            var choice = $"{theme.Id}|[cyan]Theme:[/] {shortName} [dim]- {Truncate(theme.Description, 40)}[/]";
            themeChoices.Add(choice);
            allChoices.Add(choice);
        }

        foreach (var plugin in availablePlugins)
        {
            var shortName = plugin.Id.Replace("Spectara.Revela.Plugin.", "", StringComparison.Ordinal);
            var choice = $"{plugin.Id}|[blue]Plugin:[/] {shortName} [dim]- {Truncate(plugin.Description, 40)}[/]";
            pluginChoices.Add(choice);
            allChoices.Add(choice);
        }

        var prompt = new MultiSelectionPrompt<string>()
            .Title("[cyan]Select packages to install:[/] [dim](Space to toggle, Enter to confirm)[/]")
            .PageSize(15)
            .Required(false)
            .HighlightStyle(new Style(Color.Cyan1))
            .InstructionsText("[dim](↑↓ navigate, Space toggle, a=all, Enter confirm)[/]");

        // Add themes group
        if (themeChoices.Count > 0)
        {
            prompt.AddChoiceGroup(
                "[yellow]── Themes ──[/]",
                [.. themeChoices.Select(c => c.Split('|')[1])]);
        }

        // Add plugins group
        if (pluginChoices.Count > 0)
        {
            prompt.AddChoiceGroup(
                "[yellow]── Plugins ──[/]",
                [.. pluginChoices.Select(c => c.Split('|')[1])]);
        }

        // Pre-select all items
        foreach (var choice in allChoices)
        {
            prompt.Select(choice.Split('|')[1]);
        }

        var selections = AnsiConsole.Prompt(prompt);

        // Map back to package IDs
        var selectedThemes = new List<string>();
        var selectedPlugins = new List<string>();

        foreach (var selection in selections)
        {
            // Skip group headers
            if (selection.Contains("── Themes ──", StringComparison.Ordinal) || selection.Contains("── Plugins ──", StringComparison.Ordinal))
            {
                continue;
            }

            // Find the original choice to get the package ID
            var originalChoice = allChoices.FirstOrDefault(c => c.Split('|')[1] == selection);
            if (originalChoice is not null)
            {
                var packageId = originalChoice.Split('|')[0];
                if (packageId.Contains(".Theme.", StringComparison.Ordinal))
                {
                    selectedThemes.Add(packageId);
                }
                else
                {
                    selectedPlugins.Add(packageId);
                }
            }
        }

        return (selectedThemes, selectedPlugins);
    }

    private static async Task<InstallResult> InstallSelectedAsync(
        List<string> packageIds,
        Func<string, string?, string?, CancellationToken, Task<int>> installFunc,
        CancellationToken cancellationToken)
    {
        if (packageIds.Count == 0)
        {
            return InstallResult.Empty;
        }

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var packageId in packageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await installFunc(packageId, null, null, cancellationToken);
            if (result == 0)
            {
                installed.Add(packageId);
            }
            else
            {
                failed.Add(packageId);
            }
        }

        return new InstallResult(installed, [], failed);
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }

    private static void ShowRefreshFailedError()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to refresh package index.");
        AnsiConsole.MarkupLine("[dim]Check your network connection and try again.[/]");
    }

    private static void ShowNoThemesError()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{OutputMarkers.Error} No theme selected. Setup incomplete.");
        AnsiConsole.MarkupLine("[dim]At least one theme is required to generate websites.[/]");
        AnsiConsole.MarkupLine("[dim]Run 'revela' again to restart the setup wizard.[/]");
    }

    private static void ShowAlreadyInstalledMessage()
    {
        AnsiConsole.WriteLine();

        var lines = new List<string>
        {
            "[green]✓ All packages already installed![/]",
            "",
            "All available themes and plugins are already set up.",
            "",
            "You're ready to use Revela!",
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

    private static void ShowCompletionSummary(InstallResult themeResult, InstallResult pluginResult)
    {
        AnsiConsole.WriteLine();

        var anythingInstalled = themeResult.HasInstalled || pluginResult.HasInstalled;

        if (anythingInstalled)
        {
            // Build detailed list of installed packages
            var lines = new List<string> { "[green]✓ Setup completed successfully![/]", "" };

            if (themeResult.HasInstalled)
            {
                lines.Add("[bold]Installed themes:[/]");
                foreach (var theme in themeResult.Installed)
                {
                    var shortName = theme.Replace("Spectara.Revela.Theme.", "", StringComparison.Ordinal);
                    lines.Add($"  [cyan]•[/] {shortName}");
                }

                lines.Add("");
            }

            if (pluginResult.HasInstalled)
            {
                lines.Add("[bold]Installed plugins:[/]");
                foreach (var plugin in pluginResult.Installed)
                {
                    var shortName = plugin.Replace("Spectara.Revela.Plugin.", "", StringComparison.Ordinal);
                    lines.Add($"  [cyan]•[/] {shortName}");
                }

                lines.Add("");
            }

            lines.Add("[bold]Please restart Revela to load the new packages.[/]");

            var panel = new Panel(new Markup(string.Join("\n", lines)))
                .WithHeader("[green]Complete[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Green))
                .Padding(1, 0);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
        }
        else
        {
            // Nothing new installed (user selected nothing or all were already installed)
            var lines = new List<string>
            {
                "[green]✓ Setup completed![/]",
                "",
                "No new packages were installed.",
                "",
                "You're ready to use Revela!",
            };

            var panel = new Panel(new Markup(string.Join("\n", lines)))
                .WithHeader("[green]Complete[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Green))
                .Padding(1, 0);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        }

        Console.ReadKey(intercept: true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting setup wizard")]
    private static partial void LogStartingWizard(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setup wizard completed: {ThemesInstalled} themes, {PluginsInstalled} plugins installed")]
    private static partial void LogWizardCompleted(ILogger logger, int themesInstalled, int pluginsInstalled);
}
