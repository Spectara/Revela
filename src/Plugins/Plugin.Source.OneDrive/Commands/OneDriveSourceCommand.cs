using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Plugin.Source.OneDrive.Formatting;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
using Spectara.Revela.Plugin.Source.OneDrive.Services;
using Spectre.Console;

#pragma warning disable IDE0055 // Fix formatting preference differences

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
IOptionsMonitor<OneDrivePluginConfig> config,
DownloadAnalyzer downloadAnalyzer)
{
    public Command Create()
    {
        var command = new Command("download", "Download images from OneDrive shared folder");

        // Options (override config file)
        var shareUrlOption = new Option<string?>("--share-url", "-u")
        {
            Description = "OneDrive shared folder URL (overrides config)"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory (defaults to ./content)"
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force re-download even if files exist"
        };

        var includeOption = new Option<string[]?>("--include", "-i")
        {
            Description = "File patterns to include (overrides config)",
            AllowMultipleArgumentsPerToken = true
        };

        var excludeOption = new Option<string[]?>("--exclude", "-e")
        {
            Description = "File patterns to exclude (overrides config)",
            AllowMultipleArgumentsPerToken = true
        };

        var concurrencyOption = new Option<int?>("--concurrency", "-c")
        {
            Description = "Number of concurrent downloads (defaults to auto-detect based on CPU cores)"
        };
        // TODO: Add validation in System.CommandLine 2.0 (API different from beta)

        var debugOption = new Option<bool>("--debug", "-d")
        {
            Description = "Enable debug logging"
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
            Description = "Remove ALL local files not in OneDrive, ignoring filters (dangerous!)"
        };

        var showFilesOption = new Option<bool>("--show-files")
        {
            Description = "Show detailed list of all affected files"
        };

        command.Options.Add(shareUrlOption);
        command.Options.Add(outputOption);
        command.Options.Add(forceOption);
        command.Options.Add(includeOption);
        command.Options.Add(excludeOption);
        command.Options.Add(concurrencyOption);
        command.Options.Add(debugOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(cleanOption);
        command.Options.Add(cleanAllOption);
        command.Options.Add(showFilesOption);

        command.SetAction(async parseResult =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);
            var outputDir = parseResult.GetValue(outputOption);
            var force = parseResult.GetValue(forceOption);
            var includePatterns = parseResult.GetValue(includeOption);
            var excludePatterns = parseResult.GetValue(excludeOption);
            var concurrency = parseResult.GetValue(concurrencyOption);
            var debug = parseResult.GetValue(debugOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var clean = parseResult.GetValue(cleanOption);
            var cleanAll = parseResult.GetValue(cleanAllOption);
            var showFiles = parseResult.GetValue(showFilesOption);

            await ExecuteAsync(shareUrl, outputDir, force, includePatterns, excludePatterns, concurrency, debug, dryRun, clean, cleanAll, showFiles);
            return 0;
        });

        return command;
    }

    private async Task ExecuteAsync(
        string? shareUrlOverride,
        string? outputDirectoryOverride,
        bool forceRefresh,
        string[]? includePatternsOverride,
        string[]? excludePatternsOverride,
        int? concurrencyOverride,
        bool debug,
        bool dryRun,
        bool clean,
        bool cleanAll,
        bool showFiles
    )
    {
        // Debug info (log level must be configured via environment before startup)
        if (debug)
        {
            AnsiConsole.MarkupLine("[yellow]Debug logging requested[/]");
            AnsiConsole.MarkupLine("[dim]Set environment variable REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug[/]");
            AnsiConsole.MarkupLine("[dim]Or create logging.json with Debug level[/]");
        }

        try
        {
            // Get current config from IOptionsMonitor (hot-reload support)
            var currentConfig = config.CurrentValue;

            // CLI parameters override config file (priority: CLI > Config)
            var shareUrl = shareUrlOverride ?? currentConfig.ShareUrl;
            var includePatterns = includePatternsOverride ?? currentConfig.IncludePatterns?.ToArray();
            var excludePatterns = excludePatternsOverride ?? currentConfig.ExcludePatterns?.ToArray();

            // Validate ShareUrl (either from config or CLI)
            if (string.IsNullOrWhiteSpace(shareUrl))
            {
                throw new InvalidOperationException(
                    "No OneDrive share URL provided. Use one of these methods:\n" +
                    "1. Create onedrive.json with ShareUrl property (in Plugins:OneDrive section)\n" +
                    "2. Set environment variable: REVELA__PLUGINS__ONEDRIVE__SHAREURL=<url> or ONEDRIVE_SHAREURL=<url>\n" +
                    "3. Provide --share-url parameter\n\n" +
                    "Example onedrive.json:\n" +
                    "{\n" +
                    "  \"Plugins\": {\n" +
                    "    \"OneDrive\": {\n" +
                    "      \"ShareUrl\": \"https://1drv.ms/u/...\"\n" +
                    "    }\n" +
                    "  }\n" +
                    "}"
                );
            }

            // Build OneDriveConfig for provider
            var downloadConfig = new OneDriveConfig
            {
                ShareUrl = shareUrl,
                IncludePatterns = includePatterns,
                ExcludePatterns = excludePatterns
            };

            // Determine output directory (CLI > Config > Default)
            var outputDirectory = outputDirectoryOverride
                ?? currentConfig.OutputDirectory
                ?? "source";
            outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), outputDirectory);

            // Determine concurrency (CLI > Config > Auto-detect)
            var concurrency = concurrencyOverride
                ?? currentConfig.DefaultConcurrency
                ?? GetDefaultConcurrency();

            // Validate concurrency
            if (concurrency <= 0)
            {
                throw new ArgumentException("Concurrency must be greater than 0", nameof(concurrencyOverride));
            }

            AnsiConsole.MarkupLine("[blue]Downloading from OneDrive...[/]");
            AnsiConsole.MarkupLine($"[dim]Share URL:[/] {downloadConfig.ShareUrl}");
            AnsiConsole.MarkupLine($"[dim]Output:[/] {outputDirectory}");
            AnsiConsole.MarkupLine($"[dim]Concurrency:[/] {concurrency} parallel downloads");
            AnsiConsole.WriteLine();

            // Phase 1: Scan OneDrive structure
            // Note: provider is injected via constructor (DI)
            IReadOnlyList<OneDriveItem>? allItems = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[yellow]Scanning OneDrive folder structure...[/]", async ctx =>
                {
                    allItems = await provider.ListItemsAsync(downloadConfig);
                    var fileCount = allItems.Count(i => !i.IsFolder);
                    var folderCount = allItems.Count(i => i.IsFolder);

                    ctx.Status($"[green]OK[/] Found {fileCount} files in {folderCount} folders");
                    await Task.Delay(500); // Brief pause to show result
                });

            if (allItems is null)
            {
                throw new InvalidOperationException("Failed to scan OneDrive folder");
            }

            AnsiConsole.MarkupLine($"[green]OK[/] Scan complete: {allItems.Count(i => !i.IsFolder)} files, {allItems.Count(i => i.IsFolder)} folders");
            AnsiConsole.WriteLine();

            // Phase 2: Analyze changes
            AnsiConsole.MarkupLine("[blue]Analyzing changes...[/]");
            
            var analysis = downloadAnalyzer.Analyze(
                allItems,
                outputDirectory,
                downloadConfig,
                includeOrphans: clean || cleanAll,
                includeAllOrphans: cleanAll,
                forceRefresh: forceRefresh
            );

            // Display analysis results
            DisplayAnalysisResults(analysis, dryRun, clean || cleanAll, forceRefresh, showFiles);

            // Dry-run: Exit after showing preview
            if (dryRun)
            {
                AnsiConsole.MarkupLine("\n[dim]Run without --dry-run to apply changes.[/]");
                if (analysis.Statistics.OrphanedFiles > 0)
                {
                    AnsiConsole.MarkupLine("[dim]Add --clean to remove orphaned files.[/]");
                }
                return;
            }

            // Handle orphaned files if --clean specified
            if ((clean || cleanAll) && analysis.OrphanedFiles is not [])
            {
                if (!await AnsiConsole.ConfirmAsync($"[yellow]Delete {analysis.OrphanedFiles.Count} orphaned file(s)?[/]").ConfigureAwait(false))
                {
                    AnsiConsole.MarkupLine("[dim]Skipping cleanup.[/]");
                }
                else
                {
                    foreach (var file in analysis.OrphanedFiles)
                    {
                        file.Delete();
                    }
                    AnsiConsole.MarkupLine($"[green]OK[/] Deleted {analysis.OrphanedFiles.Count} orphaned file(s)");
                }
            }

            // Skip download if nothing to download
            if (analysis.Statistics.TotalFilesToDownload == 0)
            {
                AnsiConsole.MarkupLine("[green]OK[/] All files are up to date!");
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

                    var progress = new Progress<(int current, int total, string fileName)>(report =>
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
                    downloadedFiles = [.. await DownloadItemsAsync(itemsToDownload, outputDirectory, concurrency, progress)];

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
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            logger.DownloadFailed(ex);
        }
    }

    /// <summary>
    /// Gets the default concurrency based on CPU cores
    /// </summary>
    /// <remarks>
    /// Downloads are I/O-bound, not CPU-bound, allowing higher concurrency than CPU cores.
    /// 
    /// NOTE: OneDrive downloads use pre-signed CDN URLs (not OneDrive API), so API rate limiting
    /// is not a concern during downloads. The scan phase (ListItemsAsync) caches all file metadata
    /// once, then downloads proceed in parallel using direct CDN links.
    /// 
    /// Concurrency limits prevent:
    /// - Network bandwidth saturation (too many parallel HTTP streams)
    /// - Memory exhaustion (HttpClient buffers ~10MB per active download)
    /// - CDN throttling (OneDrive CDN may limit connections per IP)
    /// - Poor UX (progress becomes unpredictable with 64+ parallel tasks)
    /// 
    /// Sweet spot: 2-4x CPU cores for I/O-bound operations.
    /// Based on benchmark results from original Bash script.
    /// </remarks>
    private static int GetDefaultConcurrency()
    {
        var processorCount = Environment.ProcessorCount;

        return processorCount switch
        {
            1 => 4,              // Even single-core can handle multiple downloads
            2 => 6,              // Sweet spot for 2-core systems
            <= 4 => 8,           // Good for 4-core systems
            <= 8 => 12,          // 6-8 core systems
            <= 16 => 16,         // 12-16 core systems
            _ => 24              // High-end systems (20+ cores, capped for safety)
        };
    }

    /// <summary>
    /// Displays analysis results in a user-friendly format
    /// </summary>
    private static void DisplayAnalysisResults(DownloadAnalysis analysis, bool isDryRun, bool includeOrphans, bool forceRefresh, bool showFiles)
    {
        var stats = analysis.Statistics;

        // Show file lists first (if --show-files is enabled)
        if (showFiles)
        {
            // Show new files (ALL of them)
            var newItems = analysis.Items.Where(i => i.Status == FileStatus.New).ToList();
            if (newItems is not [])
            {
                AnsiConsole.MarkupLine("[green]NEW FILES:[/]");
                var table = new Table
                {
                    Border = TableBorder.None
                };
                table.AddColumn("Path");
                table.AddColumn("Size");

                foreach (var item in newItems)
                {
                    table.AddRow($"[dim]+[/] {item.RelativePath}", FileSizeFormatter.Format(item.RemoteItem.Size));
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }

            // Show modified files (ALL of them)
            var modifiedItems = analysis.Items.Where(i => i.Status == FileStatus.Modified).ToList();
            if (modifiedItems is not [])
            {
                AnsiConsole.MarkupLine("[yellow]MODIFIED FILES:[/]");
                var table = new Table
                {
                    Border = TableBorder.None
                };
                table.AddColumn("Path");
                table.AddColumn("Reason");

                foreach (var item in modifiedItems)
                {
                    table.AddRow($"[dim]~[/] {item.RelativePath}", item.Reason);
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }

            // Show orphaned files (ALL of them)
            if (includeOrphans && analysis.OrphanedFiles is not [])
            {
                AnsiConsole.MarkupLine("[red]ORPHANED FILES (local only):[/]");
                var table = new Table
                {
                    Border = TableBorder.None
                };
                table.AddColumn("Path");
                table.AddColumn("Size");

                foreach (var file in analysis.OrphanedFiles)
                {
                    var relativePath = file.Name; // Simplified for display
                    table.AddRow($"[dim]-[/] {relativePath}", FileSizeFormatter.Format(file.Length));
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
        }

        // Summary panel (ALWAYS shown, at the end)
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
            summaryText += $"\n[dim]🔄 Force refresh enabled[/]";
        }

        if (!showFiles && (stats.NewFiles > 0 || stats.ModifiedFiles > 0 || stats.OrphanedFiles > 0))
        {
            summaryText += $"\n\n[dim]Run with --show-files to see detailed file list[/]";
        }

        AnsiConsole.Write(new Panel(summaryText)
        {
            Header = new PanelHeader(isDryRun ? "[bold yellow]Preview[/]" : "[bold blue]Summary[/]"),
            Border = BoxBorder.Rounded
        });
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Downloads a list of items with progress reporting
    /// </summary>
    private async Task<List<string>> DownloadItemsAsync(
        List<DownloadItem> items,
        string destinationDirectory,
        int concurrency,
        IProgress<(int current, int total, string fileName)>? progress
    )
    {
        var downloadedFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
        var current = 0;
        var total = items.Count;

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = default
            },
            async (item, ct) =>
            {
                var destinationPath = Path.Combine(destinationDirectory, item.RelativePath);

                // Download file first
                var downloadedPath = await provider.DownloadFileAsync(item.RemoteItem, destinationPath, ct);
                downloadedFiles.Add(downloadedPath);

                // Report progress AFTER download completes
                var currentIndex = Interlocked.Increment(ref current);
                progress?.Report((currentIndex, total, item.RelativePath));
            });

        return [.. downloadedFiles];
    }
}

