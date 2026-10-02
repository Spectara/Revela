using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Commands.Packages;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Features.Packages.Commands.Revela;

/// <summary>
/// Setup wizard for first-time Revela configuration.
/// </summary>
/// <remarks>
/// Uses <see cref="IPackageIndexService"/> to discover available themes and plugins and
/// <see cref="PackageInstallService"/> to install them (same type check and declaration rule
/// as 'plugin install' and 'theme install').
/// </remarks>
internal sealed partial class Wizard(
    ILogger<Wizard> logger,
    IPackageIndexService packageIndexService,
    RefreshCommand packagesRefreshCommand,
    PackageInstallService installService) : ISetupWizard
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
        var (availableThemes, availablePlugins) = PartitionPackages(
            await packageIndexService.SearchByTypeAsync(PackageIds.ThemePackageType, cancellationToken),
            await packageIndexService.SearchByTypeAsync(PackageIds.PluginPackageType, cancellationToken));

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

            themeResult = await InstallSelectedAsync([.. availableThemes.Select(t => t.Id)], PackageIds.ThemePackageType, cancellationToken);
            pluginResult = await InstallSelectedAsync([.. availablePlugins.Select(p => p.Id)], PackageIds.PluginPackageType, cancellationToken);
        }
        else
        {
            // Custom installation — user selects themes and plugins
            var (selectedThemes, selectedPlugins) = PromptCustomSelection(availableThemes, availablePlugins);

            // Must have at least one theme
            if (selectedThemes.Count == 0 && !availableThemes.Any(theme => installService.IsInstalled(theme.Id)))
            {
                ShowNoThemesError();
                return 1;
            }

            // Install user-selected packages
            themeResult = await InstallSelectedAsync(
                selectedThemes,
                PackageIds.ThemePackageType,
                cancellationToken);

            pluginResult = await InstallSelectedAsync(
                selectedPlugins,
                PackageIds.PluginPackageType,
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

    /// <summary>
    /// Splits index entries into the themes and plugins the wizard may offer.
    /// </summary>
    /// <remarks>
    /// Only official <c>Spectara.Revela.*</c> packages are offered, even if the index
    /// service returns more.
    /// </remarks>
    internal static (IReadOnlyList<PackageIndexEntry> Themes, IReadOnlyList<PackageIndexEntry> Plugins) PartitionPackages(
        IReadOnlyList<PackageIndexEntry> themes,
        IReadOnlyList<PackageIndexEntry> plugins)
    {
        var officialThemes = themes
            .Where(p => PackageTrustPolicy.IsOfficialPackageId(p.Id))
            .ToList();
        var officialPlugins = plugins
            .Where(p => PackageTrustPolicy.IsOfficialPackageId(p.Id))
            .ToList();

        return (officialThemes, officialPlugins);
    }

    /// <summary>
    /// Builds the Spectre markup label shown for a package in the custom selection prompt.
    /// </summary>
    internal static string FormatChoiceLabel(PackageIndexEntry package, bool isTheme)
    {
        var description = Markup.Escape(Truncate(package.Description, 40));
        if (isTheme)
        {
            var themeName = Markup.Escape(PackageIds.ToShortName(package.Id));
            return $"[cyan]Theme:[/] {themeName} [dim]- {description}[/]";
        }

        var pluginName = Markup.Escape(PackageIds.ToShortName(package.Id));
        return $"[blue]Plugin:[/] {pluginName} [dim]- {description}[/]";
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
            var choice = $"{theme.Id}|{FormatChoiceLabel(theme, isTheme: true)}";
            themeChoices.Add(choice);
            allChoices.Add(choice);
        }

        foreach (var plugin in availablePlugins)
        {
            var choice = $"{plugin.Id}|{FormatChoiceLabel(plugin, isTheme: false)}";
            pluginChoices.Add(choice);
            allChoices.Add(choice);
        }

        var prompt = new MultiSelectionPrompt<string>()
            .Title("[cyan]Select packages to install:[/] [dim](Space to toggle, Enter to confirm)[/]")
            .PageSize(15)
            .Required(false)
            .HighlightStyle(new Style(Color.Cyan1))
            .InstructionsText("[dim](↑↓ navigate, Space toggle, a=all, Enter confirm)[/]")
            .AddChoices([.. themeChoices.Select(c => c.Split('|', 2)[1])])
            .AddChoices([.. pluginChoices.Select(c => c.Split('|', 2)[1])]);

        // Pre-select all items
        foreach (var choice in allChoices)
        {
            prompt.Select(choice.Split('|', 2)[1]);
        }

        var selections = AnsiConsole.Prompt(prompt);

        // Map back to package IDs
        var selectedThemes = new List<string>();
        var selectedPlugins = new List<string>();

        foreach (var selection in selections)
        {
            // Find the original choice to get the package ID
            var originalChoice = allChoices.FirstOrDefault(c => c.Split('|', 2)[1] == selection);
            if (originalChoice is not null)
            {
                var packageId = originalChoice.Split('|')[0];
                if (availableThemes.Any(theme => theme.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
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

    private async Task<InstallResult> InstallSelectedAsync(
        List<string> packageIds,
        string packageType,
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
            var result = await installService.InstallAsync(packageId, packageType, cancellationToken: cancellationToken);
            if (result.Status == PackageInstallStatus.Installed)
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
                    var shortName = PackageIds.ToShortName(theme);
                    lines.Add($"  [cyan]•[/] {Markup.Escape(shortName)}");
                }

                lines.Add("");
            }

            if (pluginResult.HasInstalled)
            {
                lines.Add("[bold]Installed plugins:[/]");
                foreach (var plugin in pluginResult.Installed)
                {
                    var shortName = PackageIds.ToShortName(plugin);
                    lines.Add($"  [cyan]•[/] {Markup.Escape(shortName)}");
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

