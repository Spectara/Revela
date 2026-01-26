using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to process images from manifest.
/// </summary>
/// <remarks>
/// <para>
/// Thin CLI wrapper that delegates to <see cref="IImageService.ProcessAsync"/>.
/// Implements <see cref="IGenerateStep"/> for pipeline orchestration.
/// </para>
/// <para>
/// Usage: revela generate images [--force]
/// </para>
/// </remarks>
public sealed partial class ImagesCommand(
    ILogger<ImagesCommand> logger,
    IImageService imageService,
    IManifestRepository manifestRepository,
    IOptionsMonitor<ProjectConfig> projectConfig) : IGenerateStep
{
    /// <inheritdoc />
    public string Name => "images";

    /// <inheritdoc />
    public string Description => "Process images from manifest";

    /// <inheritdoc />
    public int Order => GenerateStepOrder.Images;

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
            return await ExecuteAsync(force, cancellationToken);
        });

        return command;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Called by pipeline orchestration. Uses default options (no force rebuild).
    /// </remarks>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(force: false, cancellationToken);

    /// <summary>
    /// Executes the images command with force option.
    /// </summary>
    /// <param name="force">Force rebuild all images (ignore cache).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ExecuteAsync(bool force, CancellationToken cancellationToken)
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

            var result = await AnsiConsole.Live(new Text("Initializing..."))
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    var progress = new Progress<ImageProgress>(p => ctx.UpdateTarget(RenderProgress(p)));

                    return await imageService.ProcessAsync(options, progress, cancellationToken);
                });

            if (result.Success)
            {
                var projectName = projectConfig.CurrentValue.Name;
                if (string.IsNullOrEmpty(projectName))
                {
                    projectName = "Revela Site";
                }

                var content = "[green]Image processing complete![/]\n\n";
                content += $"[dim]Project:[/]   [cyan]{projectName}[/]\n\n";
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

                content += $"  Duration:  {result.Duration.TotalSeconds:F2}s\n";
                content += "\n[dim]Next steps:[/]\n";
                content += "  • Run [cyan]revela generate pages[/] to render HTML\n";
                content += "  • Or run [cyan]revela generate[/] for full build";

                var successPanel = new Panel(new Markup(content))
                    .WithHeader("[bold green]Success[/]")
                    .WithSuccessStyle();
                AnsiConsole.Write(successPanel);

                // Display collected warnings (if any) after success panel
                if (result.Warnings.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]⚠ {result.Warnings.Count} warning(s) during processing:[/]");
                    foreach (var warning in result.Warnings.Take(5))
                    {
                        var safeWarning = warning
                            .Replace("[", "[[", StringComparison.Ordinal)
                            .Replace("]", "]]", StringComparison.Ordinal);
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
                new Markup($"[red]{result.ErrorMessage}[/]"))
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

    /// <summary>
    /// Render the progress display for Live Display.
    /// </summary>
    /// <remarks>
    /// Format like original Bash Expose script:
    /// <code>
    /// 029081.jpg ■ ■ ■ □ □ □
    /// 029088.jpg ■ ■ □ □ □ □
    /// 029135.jpg ■ □ □ □ □ □
    ///
    /// ━━━━━━━━━━░░░░░░░░░░  12/1000 (1%)
    /// </code>
    /// Symbols: ■ = done, » = skipped (cache), □ = pending
    /// </remarks>
    private static Rows RenderProgress(ImageProgress p)
    {
        var rows = new List<IRenderable>();

        // Get total worker count (for fixed row reservation)
        var workerCount = p.Workers.Count;

        // Render all workers (active ones with progress, idle ones as empty lines)
        foreach (var worker in p.Workers)
        {
            if (worker.IsIdle)
            {
                // Empty line for idle worker
                rows.Add(new Text(""));
            }
            else
            {
                // Escape Spectre markup in filename
                var safeName = worker.ImageName?
                    .Replace("[", "[[", StringComparison.Ordinal)
                    .Replace("]", "]]", StringComparison.Ordinal) ?? "";

                // Build variant symbols from VariantResults list (ordered)
                // ■ = done (green), ■ = skipped (dim), □ = pending
                var symbols = new List<string>();

                // First: show completed variants in their actual order
                foreach (var result in worker.VariantResults)
                {
                    symbols.Add(result == VariantResult.Done
                        ? "[green]■[/]"  // Generated
                        : "[dim]■[/]");   // Skipped (exists)
                }

                // Then: fill remaining with pending symbols
                var remaining = worker.VariantsTotal - worker.VariantResults.Count;
                for (var i = 0; i < remaining; i++)
                {
                    symbols.Add("[dim]□[/]");
                }

                var symbolLine = string.Join(" ", symbols);
                rows.Add(new Markup($"[cyan]{safeName,-20}[/] {symbolLine}"));
            }
        }

        // Add empty line before progress bar
        if (workerCount > 0)
        {
            rows.Add(new Text(""));
        }

        // Render progress bar
        var progressWidth = 40;
        var percentage = p.Total > 0 ? (double)p.Processed / p.Total : 0;
        var filledWidth = (int)(percentage * progressWidth);

        var filled = new string('━', filledWidth);
        var empty = new string('━', progressWidth - filledWidth);
        var percentValue = (int)(percentage * 100);

        rows.Add(new Markup($"[green]{filled}[/][dim]{empty}[/]  {p.Processed}/{p.Total} ({percentValue}%)"));

        return new Rows(rows);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Image processing command failed")]
    private static partial void LogImageProcessingFailed(ILogger logger);
}
