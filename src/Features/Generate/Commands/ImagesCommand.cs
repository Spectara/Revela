using System.CommandLine;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Command to process images from manifest.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IImageService.ProcessAsync"/>.
/// Implements <see cref="IPipelineStep"/> for programmatic pipeline execution (MCP/GUI).
/// </para>
/// <para>
/// Usage: revela generate images [--force]
/// </para>
/// </remarks>
internal sealed partial class ImagesCommand(
    ILogger<ImagesCommand> logger,
    IImageService imageService,
    IManifestRepository manifestRepository,
    IConsoleCapabilities consoleCapabilities,
    TimeProvider timeProvider,
    IOptionsMonitor<ProjectConfig> projectConfig) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Generate;

    string IPipelineStep.Name => "images";


    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await imageService.ProcessAsync(new ProcessImagesOptions(), progress: null, cancellationToken);
        return result.Success
            ? OperationResult.Ok()
            : OperationResult.Fail(result.ErrorMessage ?? "Image processing failed");
    }

    // ── CLI command ──
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("images", "Process images from manifest");

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Force rebuild all images (ignore cache)"
        };
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var force = parseResult.GetValue(forceOption);
            return await ExecuteAsync(force, parseResult.IsInPipeline(), cancellationToken);
        });

        return command;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Standalone execution with default options (no force rebuild).
    /// </remarks>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(force: false, inPipeline: false, cancellationToken);

    /// <summary>
    /// Executes the images command with force option.
    /// </summary>
    /// <param name="force">Force rebuild all images (ignore cache).</param>
    /// <param name="inPipeline">Whether processing runs as a pipeline step (suppresses standalone hints).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(bool force, bool inPipeline, CancellationToken cancellationToken)
    {
        try
        {
            // Early manifest check before showing progress bar
            await manifestRepository.LoadAsync(cancellationToken);

            if (manifestRepository.Root is null)
            {
                var warningPanel = new Panel(
                    "[yellow]No manifest found.[/]\n\n" +
                    "[dim]Solution:[/]\n" +
                    "Run [cyan]revela generate scan[/] first to scan your content."
                )
                .WithHeader("[bold yellow]Warning[/]")
                .WithWarningStyle();

                AnsiConsole.Write(warningPanel);
                return 1;
            }

            var options = new ProcessImagesOptions { Force = force };

            if (force)
            {
                AnsiConsole.MarkupLine("[yellow]Force rebuild requested, processing all images...[/]");
            }

            // Empty line before progress display
            AnsiConsole.WriteLine();

            // Two very different output paths:
            //  • Interactive TTY → a single dedicated ~10 Hz renderer redraws the
            //    two-line display from a lock-free snapshot the workers publish.
            //    Workers never render or take the Live() render lock.
            //  • No TTY (redirected/CI/Docker, NO_COLOR) → periodic plain-text
            //    heartbeat lines so the run is never silent. Live() also hides the
            //    cursor, which throws on a non-interactive console, so it stays
            //    gated on the shared capability check.
            var result = consoleCapabilities.CanRenderLive
                ? await AnsiConsole.Live(new Text("Initializing…"))
                    .AutoClear(false)
                    .StartAsync(ctx => RunLiveAsync(ctx, options, cancellationToken))
                : await imageService.ProcessAsync(
                    options,
                    new HeartbeatProgressReporter(Console.Out, timeProvider, HeartbeatInterval),
                    cancellationToken);

            if (result.Success)
            {
                var projectName = projectConfig.CurrentValue.Name;
                if (string.IsNullOrEmpty(projectName))
                {
                    projectName = "Revela Site";
                }

                var content = "[green]Image processing complete![/]\n\n";
                content += $"[dim]Project:[/]   [cyan]{Markup.Escape(projectName)}[/]\n\n";
                content += "[dim]Statistics:[/]\n";

                content += $"  Processed: {result.ProcessedCount} images\n";

                if (result.SkippedCount > 0)
                {
                    content += $"  Cached:    {result.SkippedCount} images\n";
                }

                if (result.FilesCreated > 0)
                {
                    content += $"  Files:     {result.FilesCreated} created\n";
                    content += $"  Size:      {FormatSize(result.TotalSize)} (generated)\n";
                }

                content += $"  Duration:  {result.Duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s";

                if (!inPipeline)
                {
                    content += "\n\n[dim]Next steps:[/]\n";
                    content += "  • Run [cyan]revela generate pages[/] to render HTML\n";
                    content += "  • Or run [cyan]revela generate all[/] for full build";
                }

                var successPanel = new Panel(new Markup(content))
                    .WithHeader("[bold green]Success[/]")
                    .WithSuccessStyle();
                AnsiConsole.Write(successPanel);

                // Display collected warnings (if any) after success panel
                if (result.Warnings.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"{OutputMarkers.Warning} {result.Warnings.Count} warning(s) during processing:");
                    foreach (var warning in result.Warnings.Take(5))
                    {
                        var safeWarning = Markup.Escape(warning);
                        AnsiConsole.MarkupLine($"  [dim]• {safeWarning}[/]");
                    }

                    if (result.Warnings.Count > 5)
                    {
                        AnsiConsole.MarkupLine($"  [dim]... and {result.Warnings.Count - 5} more[/]");
                    }
                }

                return 0;
            }

            var errorPanel = new Panel(
                new Markup($"[red]{Markup.Escape(result.ErrorMessage ?? string.Empty)}[/]"))
                .WithHeader("[bold red]Image processing failed[/]")
                .WithErrorStyle();
            AnsiConsole.Write(errorPanel);
            LogImageProcessingFailed(logger);
            return 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Canceled[/]");
            LogImageProcessingFailed(logger);
            return 1;
        }
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
            < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:F1} KB", bytes / 1024.0),
            < 1024 * 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:F1} MB", bytes / (1024.0 * 1024.0)),
            _ => string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", bytes / (1024.0 * 1024.0 * 1024.0))
        };
    }

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private const int BarWidth = 20;

    /// <summary>
    /// Interactive path: runs the encode concurrently with a single ~10 Hz
    /// renderer. Workers only publish immutable snapshots (lock-free); this loop
    /// is the sole renderer, so no worker ever takes the Live() render lock.
    /// </summary>
    private async Task<ImageResult> RunLiveAsync(
        LiveDisplayContext ctx,
        ProcessImagesOptions options,
        CancellationToken cancellationToken)
    {
        var latest = new StrongBox<ImageProgress?>(null);
        var progress = new SynchronousProgress<ImageProgress>(p => Volatile.Write(ref latest.Value, p));
        var processTask = imageService.ProcessAsync(options, progress, cancellationToken);

        var frame = 0;
        while (!processTask.IsCompleted)
        {
            ctx.UpdateTarget(BuildLiveDisplay(Volatile.Read(ref latest.Value), frame++));
            ctx.Refresh();
            await Task.WhenAny(processTask, Task.Delay(RefreshInterval, cancellationToken));
        }

        // One last frame so the display ends at 100% before the summary panel.
        ctx.UpdateTarget(BuildLiveDisplay(Volatile.Read(ref latest.Value), frame));
        ctx.Refresh();

        return await processTask;
    }

    /// <summary>
    /// Builds the two-line live display:
    /// <code>
    /// Encoding photos  ████████████░░░░░░░░  512/1197 (43%)  ·  18m elapsed  ·  ~24m left
    /// ⠋ 16 workers busy · avif 512 · webp 998 · jpg 1120 · 132 img/min · 3 skipped
    /// </code>
    /// Line 1 counts photos (not variants); line 2 gives liveness + diagnostics.
    /// </summary>
    private static Rows BuildLiveDisplay(ImageProgress? snapshot, int frame)
    {
        if (snapshot is null || snapshot.Total == 0)
        {
            return new Rows(new Markup("[dim]Initializing…[/]"));
        }

        var bar = ImageProgressFormatter.Bar(snapshot.Processed, snapshot.Total, BarWidth, '█', '░');
        var line1 = new Markup(string.Format(
            CultureInfo.InvariantCulture,
            "[bold]Encoding photos[/]  [green]{0}[/]  {1}  [dim]·[/]  {2} elapsed  [dim]·[/]  {3}",
            bar,
            ImageProgressFormatter.Counts(snapshot),
            ImageProgressFormatter.Duration(snapshot.Elapsed),
            snapshot.Eta is { } eta ? $"~{ImageProgressFormatter.Duration(eta)} left" : "estimating…"));

        var parts = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, "{0} workers busy", snapshot.WorkersBusy)
        };

        var formats = string.Join(
            " [dim]·[/] ",
            snapshot.DoneByFormat
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    kvp.Key,
                    kvp.Value)));
        if (formats.Length > 0)
        {
            parts.Add(formats);
        }

        if (snapshot.ImagesPerMinute > 0)
        {
            parts.Add(ImageProgressFormatter.Rate(snapshot.ImagesPerMinute));
        }

        if (snapshot.Skipped > 0)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} skipped", snapshot.Skipped));
        }

        var spinner = SpinnerFrames[frame % SpinnerFrames.Length];
        var line2 = new Markup($"[green]{spinner}[/] " + string.Join(" [dim]·[/] ", parts));

        return new Rows(line1, line2);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing command failed")]
    private static partial void LogImageProcessingFailed(ILogger logger);
}

