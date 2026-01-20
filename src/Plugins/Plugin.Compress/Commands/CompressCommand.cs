using System.CommandLine;
using System.Globalization;

using Spectara.Revela.Plugin.Compress.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Compress.Commands;

/// <summary>
/// Command to compress static files in the output directory.
/// </summary>
/// <remarks>
/// <para>
/// Creates .gz (Gzip) and .br (Brotli) files alongside originals.
/// </para>
/// <para>
/// <b>Note:</b> This command is NOT included in 'generate all' pipeline.
/// Pre-compression requires server configuration (nginx gzip_static, Apache mod_deflate, etc.).
/// Run explicitly with 'revela generate compress' when needed.
/// </para>
/// </remarks>
public sealed partial class CompressCommand(
    ILogger<CompressCommand> logger,
    IPathResolver pathResolver,
    CompressionService compressionService) : IGenerateStep
{
    /// <summary>Order for compress step - runs after images (400).</summary>
    public const int StepOrder = 500;

    /// <inheritdoc />
    public string Name => "compress";

    /// <inheritdoc />
    public string Description => "Compress static files (Gzip/Brotli)";

    /// <inheritdoc />
    public int Order => StepOrder;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("compress", "Compress static files with Gzip and Brotli");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var outputPath = pathResolver.OutputPath;

        // Check if output directory exists
        if (!Directory.Exists(outputPath))
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Output directory does not exist: [dim]{outputPath}[/]");
            AnsiConsole.MarkupLine("[dim]Run [cyan]revela generate pages[/] first to create output files.[/]");
            return 0;
        }

        LogStartingCompression(logger, outputPath);

        CompressionStats? stats = null;

        // Compress with progress display
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Compressing[/]");
                task.IsIndeterminate = true;

                var progress = new Progress<(int current, int total, string fileName)>(report =>
                {
                    if (task.IsIndeterminate && report.total > 0)
                    {
                        task.IsIndeterminate = false;
                        task.MaxValue = report.total;
                    }
                    task.Value = report.current;

                    // Escape Spectre markup in filenames
                    var safeName = report.fileName
                        .Replace("[", "[[", StringComparison.Ordinal)
                        .Replace("]", "]]", StringComparison.Ordinal);

                    task.Description = $"[green]Compressing[/] ({report.current}/{report.total}) {safeName}";
                });

                stats = await compressionService.CompressDirectoryAsync(outputPath, progress, cancellationToken)
                    .ConfigureAwait(false);

                task.Value = task.MaxValue;
                task.Description = "[green]Compression complete[/]";
            }).ConfigureAwait(false);

        if (stats is null || stats.TotalFiles == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Info} No files to compress");
            return 0;
        }

        // Display success panel with statistics
        DisplaySuccessPanel(stats);

        return 0;
    }

    /// <summary>
    /// Displays compression statistics in a success panel.
    /// </summary>
    private static void DisplaySuccessPanel(CompressionStats stats)
    {
        var content = $"[green]Compression complete![/]\n\n" +
                      $"[dim]Summary:[/]\n" +
                      $"  Files:    {stats.TotalFiles}\n" +
                      $"  Original: {CompressionService.FormatSize(stats.Gzip.OriginalSize)}\n\n" +
                      $"[dim]Compressed sizes:[/]\n" +
                      string.Format(
                          CultureInfo.InvariantCulture,
                          "  Gzip:    {0} ({1:0.0}% savings)\n",
                          CompressionService.FormatSize(stats.Gzip.CompressedSize),
                          stats.Gzip.SavingsPercent) +
                      string.Format(
                          CultureInfo.InvariantCulture,
                          "  Brotli:  {0} ({1:0.0}% savings)",
                          CompressionService.FormatSize(stats.Brotli.CompressedSize),
                          stats.Brotli.SavingsPercent);

        if (stats.SkippedCount > 0)
        {
            content += $"\n\n[dim]Skipped:[/] {stats.SkippedCount} files (< 256 bytes)";
        }

        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader("[bold green]Success[/]")
        };
        panel.WithSuccessStyle();
        AnsiConsole.Write(panel);
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting compression in {OutputPath}")]
    private static partial void LogStartingCompression(ILogger logger, string outputPath);

    #endregion
}
