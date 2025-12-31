using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Clean.Commands;

/// <summary>
/// Cleans the cache directory.
/// </summary>
public sealed partial class CleanCacheCommand(
    ILogger<CleanCacheCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment)
{
    /// <summary>Order for this command in menu.</summary>
    public const int Order = 20;

    /// <summary>Cache directory name.</summary>
    private const string CacheDirectory = ".cache";

    /// <summary>Gets full path to cache directory.</summary>
    private string CachePath => Path.Combine(projectEnvironment.Value.Path, CacheDirectory);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("cache", "Clean cache directory (.cache)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(CachePath))
        {
            AnsiConsole.MarkupLine($"[dim]{CacheDirectory}/[/] [yellow]does not exist[/]");
            return Task.FromResult(0);
        }

        var target = AnalyzeDirectory(CachePath);

        try
        {
            Directory.Delete(CachePath, recursive: true);
            LogDirectoryDeleted(logger, target.Path, target.FileCount);

            AnsiConsole.MarkupLine($"[green]✓[/] Deleted [cyan]{CacheDirectory}/[/] ({target.FileCount} files, {FormatSize(target.TotalSize)})");
        }
        catch (IOException ex)
        {
            LogDeleteFailed(logger, CachePath, ex);
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete {CacheDirectory}: {ex.Message}");
            return Task.FromResult(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(logger, CachePath, ex);
            AnsiConsole.MarkupLine($"[red]✗[/] Access denied: {CacheDirectory}");
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
