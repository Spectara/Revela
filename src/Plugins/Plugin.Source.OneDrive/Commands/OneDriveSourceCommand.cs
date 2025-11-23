using System.CommandLine;
using Microsoft.Extensions.Options;
#pragma warning disable IDE0005 // Using directive is necessary for LoggerMessage attribute
using Microsoft.Extensions.Logging;
#pragma warning restore IDE0005
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
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
public sealed partial class OneDriveSourceCommand(
    ILogger<OneDriveSourceCommand> logger,
    SharedLinkProvider provider,
    IOptionsMonitor<OneDrivePluginConfig> config)
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
            Description = "Output directory (defaults to ./source)"
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

        command.Options.Add(shareUrlOption);
        command.Options.Add(outputOption);
        command.Options.Add(forceOption);
        command.Options.Add(includeOption);
        command.Options.Add(excludeOption);
        command.Options.Add(concurrencyOption);
        command.Options.Add(debugOption);

        command.SetAction(async parseResult =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);
            var outputDir = parseResult.GetValue(outputOption);
            var force = parseResult.GetValue(forceOption);
            var includePatterns = parseResult.GetValue(includeOption);
            var excludePatterns = parseResult.GetValue(excludeOption);
            var concurrency = parseResult.GetValue(concurrencyOption);
            var debug = parseResult.GetValue(debugOption);

            await ExecuteAsync(shareUrl, outputDir, force, includePatterns, excludePatterns, concurrency, debug);
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
        bool debug
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

            AnsiConsole.MarkupLine("[blue]📥 Downloading from OneDrive...[/]");
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

                    ctx.Status($"[green]✓[/] Found {fileCount} files in {folderCount} folders");
                    await Task.Delay(500); // Brief pause to show result
                });

            if (allItems is null)
            {
                throw new InvalidOperationException("Failed to scan OneDrive folder");
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Scan complete: {allItems.Count(i => !i.IsFolder)} files, {allItems.Count(i => i.IsFolder)} folders");
            AnsiConsole.WriteLine();

            // Phase 2: Download files with progress
            var downloadedFiles = new List<string>();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)  // Show completed downloads
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    // Main progress task (shows overall progress)
                    var mainTask = ctx.AddTask($"[green]Overall Progress[/]", maxValue: 100);
                    mainTask.IsIndeterminate = true;

                    var progress = new Progress<(int current, int total, string fileName)>(report =>
                    {
                        // Update main task
                        if (mainTask.IsIndeterminate && report.total > 0)
                        {
                            mainTask.IsIndeterminate = false;
                            mainTask.MaxValue = report.total;
                        }
                        mainTask.Value = report.current;
                        mainTask.Description = $"[green]Overall Progress[/] ({report.current}/{report.total}) - {concurrency} parallel downloads";
                    });

                    downloadedFiles = [.. await provider.DownloadAllAsync(downloadConfig, outputDirectory, forceRefresh, concurrency, progress, allItems)];

                    mainTask.StopTask();
                });

            // Success message
            var panel = new Panel(
                $"[green]✨ Downloaded {downloadedFiles.Count} file(s)![/]\n\n" +
                $"[bold]Files saved to:[/] [cyan]{outputDirectory}[/]\n\n" +
                $"[dim]Next steps:[/]\n" +
                $"1. Run [cyan]revela generate[/] to process images\n" +
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
            LogDownloadFailed(logger, ex);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "OneDrive download failed")]
    private static partial void LogDownloadFailed(ILogger logger, Exception exception);
}
