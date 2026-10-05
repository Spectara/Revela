using System.CommandLine;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Features.Packages.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin install' command.
/// </summary>
/// <remarks>
/// A single package installs without the package index; the package type is validated from the
/// package's nuspec by <see cref="PackageInstallService"/>. Interactive selection and <c>--all</c>
/// list plugins from the index ('revela packages refresh').
/// </remarks>
internal sealed partial class PluginInstallCommand(
    ILogger<PluginInstallCommand> logger,
    PackageInstallService installService,
    IPackageIndexService packageIndexService,
    ProjectFeedConsent feedConsent,
    IConsoleCapabilities consoleCapabilities)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("install", "Install a plugin from NuGet");

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Plugin name (e.g., 'Source.OneDrive' for Revela.Plugin.Source.OneDrive)",
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
            Description = "Install all available plugins"
        };
        command.Options.Add(allOption);

        var allowProjectFeedsOption = ProjectFeedConsent.CreateOption();
        command.Options.Add(allowProjectFeedsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var source = parseResult.GetValue(sourceOption);
            var all = parseResult.GetValue(allOption);

            // No name and no --all → interactive multi-selection, which needs a terminal
            if (!all && string.IsNullOrEmpty(name) && !consoleCapabilities.EnsureInteractive(
                "No plugin name given and this console is not interactive.",
                "revela plugin install <name>",
                "revela plugin install --all"))
            {
                return 1;
            }

            if (!await feedConsent.EnsureApprovedAsync(parseResult.GetValue(allowProjectFeedsOption), source, cancellationToken))
            {
                return 1;
            }

            // --all flag → install all available plugins
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

            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteFromNuGetAsync(name, version, source, cancellationToken);
        });

        return command;
    }

    /// <summary>
    /// Installs all available plugins (for --all flag).
    /// </summary>
    /// <param name="showRestartNotice">Whether to show a prominent restart notice (false when called from wizard).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing installed, already-installed, and failed packages.</returns>
    public async Task<InstallResult> InstallAllAsync(bool showRestartNotice = true, CancellationToken cancellationToken = default)
    {
        var plugins = await packageIndexService.SearchByTypeAsync(PackageIds.PluginPackageType, cancellationToken);

        if (plugins.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No plugins found in package index.");
            AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
            return InstallResult.Empty;
        }

        // Filter out already installed plugins
        var installedIds = plugins.Select(p => p.Id).Where(installService.IsInstalled).ToList();
        var availablePlugins = plugins.Where(p => !installService.IsInstalled(p.Id)).ToList();

        if (availablePlugins.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All available plugins are already installed.");
            return new InstallResult([], installedIds, []);
        }

        AnsiConsole.MarkupLine($"Installing [cyan]{availablePlugins.Count}[/] plugin(s)...");
        AnsiConsole.WriteLine();

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var plugin in availablePlugins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecuteFromNuGetAsync(plugin.Id, version: null, source: null, cancellationToken);
            if (result == 0)
            {
                installed.Add(plugin.Id);
            }
            else
            {
                failed.Add(plugin.Id);
            }
        }

        // Show summary
        AnsiConsole.WriteLine();
        if (failed.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {installed.Count} plugins installed successfully.");
        }
        else
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {installed.Count} of {availablePlugins.Count} plugins installed.");
        }

        // Show prominent restart notice if packages were installed
        if (showRestartNotice && installed.Count > 0)
        {
            InstallCommandHelper.ShowRestartNotice("plugins");
        }

        return new InstallResult(installed, installedIds, failed);
    }

    /// <summary>
    /// Installs plugins interactively (shows multi-select prompt).
    /// </summary>
    /// <param name="showRestartNotice">Whether to show a prominent restart notice (false when called from wizard).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing installed, already-installed, and failed packages.</returns>
    public async Task<InstallResult> InstallInteractiveAsync(bool showRestartNotice = true, CancellationToken cancellationToken = default)
    {
        var selectedPlugins = await SelectPluginsInteractivelyAsync(cancellationToken);
        if (selectedPlugins.Count == 0)
        {
            // Check if all plugins are already installed
            var plugins = await packageIndexService.SearchByTypeAsync(PackageIds.PluginPackageType, cancellationToken);
            var installedPlugins = plugins.Select(p => p.Id).Where(installService.IsInstalled).ToList();
            if (installedPlugins.Count > 0)
            {
                return new InstallResult([], installedPlugins, []);
            }

            return InstallResult.Empty;
        }

        var installed = new List<string>();
        var failed = new List<string>();

        foreach (var pluginId in selectedPlugins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ExecuteFromNuGetAsync(pluginId, version: null, source: null, cancellationToken);
            if (result == 0)
            {
                installed.Add(pluginId);
            }
            else
            {
                failed.Add(pluginId);
            }
        }

        // Show summary if multiple plugins
        if (selectedPlugins.Count > 1)
        {
            AnsiConsole.WriteLine();
            if (failed.Count == 0)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {installed.Count} plugins installed successfully.");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {installed.Count} of {selectedPlugins.Count} plugins installed.");
            }
        }

        // Show prominent restart notice if packages were installed
        if (showRestartNotice && installed.Count > 0)
        {
            InstallCommandHelper.ShowRestartNotice("plugins");
        }

        return new InstallResult(installed, [], failed);
    }

    private async Task<IReadOnlyList<string>> SelectPluginsInteractivelyAsync(CancellationToken cancellationToken)
    {
        var plugins = await packageIndexService.SearchByTypeAsync(PackageIds.PluginPackageType, cancellationToken);

        if (plugins.Count == 0)
        {
            var indexAge = packageIndexService.GetIndexAge();
            if (indexAge is null)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Package index not found.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
            }
            else
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No plugins found in package index.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
            }

            return [];
        }

        // Filter out already installed plugins
        var installedPlugins = plugins.Select(p => p.Id).Where(installService.IsInstalled).ToList();
        var availablePlugins = plugins.Where(p => !installService.IsInstalled(p.Id)).ToList();

        if (availablePlugins.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All available plugins are already installed.");
            return [];
        }

        // Show info about installed plugins
        if (installedPlugins.Count > 0)
        {
            AnsiConsole.MarkupLine("[green]Already installed:[/]");
            foreach (var pluginId in installedPlugins)
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Success} {Markup.Escape(pluginId)}");
            }

            AnsiConsole.WriteLine();
        }

        var choices = availablePlugins
            .Select(InstallCommandHelper.FormatPackageChoice)
            .ToList();

        var selections = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[cyan]Select plugins to install:[/] [dim](Space to select, Enter to confirm)[/]")
                .PageSize(15)
                .Required(false)
                .HighlightStyle(new Style(Color.Cyan1))
                .InstructionsText("[dim](↑↓ navigate, Space select, Enter confirm)[/]")
                .AddChoices(InstallCommandHelper.SelectAllChoice)
                .AddChoices(choices));

        // Handle "Select all" option
        if (selections.Contains(InstallCommandHelper.SelectAllChoice))
        {
            return [.. availablePlugins.Select(p => p.Id)];
        }

        // Extract package IDs from selections (before first space)
        return [.. selections.Select(s => s.Split(' ')[0])];
    }

    internal async Task<int> ExecuteFromNuGetAsync(string name, string? version, string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var packageId = PackageIds.FromPluginName(name);

            var sourceInfo = source is not null ? $" from [dim]{Markup.Escape(UrlRedaction.Redact(source))}[/]" : "";
            AnsiConsole.MarkupLine($"[blue]Installing plugin:[/] [cyan]{Markup.Escape(packageId)}[/]{sourceInfo}");
            if (logger.IsEnabled(LogLevel.Information))
            {
                var redactedSource = UrlRedaction.Redact(source);
                LogInstallingPlugin(packageId, version, redactedSource);
            }

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ctx.Status($"Downloading {packageId}...");
                    return await installService.InstallAsync(packageId, PackageIds.PluginPackageType, version, source, cancellationToken);
                });

            return InstallCommandHelper.ReportInstall(
                result,
                packageId,
                "plugin",
                "[dim]The plugin will be available after restarting revela.[/]");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogError(ex);
            ErrorPanels.ShowException(ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin '{PackageId}' version '{Version}' from source '{Source}'")]
    private partial void LogInstallingPlugin(string packageId, string? version, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin")]
    private partial void LogError(Exception exception);
}

