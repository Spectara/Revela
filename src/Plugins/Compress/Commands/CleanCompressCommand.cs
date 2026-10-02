using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Compress.Commands;

/// <summary>
/// Cleans compressed files (.gz, .br) from the output directory.
/// </summary>
internal sealed partial class CleanCompressCommand(
    ILogger<CleanCompressCommand> logger,
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "compress";


    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputPath = pathResolver.OutputPath;
        try
        {
            using var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, StateDirectory, cancellationToken);
            await ownership.CleanAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, outputPath, exception);
            return OperationResult.Fail($"Could not clean compressed files: {exception.Message}");
        }
    }

    private string StateDirectory => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.State);

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("compress", "Clean compressed files (.gz, .br) from output");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(cancellationToken));

        return command;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputPath = pathResolver.OutputPath;

        // If output doesn't exist, nothing to clean - exit silently
        if (!Directory.Exists(outputPath))
        {
            return 0;
        }

        CompressionStats stats;
        try
        {
            using var ownership = await CompressedSiteOwnership.OpenAsync(outputPath, StateDirectory, cancellationToken);
            stats = await ownership.CleanAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, outputPath, exception);
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Could not clean compressed files: {Markup.Escape(exception.Message)}");
            return 1;
        }

        if (stats.Gzip.FileCount == 0 && stats.Brotli.FileCount == 0)
        {
            AnsiConsole.MarkupLine("[dim]No compressed files found in output[/]");
            return 0;
        }

        var gzipSize = stats.Gzip.CompressedSize;
        var brotliSize = stats.Brotli.CompressedSize;
        var totalCount = stats.Gzip.FileCount + stats.Brotli.FileCount;
        var totalSize = gzipSize + brotliSize;

        var content = $"[green]Compressed files removed![/]\n\n" +
                      $"[dim]Summary:[/]\n" +
                      $"  Files:   {totalCount}\n" +
                      $"  Size:    {FormatSize(totalSize)}\n\n" +
                      $"[dim]By format:[/]\n" +
                      $"  Gzip:    {stats.Gzip.FileCount} files ({FormatSize(gzipSize)})\n" +
                      $"  Brotli:  {stats.Brotli.FileCount} files ({FormatSize(brotliSize)})";

        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader("[bold green]Success[/]")
        };
        panel.WithSuccessStyle();
        AnsiConsole.Write(panel);

        return 0;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0))
    };

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete {FilePath}")]
    private static partial void LogDeleteFailed(ILogger logger, string filePath, Exception exception);

    #endregion
}
