using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Packages.Commands.Restore;

/// <summary>
/// Restores project dependencies (themes and plugins)
/// </summary>
/// <remarks>
/// <para>
/// Reads <c>dependencies.packages</c> from the merged configuration (revela.json + project.json)
/// and installs every package that is not loaded yet. A package counts as installed when any
/// loaded plugin or theme (including theme extensions) has the same package ID; its type is
/// taken from the package itself, not from its ID.
/// </para>
/// <para>
/// The active theme (<c>theme.name</c>) is resolved by manifest name through local and
/// installed themes. Only when no theme and no declared dependency provides it does restore
/// fall back to the official <c>Spectara.Revela.Themes.{name}</c> package, which is then
/// declared in project.json.
/// </para>
/// <para>
/// Restored versions are pinned only in project.json entries; dependencies declared only in
/// revela.json are installed without copying them into the project.
/// </para>
/// </remarks>
internal sealed partial class RestoreCommand(
    IDependencyScanner dependencyScanner,
    IThemeRegistry themeRegistry,
    IEnumerable<IPlugin> installedPlugins,
    IEnumerable<ITheme> installedThemes,
    PackageManager packageManager,
    PackageInstallService installService,
    PackageDeclarations declarations,
    ProjectFeedConsent feedConsent,
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
        var allowProjectFeedsOption = ProjectFeedConsent.CreateOption();

        var command = new Command("restore", "Restore project dependencies (themes and plugins)");
        command.Options.Add(checkOption);
        command.Options.Add(allowProjectFeedsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var checkOnly = parseResult.GetValue(checkOption);
            var allowProjectFeeds = parseResult.GetValue(allowProjectFeedsOption);

            return await ExecuteAsync(checkOnly, allowProjectFeeds, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(bool checkOnly, bool allowProjectFeeds, CancellationToken cancellationToken)
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

        var dependencies = dependencyScanner.GetDependencies();
        var activeTheme = dependencyScanner.GetActiveThemeName();

        if (dependencies.Count == 0 && activeTheme is null)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} No dependencies to restore.");
            return 0;
        }

        AnsiConsole.MarkupLine("\n[bold]Checking dependencies...[/]\n");

        var missing = new List<RequiredDependency>();
        foreach (var dep in dependencies)
        {
            var id = Markup.Escape(dep.PackageId);
            var label = GetInstalledLabel(dep.PackageId);
            if (label is not null)
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Success} {label} [white]{id}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Error} Package [white]{id}[/] - [yellow]missing[/]");
                missing.Add(dep);
            }
        }

        var themeMissing = false;
        if (activeTheme is not null)
        {
            var themeName = Markup.Escape(activeTheme);
            ITheme? theme;
            try
            {
                theme = themeRegistry.Resolve(activeTheme, fullPath);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Error} Theme [white]{themeName}[/] - [red]invalid[/]: {Markup.Escape(ex.Message)}");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} 1 theme(s) invalid; {missing.Count} dependency(ies) missing.");
                AnsiConsole.MarkupLine("    Fix invalid local theme configuration before restoring dependencies. No packages were installed.");
                return 1;
            }

            if (theme is not null)
            {
                var provider = installedThemes.Contains(theme) ? Markup.Escape(theme.Metadata.Id) : "local";
                AnsiConsole.MarkupLine($"  {OutputMarkers.Success} Theme [white]{themeName}[/] [dim]({provider})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Error} Theme [white]{themeName}[/] - [yellow]missing[/] [dim](no installed or local theme has this name)[/]");
                themeMissing = true;
            }
        }

        AnsiConsole.WriteLine();

        var missingCount = missing.Count + (themeMissing ? 1 : 0);
        if (missingCount == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} All {dependencies.Count + (activeTheme is null ? 0 : 1)} dependency(ies) are installed.");
            return 0;
        }

        if (checkOnly)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {missingCount} dependency(ies) missing.");
            AnsiConsole.MarkupLine("    Run [blue]revela restore[/] to install them.");
            return 1;
        }

        if (!await feedConsent.EnsureApprovedAsync(allowProjectFeeds, cancellationToken: cancellationToken))
        {
            return 1;
        }

        var installed = await InstallMissingAsync(missing, cancellationToken);
        if (installed is null)
        {
            return 1;
        }

        if (themeMissing && !await RestoreActiveThemeAsync(activeTheme!, installed, cancellationToken))
        {
            return 1;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Restore complete - all dependencies installed.");
        return 0;
    }

    /// <summary>
    /// Installs the missing packages; returns the installed packages or <c>null</c> if any failed.
    /// </summary>
    private async Task<IReadOnlyList<InstalledPackage>?> InstallMissingAsync(
        List<RequiredDependency> missing,
        CancellationToken cancellationToken)
    {
        if (missing.Count == 0)
        {
            return [];
        }

        AnsiConsole.MarkupLine($"[bold]Installing {missing.Count} missing dependency(ies)...[/]\n");

        var installed = new ConcurrentBag<InstalledPackage>();
        var installFailed = new ConcurrentBag<(RequiredDependency Dep, string Error)>();

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
                        try
                        {
                            var package = await packageManager.InstallAsync(
                                packageId: dep.PackageId,
                                version: dep.Version,
                                source: null,
                                cancellationToken: ct);

                            if (package is null)
                            {
                                installFailed.Add((dep, "Installation failed (see logs)"));
                            }
                            else
                            {
                                _ = await declarations.PinAsync(package.Id, package.Version, ct);
                                installed.Add(package);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
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

        foreach (var package in installed.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            var kind = package.IsTheme ? "Theme" : "Plugin";
            AnsiConsole.MarkupLine($"  {OutputMarkers.Success} {kind} [white]{Markup.Escape(package.Id)}[/] [dim]{Markup.Escape(package.Version)}[/]");
        }

        if (!installed.IsEmpty)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Installed {installed.Count} package(s)");
        }

        if (!installFailed.IsEmpty)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to install {installFailed.Count} package(s):");
            foreach (var (dep, error) in installFailed)
            {
                AnsiConsole.MarkupLine($"  {OutputMarkers.Error} {Markup.Escape(dep.PackageId)}: [dim]{Markup.Escape(error)}[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Run with increased log level for details: [blue]revela restore --loglevel Debug[/]");
            return null;
        }

        return [.. installed];
    }

    /// <summary>
    /// Handles an active theme that no local or installed theme provides.
    /// </summary>
    private async Task<bool> RestoreActiveThemeAsync(
        string themeName,
        IReadOnlyList<InstalledPackage> installed,
        CancellationToken cancellationToken)
    {
        var name = Markup.Escape(themeName);

        // Newly installed themes are loaded on the next run; their manifest names cannot be checked in-process.
        var newThemes = installed.Where(p => p.IsTheme).ToList();
        if (newThemes.Count > 0)
        {
            var ids = Markup.Escape(string.Join(", ", newThemes.Select(p => p.Id)));
            AnsiConsole.MarkupLine($"{OutputMarkers.Info} Theme [white]{Markup.Escape(name)}[/] is expected from the newly installed theme package(s) [dim]{Markup.Escape(ids)}[/]; it is loaded on the next run.");
            return true;
        }

        var officialId = PackageIds.FromThemeName(themeName);
        AnsiConsole.MarkupLine($"{OutputMarkers.Info} No installed theme or declared dependency provides theme [white]{name}[/]. Trying the official package [white]{Markup.Escape(officialId)}[/]...");

        var result = PackageIdRules.IsValid(officialId)
            ? await installService.InstallAsync(officialId, PackageIds.ThemePackageType, version: null, source: null, cancellationToken)
            : new PackageInstallResult(PackageInstallStatus.Failed);
        if (result.Status != PackageInstallStatus.Installed)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Theme [white]{name}[/] could not be resolved: no installed or local theme has this name, no declared dependency provides it, and the official package [white]{Markup.Escape(officialId)}[/] could not be installed.");
            AnsiConsole.MarkupLine("    Add the package that provides this theme to [cyan]dependencies.packages[/] in project.json, or change [cyan]theme.name[/].");
            return false;
        }

        var package = result.Package!;
        AnsiConsole.MarkupLine($"  {OutputMarkers.Success} Theme [white]{Markup.Escape(package.Id)}[/] [dim]{Markup.Escape(package.Version)}[/]");
        return true;
    }

    /// <summary>
    /// Returns "Plugin"/"Theme" when a loaded package has <paramref name="packageId"/>; otherwise <c>null</c>.
    /// </summary>
    private string? GetInstalledLabel(string packageId)
    {
        if (installedPlugins.Any(plugin => plugin.Metadata.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Plugin";
        }

        return installedThemes.Any(theme => theme.Metadata.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            ? "Theme"
            : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Restoring dependencies for {ProjectPath}")]
    private partial void LogRestoring(string projectPath);
}
