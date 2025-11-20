using System.CommandLine;
using Microsoft.Extensions.Configuration;
#pragma warning disable IDE0005 // Using directive is necessary for LoggerMessage attribute
using Microsoft.Extensions.Logging;
#pragma warning restore IDE0005
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to download images from OneDrive shared folder
/// </summary>
public static partial class OneDriveSourceCommand
{
    private const string ConfigFileName = "onedrive.json";

    public static Command Create(IServiceProvider services)
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

            await ExecuteAsync(services, shareUrl, outputDir, force, includePatterns, excludePatterns, concurrency, debug);
            return 0;
        });

        return command;
    }

    private static async Task ExecuteAsync(
        IServiceProvider services,
        string? shareUrl,
        string? outputDirectory,
        bool forceRefresh,
        string[]? includePatterns,
        string[]? excludePatterns,
        int? concurrency,
        bool debug
    )
    {
        var loggerFactory = (ILoggerFactory?)services.GetService(typeof(ILoggerFactory));
        var logger = loggerFactory?.CreateLogger("OneDriveSource");

        // Configure debug logging if requested
        if (debug && loggerFactory is ILoggerFactory factory)
        {
            // Note: This requires LoggerFactory to support AddFilter at runtime
            // In production, this should be configured via appsettings or environment
            AnsiConsole.MarkupLine("[dim]Debug logging enabled[/]");
        }

        try
        {
            // Load configuration from file or use parameters
            var config = await LoadConfigurationAsync(shareUrl, includePatterns, excludePatterns);

            // Determine output directory
            outputDirectory ??= Path.Combine(Directory.GetCurrentDirectory(), "source");

            // Auto-detect concurrency if not specified (like original script)
            concurrency ??= GetDefaultConcurrency();

            // Validate concurrency
            if (concurrency <= 0)
            {
                throw new ArgumentException("Concurrency must be greater than 0", nameof(concurrency));
            }

            AnsiConsole.MarkupLine("[blue]📥 Downloading from OneDrive...[/]");
            AnsiConsole.MarkupLine($"[dim]Share URL:[/] {config.ShareUrl}");
            AnsiConsole.MarkupLine($"[dim]Output:[/] {outputDirectory}");
            AnsiConsole.MarkupLine($"[dim]Concurrency:[/] {concurrency} parallel downloads");
            AnsiConsole.WriteLine();

            // Get SharedLinkProvider from DI (Typed Client pattern)
            // HttpClient is automatically configured and injected
            var provider = (SharedLinkProvider?)services.GetService(typeof(SharedLinkProvider))
                ?? throw new InvalidOperationException("SharedLinkProvider not available - ensure it's registered in Program.cs");

            // Phase 1: Scan OneDrive structure
            IReadOnlyList<OneDriveItem>? allItems = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[yellow]Scanning OneDrive folder structure...[/]", async ctx =>
                {
                    allItems = await provider.ListItemsAsync(config);
                    var fileCount = allItems.Count(i => !i.IsFolder);
                    var folderCount = allItems.Count(i => i.IsFolder);

                    ctx.Status($"[green]✓[/] Found {fileCount} files in {folderCount} folders");
                    await Task.Delay(500); // Brief pause to show result
                });

            if (allItems == null)
            {
                throw new InvalidOperationException("Failed to scan OneDrive folder");
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Scan complete: {allItems.Count(i => !i.IsFolder)} files, {allItems.Count(i => i.IsFolder)} folders");
            AnsiConsole.WriteLine();

            // Phase 2: Download files with progress
            var downloadedFiles = new List<string>();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Downloading files[/]", maxValue: 100);
                    task.IsIndeterminate = true; // Start indeterminate until we know the total

                    var progress = new Progress<(int current, int total, string fileName)>(report =>
                    {
                        if (task.IsIndeterminate && report.total > 0)
                        {
                            task.IsIndeterminate = false;
                            task.MaxValue = report.total;
                        }

                        task.Value = report.current;
                        // Escape markup in filename to avoid Spectre.Console parsing issues
                        var safeFileName = report.fileName
                            .Replace("[", "[[", StringComparison.Ordinal)
                            .Replace("]", "]]", StringComparison.Ordinal);
                        task.Description = $"[green]Downloading[/] ({report.current}/{report.total}) {safeFileName}";
                    });

                    downloadedFiles = [.. await provider.DownloadAllAsync(config, outputDirectory, forceRefresh, concurrency.Value, progress, allItems)];

                    task.StopTask();
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

            if (logger != null)
            {
                LogDownloadFailed(logger, ex);
            }
        }
    }

    /// <summary>
    /// Loads configuration from multiple sources using ConfigurationBuilder
    /// </summary>
    /// <remarks>
    /// Configuration sources (in priority order, highest to lowest):
    /// 1. Command-line parameters (--share-url, --include, --exclude)
    /// 2. Environment variables (REVELA_ONEDRIVE_*)
    /// 3. Configuration file (onedrive.json)
    /// 
    /// Patterns are optional - if not specified, smart defaults are used:
    /// - All images (detected via MIME type: image/*)
    /// - All markdown files (*.md)
    /// </remarks>
    private static Task<OneDriveConfig> LoadConfigurationAsync(
        string? shareUrl,
        string[]? includePatterns,
        string[]? excludePatterns
    )
    {
        OneDriveConfig? fileConfig = null;

        try
        {
            // Build configuration from multiple sources
            // Note: JSON file is loaded from current working directory
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "REVELA_ONEDRIVE_")
                .Build();

            // Bind to OneDriveConfig
            fileConfig = configuration.Get<OneDriveConfig>();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to load configuration: {ex.Message}");
        }

        // Command-line parameters override file/environment config
        shareUrl ??= fileConfig?.ShareUrl;

        // Patterns are optional - null means use smart defaults
        includePatterns ??= fileConfig?.IncludePatterns?.ToArray();
        excludePatterns ??= fileConfig?.ExcludePatterns?.ToArray();

        // Validate that we have at least a share URL
        if (string.IsNullOrWhiteSpace(shareUrl))
        {
            throw new InvalidOperationException(
                "No OneDrive share URL provided. Use one of these methods:\n" +
                $"1. Run 'revela source onedrive init' to create {ConfigFileName}\n" +
                "2. Set environment variable: REVELA_ONEDRIVE_ShareUrl=<url>\n" +
                "3. Provide --share-url parameter"
            );
        }

        var config = new OneDriveConfig
        {
            ShareUrl = shareUrl,
            IncludePatterns = includePatterns,
            ExcludePatterns = excludePatterns
        };

        // Validate configuration using Data Annotations
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(config);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(config, validationContext, validateAllProperties: true);

        return Task.FromResult(config);
    }

    /// <summary>
    /// Gets the default concurrency based on CPU cores (like original script)
    /// </summary>
    /// <remarks>
    /// Downloads are I/O-bound, not CPU-bound.
    /// Optimal concurrency is higher than CPU cores for network downloads.
    /// Based on benchmark results from original script.
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
            _ => 24              // High-end systems (20+ cores)
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "OneDrive download failed")]
    private static partial void LogDownloadFailed(ILogger logger, Exception exception);
}
