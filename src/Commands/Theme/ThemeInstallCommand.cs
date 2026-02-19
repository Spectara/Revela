using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Handles 'revela theme install' command.
/// </summary>
/// <remarks>
/// Installs themes from the package index. Before running, use
/// 'revela packages refresh' to update the index.
/// </remarks>
internal sealed partial class ThemeInstallCommand(
    ILogger<ThemeInstallCommand> logger,
    IPackageIndexService packageIndexService,
    PluginManager pluginManager)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("install", "Install a theme from NuGet");

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Theme name (e.g., 'Lumina' for Spectara.Revela.Theme.Lumina)",
            Arity = ArgumentArity.ZeroOrOne
        };
        command.Arguments.Add(nameArgument);

        var versionOption = new Option<string?>("--version", "-v")
        {
            Description = "Specific version to install (optional)"
        };
        command.Options.Add(versionOption);

        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "NuGet source name (from 'revela plugin source list') or URL"
        };
        command.Options.Add(sourceOption);

        var allOption = new Option<bool>("--all", "-a")
        {
            Description = "Install all available themes"
        };
        command.Options.Add(allOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var source = parseResult.GetValue(sourceOption);
            var all = parseResult.GetValue(allOption);

            // --all flag → install all available themes
            if (all)
            {
                var result = await InstallAllAsync(showRestartNotice: true, cancellationToken);
                return result.HasFailures ? 1 : 0;
            }

            // No name provided → show interactive multi-selection
            if (string.IsNullOrEmpty(name))
            {
                var result = await InstallInteractiveAsync(showRestartNotice: true, cancellationToken);
                return result.HasFailures ? 1 : 0;
            }

            return await ExecuteAsync(name, version, source, cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Installs all available themes (for --all flag).
    /// </summary>
    /// <param name="showRestartNotice">Whether to show a prominent restart notice (false when called from wizard).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing installed, already-installed, and failed packages.</returns>
    public async Task<InstallResult> InstallAllAsync(bool showRestartNotice = true, CancellationToken cancellationToken = default)
    {
        var themes = await packageIndexService.SearchByTypeAsync("RevelaTheme", cancellationToken);

        if (themes.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No themes found in package index.");
            AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
            return InstallResult.Empty;
        }

        // Get already installed themes to filter them out
        var installedThemes = await GlobalConfigManager.GetThemesAsync(cancellationToken);
        var installedIds = installedThemes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableThemes = themes.Where(t => !installedIds.Contains(t.Id)).ToList();

        if (availableThemes.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All available themes are already installed.");
            return new InstallResult([], [.. installedThemes.Keys], []);
        }

        AnsiConsole.MarkupLine($"Installing [cyan]{availableThemes.Count}[/] theme(s)...");
        AnsiConsole.WriteLine();

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var theme in availableThemes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecuteAsync(theme.Id, version: null, source: null, cancellationToken);
            if (result == 0)
            {
                installed.Add(theme.Id);
            }
            else
            {
                failed.Add(theme.Id);
            }
        }

        // Show summary
        AnsiConsole.WriteLine();
        if (failed.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {installed.Count} themes installed successfully.");
        }
        else
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {installed.Count} of {availableThemes.Count} themes installed.");
        }

        // Show prominent restart notice if packages were installed
        if (showRestartNotice && installed.Count > 0)
        {
            InstallCommandHelper.ShowRestartNotice("themes");
        }

        return new InstallResult(installed, [.. installedIds], failed);
    }

    /// <summary>
    /// Installs themes interactively (shows multi-select prompt).
    /// </summary>
    /// <param name="showRestartNotice">Whether to show a prominent restart notice (false when called from wizard).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing installed, already-installed, and failed packages.</returns>
    public async Task<InstallResult> InstallInteractiveAsync(bool showRestartNotice = true, CancellationToken cancellationToken = default)
    {
        var selectedThemes = await SelectThemesInteractivelyAsync(cancellationToken);
        if (selectedThemes.Count == 0)
        {
            // Check if all themes are already installed
            var installedThemes = await GlobalConfigManager.GetThemesAsync(cancellationToken);
            if (installedThemes.Count > 0)
            {
                return new InstallResult([], [.. installedThemes.Keys], []);
            }

            return InstallResult.Empty;
        }

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var themeId in selectedThemes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecuteAsync(themeId, version: null, source: null, cancellationToken);
            if (result == 0)
            {
                installed.Add(themeId);
            }
            else
            {
                failed.Add(themeId);
            }
        }

        // Show summary if multiple themes
        if (selectedThemes.Count > 1)
        {
            AnsiConsole.WriteLine();
            if (failed.Count == 0)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {installed.Count} themes installed successfully.");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {installed.Count} of {selectedThemes.Count} themes installed.");
            }
        }

        // Show prominent restart notice if packages were installed
        if (showRestartNotice && installed.Count > 0)
        {
            InstallCommandHelper.ShowRestartNotice("themes");
        }

        return new InstallResult(installed, [], failed);
    }

    private async Task<IReadOnlyList<string>> SelectThemesInteractivelyAsync(CancellationToken cancellationToken)
    {
        var themes = await packageIndexService.SearchByTypeAsync("RevelaTheme", cancellationToken);

        if (themes.Count == 0)
        {
            var indexAge = packageIndexService.GetIndexAge();
            if (indexAge is null)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Package index not found.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No themes found in package index.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
            }

            return [];
        }

        // Get already installed themes to filter them out
        var installedThemes = await GlobalConfigManager.GetThemesAsync(cancellationToken);
        var installedIds = installedThemes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Filter out already installed themes
        var availableThemes = themes.Where(t => !installedIds.Contains(t.Id)).ToList();

        if (availableThemes.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All available themes are already installed.");
            return [];
        }

        // Show info about installed themes
        if (installedThemes.Count > 0)
        {
            AnsiConsole.MarkupLine("[green]Already installed:[/]");
            foreach (var themeId in installedThemes.Keys)
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Success} {themeId}");
            }

            AnsiConsole.WriteLine();
        }

        // Theme is required if none are installed yet
        var isRequired = installedThemes.Count == 0;

        var choices = availableThemes
            .Select(t => $"{t.Id} [dim]({t.Version})[/] - {InstallCommandHelper.Truncate(t.Description, 40)}")
            .ToList();

        var prompt = new MultiSelectionPrompt<string>()
            .Title(isRequired
                ? "[cyan]Select at least one theme to install:[/] [dim](Space to select, Enter to confirm)[/]"
                : "[cyan]Select themes to install:[/] [dim](Space to select, Enter to confirm)[/]")
            .PageSize(15)
            .Required(isRequired)
            .HighlightStyle(new Style(Color.Cyan1))
            .InstructionsText("[dim](↑↓ navigate, Space select, Enter confirm)[/]")
            .AddChoices(InstallCommandHelper.SelectAllChoice)
            .AddChoices(choices);

        var selections = AnsiConsole.Prompt(prompt);

        // Handle "Select all" option
        if (selections.Contains(InstallCommandHelper.SelectAllChoice))
        {
            return [.. availableThemes.Select(t => t.Id)];
        }

        // Extract package IDs from selections (before first space)
        return [.. selections.Select(s => s.Split(' ')[0])];
    }

    internal async Task<int> ExecuteAsync(
        string name,
        string? version,
        string? source,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "Lumina" → "Spectara.Revela.Theme.Lumina"
            //           "Spectara.Revela.Theme.Lumina" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Theme.{name}";

            // Check if package is in the index
            var packageEntry = await packageIndexService.FindPackageAsync(packageId, cancellationToken);

            if (packageEntry is null)
            {
                // Check if index exists
                var indexAge = packageIndexService.GetIndexAge();
                if (indexAge is null)
                {
                    AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Package index not found.");
                    AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
                    return 1;
                }

                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Package [cyan]{packageId}[/] not found in index.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update the index.");
                return 1;
            }

            // Validate package type
            if (!packageEntry.Types.Contains("RevelaTheme", StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Package [cyan]{packageId}[/] is not a theme.");
                AnsiConsole.MarkupLine($"  Package types: {string.Join(", ", packageEntry.Types)}");
                AnsiConsole.MarkupLine("  Use [cyan]revela plugin install[/] for plugins.");
                return 1;
            }

            var sourceInfo = source is not null ? $" from [dim]{source}[/]" : "";
            AnsiConsole.MarkupLine($"[blue]Installing theme:[/] [cyan]{packageId}[/]{sourceInfo}");
            LogInstallingTheme(logger, packageId, version, source);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ctx.Status($"Downloading {packageId}...");
                    return await pluginManager.InstallAsync(packageId, version, source, cancellationToken);
                });

            if (success)
            {
                // Register theme in global config (revela.json)
                var installedVersion = version ?? packageEntry.Version;
                await GlobalConfigManager.AddThemeAsync(packageId, installedVersion, cancellationToken);

                AnsiConsole.MarkupLine($"{OutputMarkers.Success} Theme [cyan]{packageId}[/] installed successfully.");
                AnsiConsole.MarkupLine("[dim]Configure with:[/] revela config theme select");
                return 0;
            }

            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to install theme.");
            return 1;
        }
        catch (Exception ex)
        {
            LogInstallFailed(logger, ex);
            ErrorPanels.ShowException(ex, $"Failed to install theme '{name}'.");
            return 1;
        }
    }

    /// <summary>
    /// Gets available themes (not yet installed) from the package index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available theme packages.</returns>
    public async Task<IReadOnlyList<PackageIndexEntry>> GetAvailableThemesAsync(CancellationToken cancellationToken = default)
    {
        var themes = await packageIndexService.SearchByTypeAsync("RevelaTheme", cancellationToken);
        if (themes.Count == 0)
        {
            return [];
        }

        var installedThemes = await GlobalConfigManager.GetThemesAsync(cancellationToken);
        var installedIds = installedThemes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. themes.Where(t => !installedIds.Contains(t.Id))];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing theme {PackageId} version={Version} source={Source}")]
    private static partial void LogInstallingTheme(ILogger logger, string packageId, string? version, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install theme")]
    private static partial void LogInstallFailed(ILogger logger, Exception exception);
}
