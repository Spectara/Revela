using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Plugin.Source.OneDrive.Formatting;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
using Spectara.Revela.Plugin.Source.OneDrive.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to download images from OneDrive shared folder
/// </summary>
/// <remarks>
/// Uses Dependency Injection with Primary Constructor (C# 12).
/// Configuration is injected via IOptionsMonitor for hot-reload support.
/// Dependencies are injected at construction time, making the command fully testable.
/// </remarks>
public sealed class OneDriveSourceCommand(
    ILogger<OneDriveSourceCommand> logger,
    SharedLinkProvider provider,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<OneDrivePluginConfig> config)
{
    public Command Create()
    {
        var command = new Command("sync", "Sync images from OneDrive shared folder");

        // Options
        var shareUrlOption = new Option<string?>("--share-url", "-u")
        {
            Description = "OneDrive shared folder URL (overrides config)"
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force re-download even if files exist"
        };

        var dryRunOption = new Option<bool>("--dry-run", "-n")
        {
            Description = "Preview changes without downloading (dry-run)"
        };

        var cleanOption = new Option<bool>("--clean")
        {
            Description = "Remove local files not present in OneDrive (with confirmation)"
        };

        var cleanAllOption = new Option<bool>("--clean-all")
        {
            Description = "Remove ALL local files not in OneDrive, ignoring filters (dangerous!)",
            Hidden = true
        };

        var showFilesOption = new Option<bool>("--show-files")
        {
            Description = "Show detailed list of all affected files",
            Hidden = true
        };

        command.Options.Add(shareUrlOption);
        command.Options.Add(forceOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(cleanOption);
        command.Options.Add(cleanAllOption);
        command.Options.Add(showFilesOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new ExecutionOptions
            {
                ShareUrl = parseResult.GetValue(shareUrlOption),
                ForceRefresh = parseResult.GetValue(forceOption),
                DryRun = parseResult.GetValue(dryRunOption),
                Clean = parseResult.GetValue(cleanOption),
                CleanAll = parseResult.GetValue(cleanAllOption),
                ShowFiles = parseResult.GetValue(showFilesOption)
            };

            await ExecuteAsync(options, cancellationToken);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(ExecutionOptions options, CancellationToken cancellationToken)
    {
        try
        {
            // Get current config from IOptionsMonitor (hot-reload support)
            var currentConfig = config.CurrentValue;

            // CLI --share-url overrides config file
            var shareUrl = options.ShareUrl ?? currentConfig.ShareUrl;
            var includePatterns = currentConfig.IncludePatterns?.ToArray();
            var excludePatterns = currentConfig.ExcludePatterns?.ToArray();

            // Validate ShareUrl (either from config or CLI)
            if (string.IsNullOrWhiteSpace(shareUrl))
            {
                ShowMissingConfigError();
                return;
            }

            // Build OneDriveConfig for provider
            var downloadConfig = new OneDriveConfig
            {
                ShareUrl = shareUrl,
                IncludePatterns = includePatterns,
                ExcludePatterns = excludePatterns
            };

            // Determine output directory (Config > Default)
            var outputDirectory = currentConfig.OutputDirectory ?? ProjectPaths.Source;
            outputDirectory = Path.Combine(projectEnvironment.Value.Path, outputDirectory);

            // Determine concurrency (Config > Default)
            // Most users never need to change this - sensible default works for typical home internet
            var concurrency = currentConfig.DefaultConcurrency ?? DefaultConcurrency;

            AnsiConsole.MarkupLine("[blue]Downloading from OneDrive...[/]");
            AnsiConsole.MarkupLine($"[dim]Share URL:[/] {downloadConfig.ShareUrl}");
            AnsiConsole.MarkupLine($"[dim]Output:[/] {outputDirectory}");
            AnsiConsole.MarkupLine($"[dim]Concurrency:[/] {concurrency} parallel downloads");
            AnsiConsole.WriteLine();

            // Phase 1: Scan OneDrive structure
            IReadOnlyList<OneDriveItem>? allItems = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[yellow]Scanning OneDrive folder structure...[/]", async ctx =>
                {
                    allItems = await provider.ListItemsAsync(downloadConfig, cancellationToken);

                    // Count files and folders in single pass
                    var fileCount = 0;
                    var folderCount = 0;
                    foreach (var item in allItems)
                    {
                        if (item.IsFolder)
                        {
                            folderCount++;
                        }
                        else
                        {
                            fileCount++;
                        }
                    }

                    ctx.Status($"{OutputMarkers.Success} Found {fileCount} files in {folderCount} folders");
                    await Task.Delay(500); // Brief pause to show result
                });

            if (allItems is null)
            {
                throw new InvalidOperationException("Failed to scan OneDrive folder");
            }

            // Count already done in Status block, reuse or recalculate
            var (files, folders) = CountItemTypes(allItems);
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Scan complete: {files} files, {folders} folders");
            AnsiConsole.WriteLine();

            // Phase 2: Analyze changes
            AnsiConsole.MarkupLine("[blue]Analyzing changes...[/]");

            var analysis = DownloadAnalyzer.Analyze(
                allItems,
                outputDirectory,
                downloadConfig,
                includeOrphans: options.Clean || options.CleanAll,
                includeAllOrphans: options.CleanAll,
                forceRefresh: options.ForceRefresh
            );

            // Display analysis results
            DisplayAnalysisResults(analysis, options.DryRun, options.Clean || options.CleanAll, options.ForceRefresh, options.ShowFiles);

            // Dry-run: Exit after showing preview
            if (options.DryRun)
            {
                AnsiConsole.MarkupLine("\n[dim]Run without --dry-run to apply changes.[/]");
                if (analysis.Statistics.OrphanedFiles > 0)
                {
                    AnsiConsole.MarkupLine("[dim]Add --clean to remove orphaned files.[/]");
                }
                return;
            }

            // Handle orphaned files if --clean specified
            if ((options.Clean || options.CleanAll) && analysis.OrphanedFiles is not [])
            {
#pragma warning disable CA2016 // Spectre.Console ConfirmAsync doesn't support CancellationToken
                if (!await AnsiConsole.ConfirmAsync($"[yellow]Delete {analysis.OrphanedFiles.Count} orphaned file(s)?[/]").ConfigureAwait(false))
#pragma warning restore CA2016
                {
                    AnsiConsole.MarkupLine("[dim]Skipping cleanup.[/]");
                }
                else
                {
                    foreach (var file in analysis.OrphanedFiles)
                    {
                        file.Delete();
                    }
                    AnsiConsole.MarkupLine($"{OutputMarkers.Success} Deleted {analysis.OrphanedFiles.Count} orphaned file(s)");
                }
            }

            // Skip download if nothing to download
            if (analysis.Statistics.TotalFilesToDownload == 0)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Success} All files are up to date!");
                return;
            }

            // Phase 3: Download files with progress
            AnsiConsole.WriteLine();
            var downloadedFiles = new List<string>();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var mainTask = ctx.AddTask($"[green]Downloading Changes[/]", maxValue: 100);
                    mainTask.IsIndeterminate = true;

                    var progress = new Progress<(int current, int total)>(report =>
                    {
                        if (mainTask.IsIndeterminate && report.total > 0)
                        {
                            mainTask.IsIndeterminate = false;
                            mainTask.MaxValue = report.total;
                        }
                        mainTask.Value = report.current;
                        mainTask.Description = $"[green]Downloading[/] ({report.current}/{report.total})";
                    });

                    // Only download items that need updating
                    var itemsToDownload = analysis.ItemsToDownload.ToList();
                    downloadedFiles = [.. await DownloadItemsAsync(itemsToDownload, outputDirectory, concurrency, progress, cancellationToken)];

                    mainTask.StopTask();
                });

            // Success message
            var panel = new Panel(
                $"[green]Downloaded {downloadedFiles.Count} file(s)![/]\n\n" +
                $"[bold]Files saved to:[/] [cyan]{outputDirectory}[/]\n\n" +
                $"[dim]Statistics:[/]\n" +
                $"  + New: {analysis.Statistics.NewFiles}\n" +
                $"  ~ Updated: {analysis.Statistics.ModifiedFiles}\n" +
                $"  = Unchanged: {analysis.Statistics.UnchangedFiles}\n\n" +
                $"[dim]Next steps:[/]\n" +
                $"1. Run [cyan]revela generate[/] to process your content\n" +
                $"2. Check output in [cyan]output/[/] directory"
            )
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            ErrorPanels.ShowException(ex);
            logger.DownloadFailed(ex);
        }
    }

    /// <summary>
    /// Counts files and folders in a single pass
    /// </summary>
    private static (int files, int folders) CountItemTypes(IReadOnlyList<OneDriveItem> items)
    {
        var files = 0;
        var folders = 0;

        foreach (var item in items)
        {
            if (item.IsFolder)
            {
                folders++;
            }
            else
            {
                files++;
            }
        }

        return (files, folders);
    }

    /// <summary>
    /// Default number of parallel downloads.
    /// </summary>
    /// <remarks>
    /// Downloads are I/O-bound (network), not CPU-bound. A value of 4 works well for
    /// typical home internet (50-500 Mbit). Power users with fast connections can
    /// increase this via config file (DefaultConcurrency setting).
    /// </remarks>
    private const int DefaultConcurrency = 4;

    /// <summary>
    /// Displays analysis results in a user-friendly format
    /// </summary>
    private static void DisplayAnalysisResults(DownloadAnalysis analysis, bool isDryRun, bool includeOrphans, bool forceRefresh, bool showFiles)
    {
        var stats = analysis.Statistics;

        // Show file lists first (if --show-files is enabled)
        if (showFiles)
        {
            DisplayFileList(analysis.Items.Where(i => i.Status == FileStatus.New).ToList(),
                "[green]NEW FILES:[/]", "+",
                item => (item.RelativePath, FileSizeFormatter.Format(item.RemoteItem.Size)));

            DisplayFileList(analysis.Items.Where(i => i.Status == FileStatus.Modified).ToList(),
                "[yellow]MODIFIED FILES:[/]", "~",
                item => (item.RelativePath, item.Reason));

            if (includeOrphans && analysis.OrphanedFiles is not [])
            {
                DisplayOrphanedFileList(analysis.OrphanedFiles);
            }
        }

        // Summary panel (ALWAYS shown, at the end)
        DisplaySummaryPanel(stats, isDryRun, includeOrphans, forceRefresh, showFiles);
    }

    /// <summary>
    /// Displays a file list table
    /// </summary>
    private static void DisplayFileList<T>(
        List<T> items,
        string header,
        string marker,
        Func<T, (string path, string details)> selector) where T : notnull
    {
        if (items is [])
        {
            return;
        }

        AnsiConsole.MarkupLine(header);
        var table = new Table { Border = TableBorder.None };
        table.AddColumn("Path");
        table.AddColumn("Details");

        foreach (var item in items)
        {
            var (path, details) = selector(item);
            table.AddRow($"[dim]{marker}[/] {path}", details);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays orphaned files list
    /// </summary>
    private static void DisplayOrphanedFileList(IReadOnlyList<FileInfo> files)
    {
        AnsiConsole.MarkupLine("[red]ORPHANED FILES (local only):[/]");
        var table = new Table { Border = TableBorder.None };
        table.AddColumn("Path");
        table.AddColumn("Size");

        foreach (var file in files)
        {
            table.AddRow($"[dim]-[/] {file.Name}", FileSizeFormatter.Format(file.Length));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays summary panel
    /// </summary>
    private static void DisplaySummaryPanel(
        DownloadStatistics stats,
        bool isDryRun,
        bool includeOrphans,
        bool forceRefresh,
        bool showFiles)
    {
        var summaryText = isDryRun ? "[yellow]DRY RUN - Preview of Changes[/]\n\n" : "[blue]Analysis Complete[/]\n\n";
        summaryText += $"[bold]Summary:[/]\n";
        summaryText += $"  + {stats.NewFiles} new file(s) ({FileSizeFormatter.Format(stats.TotalDownloadSize)})\n";
        summaryText += $"  ~ {stats.ModifiedFiles} modified file(s)\n";
        summaryText += $"  = {stats.UnchangedFiles} unchanged file(s)\n";

        if (includeOrphans && stats.OrphanedFiles > 0)
        {
            summaryText += $"  - {stats.OrphanedFiles} orphaned file(s) ({FileSizeFormatter.Format(stats.TotalOrphanedSize)})\n";
        }

        if (forceRefresh && stats.ModifiedFiles > 0)
        {
            summaryText += $"\n[dim]Force refresh enabled[/]";
        }

        if (!showFiles && (stats.NewFiles > 0 || stats.ModifiedFiles > 0 || stats.OrphanedFiles > 0))
        {
            summaryText += $"\n\n[dim]Run with --show-files to see detailed file list[/]";
        }

        AnsiConsole.Write(new Panel(summaryText)
            .WithHeader(isDryRun ? "[bold yellow]Preview[/]" : "[bold blue]Summary[/]")
            .WithInfoStyle());
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Downloads a list of items with progress reporting
    /// </summary>
    private async Task<List<string>> DownloadItemsAsync(
        List<DownloadItem> items,
        string destinationDirectory,
        int concurrency,
        IProgress<(int current, int total)>? progress,
        CancellationToken cancellationToken)
    {
        var downloadedFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
        var current = 0;
        var total = items.Count;

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = cancellationToken
            },
            async (item, ct) =>
            {
                var destinationPath = Path.Combine(destinationDirectory, item.RelativePath);

                // Download file first
                var downloadedPath = await provider.DownloadFileAsync(item.RemoteItem, destinationPath, ct);
                downloadedFiles.Add(downloadedPath);

                // Report progress AFTER download completes
                var currentIndex = Interlocked.Increment(ref current);
                progress?.Report((currentIndex, total));
            });

        return [.. downloadedFiles];
    }

    /// <summary>
    /// Shows a user-friendly error when OneDrive is not configured.
    /// </summary>
    private static void ShowMissingConfigError()
    {
        ErrorPanels.ShowConfigRequiredError(
            "OneDrive share URL",
            "config onedrive",
            "  1. Run [cyan]revela config onedrive[/] to configure interactively\n" +
            "  2. Set environment variable: [cyan]SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL[/]\n" +
            "  3. Provide [cyan]--share-url[/] parameter");
    }
}

