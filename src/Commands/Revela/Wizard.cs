using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Revela;

/// <summary>
/// Setup wizard for first-time Revela configuration.
/// </summary>
/// <remarks>
/// Uses <see cref="IPackageIndexService"/> to discover available themes and plugins,
/// and <see cref="PluginManager"/> directly for installation (bypasses type checks
/// in PluginInstallCommand so both themes and plugins can be installed).
/// </remarks>
internal sealed partial class Wizard(
    ILogger<Wizard> logger,
    IPackageIndexService packageIndexService,
    RefreshCommand packagesRefreshCommand,
    PluginManager pluginManager,
    IGlobalConfigManager globalConfigManager)
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

        // Get available packages directly from package index (no plugin dependency)
        var availableThemes = await packageIndexService.SearchByTypeAsync("RevelaTheme", cancellationToken);
        var availablePlugins = await packageIndexService.SearchByTypeAsync("RevelaPlugin", cancellationToken);

        var totalAvailable = availableThemes.Count + availablePlugins.Count;

        // All already installed?
        if (totalAvailable == 0)
        {
            ShowAlreadyInstalledMessage();
            return 0;
        }

        // Show setup mode selection
        var mode = PromptSetupMode(availableThemes.Count, availablePlugins.Count);

        // Core plugins are always installed — not user-selectable
        var corePlugins = availablePlugins
            .Where(p => p.Id.Contains(".Plugins.Core.", StringComparison.Ordinal))
            .ToList();
        var optionalPlugins = availablePlugins
            .Where(p => !p.Id.Contains(".Plugins.Core.", StringComparison.Ordinal))
            .ToList();

        InstallResult themeResult;
        InstallResult pluginResult;

        if (mode == FullInstallation)
        {
            // Full installation - install everything
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Installing all packages...[/]");
            AnsiConsole.WriteLine();

            themeResult = await InstallThemesAsync(availableThemes, cancellationToken);
            pluginResult = await InstallPackagesAsync(availablePlugins, cancellationToken);
        }
        else
        {
            // Custom installation — core plugins auto-installed, user selects the rest
            if (corePlugins.Count > 0)
            {
                ShowCorePluginsInfo(corePlugins);
            }

            var (selectedThemes, selectedPlugins) = PromptCustomSelection(availableThemes, optionalPlugins);

            // Must have at least one theme
            if (selectedThemes.Count == 0)
            {
                var installedThemes = await globalConfigManager.GetThemesAsync(cancellationToken);
                if (installedThemes.Count == 0)
                {
                    ShowNoThemesError();
                    return 1;
                }
            }

            // Always install core plugins first
            var coreResult = await InstallPackagesAsync(corePlugins, cancellationToken);

            // Install user-selected packages
            themeResult = await InstallSelectedAsync(
                selectedThemes,
                InstallThemeAsync,
                cancellationToken);

            var optionalResult = await InstallSelectedAsync(
                selectedPlugins,
                InstallPackageAsync,
                cancellationToken);

            // Merge core + optional plugin results
            pluginResult = new InstallResult(
                [.. coreResult.Installed, .. optionalResult.Installed],
                [.. coreResult.AlreadyInstalled, .. optionalResult.AlreadyInstalled],
                [.. coreResult.Failed, .. optionalResult.Failed]);
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
            .WithInfoStyle()
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
            var shortName = theme.Id.Replace("Spectara.Revela.Themes.", "", StringComparison.Ordinal);
            var choice = $"{theme.Id}|[cyan]Theme:[/] {shortName} [dim]- {Truncate(theme.Description, 40)}[/]";
            themeChoices.Add(choice);
            allChoices.Add(choice);
        }

        foreach (var plugin in availablePlugins)
        {
            var shortName = plugin.Id.Replace("Spectara.Revela.Plugins.", "", StringComparison.Ordinal);
            var choice = $"{plugin.Id}|[blue]Plugin:[/] {shortName} [dim]- {Truncate(plugin.Description, 40)}[/]";
            pluginChoices.Add(choice);
            allChoices.Add(choice);
        }

        var prompt = new MultiSelectionPrompt<string>()
            .Title("[cyan]Select packages to install:[/] [dim](Space to toggle, Enter to confirm)[/]")
            .PageSize(15)
            .Required(false)
            .HighlightStyle(new Style(Color.Cyan1))
            .InstructionsText("[dim](↑↓ navigate, Space toggle, a=all, Enter confirm)[/]")
            .AddChoices([.. themeChoices.Select(c => c.Split('|')[1])])
            .AddChoices([.. pluginChoices.Select(c => c.Split('|')[1])]);

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
            // Find the original choice to get the package ID
            var originalChoice = allChoices.FirstOrDefault(c => c.Split('|')[1] == selection);
            if (originalChoice is not null)
            {
                var packageId = originalChoice.Split('|')[0];
                if (packageId.Contains(".Themes.", StringComparison.Ordinal))
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

    /// <summary>
    /// Installs all available themes using PluginManager directly.
    /// </summary>
    private async Task<InstallResult> InstallThemesAsync(
        IReadOnlyList<PackageIndexEntry> themes,
        CancellationToken cancellationToken)
    {
        if (themes.Count == 0)
        {
            return InstallResult.Empty;
        }

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var theme in themes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await InstallThemeAsync(theme.Id, null, null, cancellationToken);
            if (result == 0)
            {
                installed.Add(theme.Id);
            }
            else
            {
                failed.Add(theme.Id);
            }
        }

        return new InstallResult(installed, [], failed);
    }

    /// <summary>
    /// Installs all available plugins using PluginManager directly.
    /// </summary>
    private async Task<InstallResult> InstallPackagesAsync(
        IReadOnlyList<PackageIndexEntry> packages,
        CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
        {
            return InstallResult.Empty;
        }

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await InstallPackageAsync(package.Id, null, null, cancellationToken);
            if (result == 0)
            {
                installed.Add(package.Id);
            }
            else
            {
                failed.Add(package.Id);
            }
        }

        return new InstallResult(installed, [], failed);
    }

    /// <summary>
    /// Installs a single theme: NuGet install + register in global config.
    /// </summary>
    private async Task<int> InstallThemeAsync(
        string packageId,
        string? version,
        string? source,
        CancellationToken cancellationToken)
    {
        var success = await pluginManager.InstallAsync(packageId, version, source, cancellationToken);
        if (success)
        {
            await globalConfigManager.AddThemeAsync(packageId, version ?? "latest", cancellationToken);
        }

        return success ? 0 : 1;
    }

    /// <summary>
    /// Installs a single plugin via PluginManager.
    /// </summary>
    private async Task<int> InstallPackageAsync(
        string packageId,
        string? version,
        string? source,
        CancellationToken cancellationToken)
    {
        var success = await pluginManager.InstallAsync(packageId, version, source, cancellationToken);
        return success ? 0 : 1;
    }

    private static void ShowCorePluginsInfo(List<PackageIndexEntry> corePlugins)
    {
        AnsiConsole.WriteLine();

        var lines = new List<string> { "[bold]Core plugins[/] [dim](always installed):[/]", "" };
        foreach (var plugin in corePlugins)
        {
            var shortName = plugin.Id.Replace("Spectara.Revela.Plugins.Core.", "", StringComparison.Ordinal);
            lines.Add($"  [green]✓[/] {Markup.Escape(shortName)} [dim]- {Markup.Escape(Truncate(plugin.Description, 50))}[/]");
        }

        AnsiConsole.MarkupLine(string.Join("\n", lines));
        AnsiConsole.WriteLine();
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
            .WithSuccessStyle()
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
                    var shortName = theme.Replace("Spectara.Revela.Themes.", "", StringComparison.Ordinal);
                    lines.Add($"  [cyan]•[/] {shortName}");
                }

                lines.Add("");
            }

            if (pluginResult.HasInstalled)
            {
                lines.Add("[bold]Installed plugins:[/]");
                foreach (var plugin in pluginResult.Installed)
                {
                    var shortName = plugin.Replace("Spectara.Revela.Plugins.", "", StringComparison.Ordinal);
                    lines.Add($"  [cyan]•[/] {shortName}");
                }

                lines.Add("");
            }

            lines.Add("[bold]Please restart Revela to load the new packages.[/]");

            var panel = new Panel(new Markup(string.Join("\n", lines)))
                .WithHeader("[green]Complete[/]")
                .WithSuccessStyle()
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
                .WithSuccessStyle()
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
