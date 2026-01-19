using System.CommandLine;
using System.Globalization;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Compress.Commands;

/// <summary>
/// Cleans compressed files (.gz, .br) from the output directory.
/// </summary>
public sealed partial class CleanCompressCommand(
    ILogger<CleanCompressCommand> logger,
    IPathResolver pathResolver)
{
    /// <summary>Order for this command in clean menu.</summary>
    public const int Order = 40;

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("compress", "Clean compressed files (.gz, .br) from output");

        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputPath = pathResolver.OutputPath;

        // If output doesn't exist, nothing to clean - exit silently
        if (!Directory.Exists(outputPath))
        {
            return Task.FromResult(0);
        }

        // Find all .gz and .br files
        var gzipFiles = Directory.GetFiles(outputPath, "*.gz", SearchOption.AllDirectories);
        var brotliFiles = Directory.GetFiles(outputPath, "*.br", SearchOption.AllDirectories);

        if (gzipFiles.Length == 0 && brotliFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[dim]No compressed files found in output[/]");
            return Task.FromResult(0);
        }

        var gzipSize = DeleteFiles(gzipFiles, cancellationToken);
        var brotliSize = DeleteFiles(brotliFiles, cancellationToken);

        var totalCount = gzipFiles.Length + brotliFiles.Length;
        var totalSize = gzipSize + brotliSize;

        var content = $"[green]Compressed files removed![/]\n\n" +
                      $"[dim]Summary:[/]\n" +
                      $"  Files:   {totalCount}\n" +
                      $"  Size:    {FormatSize(totalSize)}\n\n" +
                      $"[dim]By format:[/]\n" +
                      $"  Gzip:    {gzipFiles.Length} files ({FormatSize(gzipSize)})\n" +
                      $"  Brotli:  {brotliFiles.Length} files ({FormatSize(brotliSize)})";

        var panel = new Panel(new Markup(content))
        {
            Header = new PanelHeader("[bold green]Success[/]")
        };
        panel.WithSuccessStyle();
        AnsiConsole.Write(panel);

        return Task.FromResult(0);
    }

    private long DeleteFiles(string[] files, CancellationToken cancellationToken)
    {
        long totalSize = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
                File.Delete(file);
                LogFileDeleted(logger, file);
            }
            catch (IOException ex)
            {
                LogDeleteFailed(logger, file, ex);
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to delete {file}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogDeleteFailed(logger, file, ex);
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Access denied: {file}");
            }
        }

        return totalSize;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0))
    };

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted: {FilePath}")]
    private static partial void LogFileDeleted(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete {FilePath}")]
    private static partial void LogDeleteFailed(ILogger logger, string filePath, Exception exception);

    #endregion
}
