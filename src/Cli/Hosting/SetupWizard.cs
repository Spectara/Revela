using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Setup wizard for first-time Revela configuration.
/// </summary>
/// <remarks>
/// <para>
/// The wizard is triggered automatically when revela.json doesn't exist
/// (fresh installation). It orchestrates existing commands to:
/// </para>
/// <list type="number">
/// <item>Refresh package index from all feeds</item>
/// <item>Install themes (at least one required)</item>
/// <item>Install plugins (optional)</item>
/// </list>
/// <para>
/// After completion:
/// - If packages were installed: exits for plugin reload
/// - If all packages already installed: continues to menu
/// </para>
/// <para>
/// Feed configuration is available via 'revela config feed' commands.
/// </para>
/// </remarks>
internal sealed partial class SetupWizard(
    ILogger<SetupWizard> logger,
    RefreshCommand packagesRefreshCommand,
    ThemeInstallCommand themeInstallCommand,
    PluginInstallCommand pluginInstallCommand)
{
    /// <summary>
    /// Exit code indicating packages were installed and restart is required.
    /// </summary>
    public const int ExitCodeRestartRequired = 2;

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

        // Refresh package index silently
        AnsiConsole.MarkupLine("[dim]Refreshing package index...[/]");
        AnsiConsole.WriteLine();

        var refreshResult = await packagesRefreshCommand.RefreshAsync(cancellationToken);
        if (refreshResult != 0)
        {
            ShowRefreshFailedError();
            return 1;
        }

        // Step 1: Install themes (required)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 1/2: Install Themes ━━━[/]");
        AnsiConsole.MarkupLine("[dim]At least one theme is required to generate websites.[/]");
        AnsiConsole.WriteLine();

        var themeResult = await themeInstallCommand.InstallInteractiveAsync(cancellationToken);

        // Check if we have any themes (either just installed or already installed)
        if (!themeResult.HasInstalled && !themeResult.AllAlreadyInstalled)
        {
            ShowNoThemesError();
            return 1;
        }

        // Step 2: Install plugins (optional)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]━━━ Step 2/2: Install Plugins (Optional) ━━━[/]");
        AnsiConsole.MarkupLine("[dim]Plugins add extra functionality. Select none to skip.[/]");
        AnsiConsole.WriteLine();

        var pluginResult = await pluginInstallCommand.InstallInteractiveAsync(cancellationToken);

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
                "This wizard will help you configure Revela for first use:\n" +
                "  [cyan]1.[/] Install a theme (required)\n" +
                "  [cyan]2.[/] Install plugins (optional)\n\n" +
                "[dim]You can re-run this wizard later via:[/] Setup → Setup Wizard"))
            .WithHeader("[cyan1]Setup[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void ShowRefreshFailedError()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[red]✗[/] Failed to refresh package index.");
        AnsiConsole.MarkupLine("[dim]Check your network connection and try again.[/]");
    }

    private static void ShowNoThemesError()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[red]✗[/] No theme installed. Setup incomplete.");
        AnsiConsole.MarkupLine("[dim]At least one theme is required to generate websites.[/]");
        AnsiConsole.MarkupLine("[dim]Run 'revela' again to restart the setup wizard.[/]");
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
                    // Extract short name from full package ID
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
            // Nothing new installed
            var lines = new List<string>
            {
                "[green]✓ Setup completed![/]",
                "",
                "All themes and plugins were already installed.",
                "",
                "You're ready to use Revela!"
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
