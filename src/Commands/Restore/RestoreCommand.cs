using System.CommandLine;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Restore;

/// <summary>
/// Restores project dependencies (themes and plugins)
/// </summary>
/// <remarks>
/// Scans the project for required dependencies and installs missing ones:
/// - Theme from project.json
/// - Plugins from plugins/*.json files (keys in "Plugins" section)
/// </remarks>
public sealed partial class RestoreCommand(
    IDependencyScanner dependencyScanner,
    IThemeResolver themeResolver,
    IEnumerable<IPlugin> installedPlugins,
    ILogger<RestoreCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var pathOption = new Option<string?>("--path", "-p")
        {
            Description = "Path to the project directory (default: current directory)"
        };

        var checkOption = new Option<bool>("--check")
        {
            Description = "Only check dependencies, don't install"
        };

        var command = new Command("restore", "Restore project dependencies (themes and plugins) (not implemented yet)");
        command.Options.Add(pathOption);
        command.Options.Add(checkOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(pathOption) ?? Directory.GetCurrentDirectory();
            var checkOnly = parseResult.GetValue(checkOption);

            return await ExecuteAsync(path, checkOnly, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string projectPath, bool checkOnly, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(projectPath);

        if (!Directory.Exists(fullPath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR[/] Project directory not found: {fullPath}");
            return 1;
        }

        // Check for project.json
        var projectJsonPath = Path.Combine(fullPath, "project.json");
        if (!File.Exists(projectJsonPath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR[/] No project.json found in {fullPath}");
            AnsiConsole.MarkupLine("    Run [blue]revela init project[/] to create a new project.");
            return 1;
        }

        LogRestoring(fullPath);

        // Scan for dependencies
        var dependencies = await dependencyScanner.ScanAsync(fullPath, cancellationToken);

        if (dependencies.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]OK[/] No dependencies to restore.");
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
            var sourceFile = Path.GetFileName(dep.SourceFile);

            if (isInstalled)
            {
                AnsiConsole.MarkupLine($"  [green]+[/] {typeLabel} [white]{shortName}[/] [dim]({sourceFile})[/]");
                installed.Add(dep);
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {typeLabel} [white]{shortName}[/] [dim]({sourceFile})[/] - [yellow]missing[/]");
                missing.Add(dep);
            }
        }

        AnsiConsole.WriteLine();

        // Summary
        if (missing.Count == 0)
        {
            AnsiConsole.MarkupLine($"[green]OK[/] All {dependencies.Count} dependency(ies) are installed.");
            return 0;
        }

        if (checkOnly)
        {
            AnsiConsole.MarkupLine($"[yellow]![/] {missing.Count} dependency(ies) missing.");
            AnsiConsole.MarkupLine("    Run [blue]revela restore[/] to install them.");
            return 1;
        }

        // Install missing dependencies
        AnsiConsole.MarkupLine($"[bold]Installing {missing.Count} missing dependency(ies)...[/]\n");

        var installFailed = new List<(RequiredDependency Dep, string Error)>();

        foreach (var dep in missing)
        {
            var shortName = GetShortName(dep);

            try
            {
                // TODO: Implement actual package installation from NuGet
                // For now, just show what would be installed
                AnsiConsole.MarkupLine($"  [yellow]↓[/] Installing {shortName}...");

                // Placeholder - will integrate with NuGet/plugin installation
                AnsiConsole.MarkupLine($"    [dim](not implemented yet)[/]");
                AnsiConsole.MarkupLine($"    [dim]Install manually: revela plugin add {dep.PackageId}[/]");
            }
            catch (Exception ex)
            {
                installFailed.Add((dep, ex.Message));
                AnsiConsole.MarkupLine($"  [red]x[/] Failed to install {shortName}: {ex.Message}");
            }
        }

        AnsiConsole.WriteLine();

        if (installFailed.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]ERROR[/] Failed to install {installFailed.Count} package(s).");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]OK[/] Restore complete.");
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
