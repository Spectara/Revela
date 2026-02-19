using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Commands.Restore;

/// <summary>
/// Restores project dependencies (themes and plugins)
/// </summary>
/// <remarks>
/// Reads dependencies from the merged configuration (revela.json + project.json)
/// and installs any that are missing:
/// - Theme from "theme" property
/// - Themes from "themes" section
/// - Plugins from "plugins" section
/// Uses PluginManager for installation.
/// </remarks>
internal sealed partial class RestoreCommand(
    IDependencyScanner dependencyScanner,
    IThemeResolver themeResolver,
    IEnumerable<IPlugin> installedPlugins,
    PluginManager pluginManager,
    IOptions<ProjectEnvironment> projectEnvironment,
    ILogger<RestoreCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var checkOption = new Option<bool>("--check")
        {
            Description = "Only check dependencies, don't install"
        };

        var command = new Command("restore", "Restore project dependencies (themes and plugins)");
        command.Options.Add(checkOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var checkOnly = parseResult.GetValue(checkOption);

            return await ExecuteAsync(checkOnly, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(bool checkOnly, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(projectEnvironment.Value.Path);

        if (!Directory.Exists(fullPath))
        {
            ErrorPanels.ShowDirectoryNotFoundError(fullPath);
            return 1;
        }

        // Check for project.json
        var projectJsonPath = Path.Combine(fullPath, "project.json");
        if (!File.Exists(projectJsonPath))
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        LogRestoring(fullPath);

        // Get dependencies from merged config (revela.json + project.json)
        var dependencies = dependencyScanner.GetDependencies();

        if (dependencies.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} No dependencies to restore.");
            return 0;
        }

        // Check each dependency
        var missing = new List<RequiredDependency>();
        var installed = new List<RequiredDependency>();

        AnsiConsole.MarkupLine("\n[bold]Checking dependencies...[/]\n");

        foreach (var dep in dependencies)
        {
            var isInstalled = dep.Type switch
            {
                DependencyType.Theme => IsThemeInstalled(dep, fullPath),
                DependencyType.Plugin => IsPluginInstalled(dep),
                _ => false
            };

            var typeLabel = dep.Type == DependencyType.Theme ? "Theme" : "Plugin";
            var shortName = GetShortName(dep);

            if (isInstalled)
            {
                AnsiConsole.MarkupLine($"  [green]+[/] {typeLabel} [white]{shortName}[/]");
                installed.Add(dep);
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {typeLabel} [white]{shortName}[/] - [yellow]missing[/]");
                missing.Add(dep);
            }
        }

        AnsiConsole.WriteLine();

        // Summary
        if (missing.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {dependencies.Count} dependency(ies) are installed.");
            return 0;
        }

        if (checkOnly)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {missing.Count} dependency(ies) missing.");
            AnsiConsole.MarkupLine("    Run [blue]revela restore[/] to install them.");
            return 1;
        }

        // Install missing dependencies with progress bar
        AnsiConsole.MarkupLine($"[bold]Installing {missing.Count} missing dependency(ies)...[/]\n");

        var installFailed = new System.Collections.Concurrent.ConcurrentBag<(RequiredDependency Dep, string Error)>();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[green]Restoring dependencies[/]", maxValue: missing.Count);

                await Parallel.ForEachAsync(
                    missing,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 4,
                        CancellationToken = cancellationToken
                    },
                    async (dep, ct) =>
                    {
                        var shortName = GetShortName(dep);

                        try
                        {
                            // Install plugin or theme using PluginManager
                            var success = await pluginManager.InstallAsync(
                                packageIdOrPath: dep.PackageId,
                                version: dep.Version,
                                source: null, // Use default NuGet.org
                                cancellationToken: ct);

                            if (!success)
                            {
                                installFailed.Add((dep, "Installation failed (see logs)"));
                            }
                        }
                        catch (Exception ex)
                        {
                            installFailed.Add((dep, ex.Message));
                        }
                        finally
                        {
                            progressTask.Increment(1);
                        }
                    });
            });

        AnsiConsole.WriteLine();

        // Show results
        var successCount = missing.Count - installFailed.Count;
        if (successCount > 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Installed {successCount} package(s)");
        }

        if (!installFailed.IsEmpty)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to install {installFailed.Count} package(s):");
            foreach (var (dep, error) in installFailed)
            {
                var shortName = GetShortName(dep);
                // Escape Spectre markup in error message
                var safeError = error
                    .Replace("[", "[[", StringComparison.Ordinal)
                    .Replace("]", "]]", StringComparison.Ordinal);
                AnsiConsole.MarkupLine($"  [red]-[/] {shortName}: [dim]{safeError}[/]");
            }
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Run with increased log level for details: [blue]revela restore --loglevel Debug[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Restore complete - all dependencies installed.");
        return 0;
    }

    private bool IsThemeInstalled(RequiredDependency dep, string projectPath)
    {
        var themeName = GetShortName(dep);

        // Check if theme is available (local or installed)
        var theme = themeResolver.Resolve(themeName, projectPath);
        return theme != null;
    }

    private bool IsPluginInstalled(RequiredDependency dep)
    {
        // Check if plugin is loaded by matching package ID patterns
        // Package ID: "Spectara.Revela.Plugin.Source.OneDrive"
        // Plugin Name: "OneDrive Source"

        return installedPlugins.Any(p =>
        {
            // Direct name match
            if (p.Metadata.Name.Equals(GetShortName(dep), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if package ID contains the plugin name (without spaces)
            var pluginNameNormalized = p.Metadata.Name.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (dep.PackageId.Contains(pluginNameNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if the last parts of package ID match the plugin name parts
            // "Spectara.Revela.Plugin.Source.OneDrive" should match "OneDrive Source"
            var packageParts = dep.PackageId.Split('.');
            var nameParts = p.Metadata.Name.Split(' ');

            // Reverse compare: last package part should be in name
            if (packageParts.Length > 0 && nameParts.Length > 0)
            {
                var lastPackagePart = packageParts[^1];
                if (nameParts.Any(part => part.Equals(lastPackagePart, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        });
    }

    private static string GetShortName(RequiredDependency dep)
    {
        // Extract short name from package ID
        // "Spectara.Revela.Plugin.Source.OneDrive" → "OneDrive Source" or just last part
        // "Spectara.Revela.Theme.Lumina" → "Lumina"

        var parts = dep.PackageId.Split('.');
        if (parts.Length >= 2)
        {
            // Get last meaningful parts
            if (dep.Type == DependencyType.Theme && parts.Length > 3)
            {
                return parts[^1]; // Just theme name
            }

            if (dep.Type == DependencyType.Plugin && parts.Length > 4)
            {
                // "Spectara.Revela.Plugin.Source.OneDrive" → "OneDrive"
                return parts[^1];
            }
        }

        return dep.PackageId;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restoring dependencies for {ProjectPath}")]
    private partial void LogRestoring(string projectPath);
}
