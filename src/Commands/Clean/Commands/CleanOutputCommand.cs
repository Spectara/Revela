using System.CommandLine;
using System.Globalization;

using Spectre.Console;

namespace Spectara.Revela.Commands.Clean.Commands;

/// <summary>
/// Cleans the output directory.
/// </summary>
public sealed partial class CleanOutputCommand(ILogger<CleanOutputCommand> logger)
{
    /// <summary>Order for this command in menu.</summary>
    public const int Order = 10;

    /// <summary>Output directory for generated site.</summary>
    private const string OutputDirectory = "output";

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("output", "Clean output directory (generated HTML/images)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(OutputDirectory))
        {
            AnsiConsole.MarkupLine($"[dim]{OutputDirectory}/[/] [yellow]does not exist[/]");
            return Task.FromResult(0);
        }

        var target = AnalyzeDirectory(OutputDirectory);

        try
        {
            Directory.Delete(OutputDirectory, recursive: true);
            LogDirectoryDeleted(logger, target.Path, target.FileCount);

            AnsiConsole.MarkupLine($"[green]✓[/] Deleted [cyan]{OutputDirectory}/[/] ({target.FileCount} files, {FormatSize(target.TotalSize)})");
        }
        catch (IOException ex)
        {
            LogDeleteFailed(logger, OutputDirectory, ex);
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete {OutputDirectory}: {ex.Message}");
            return Task.FromResult(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(logger, OutputDirectory, ex);
            AnsiConsole.MarkupLine($"[red]✗[/] Access denied: {OutputDirectory}");
            return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }

    private static CleanTarget AnalyzeDirectory(string path)
    {
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return new CleanTarget(path, files.Length, totalSize);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        < 1024 * 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0)),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.##} GB", bytes / (1024.0 * 1024.0 * 1024.0))
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Path} ({FileCount} files)")]
    private static partial void LogDirectoryDeleted(ILogger logger, string path, int fileCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete {Path}")]
    private static partial void LogDeleteFailed(ILogger logger, string path, Exception exception);
}

/// <summary>
/// Information about a directory to be cleaned.
/// </summary>
internal sealed record CleanTarget(string Path, int FileCount, long TotalSize);
