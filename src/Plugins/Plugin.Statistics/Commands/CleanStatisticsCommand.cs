using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Statistics.Commands;

/// <summary>
/// Cleans statistics JSON files from the cache directory.
/// </summary>
public sealed partial class CleanStatisticsCommand(
    ILogger<CleanStatisticsCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment)
{
    /// <summary>Order for this command in menu.</summary>
    public const int Order = 30;

    /// <summary>Statistics JSON filename.</summary>
    private const string StatisticsFileName = "statistics.json";

    /// <summary>Gets full path to cache directory.</summary>
    private string CachePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Clean statistics JSON files from cache");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    private Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // If cache doesn't exist, nothing to clean - exit silently
        // (this is expected when running after 'clean cache' or 'clean all')
        if (!Directory.Exists(CachePath))
        {
            return Task.FromResult(0);
        }

        // Find all statistics.json files in .cache
        var statsFiles = Directory.GetFiles(CachePath, StatisticsFileName, SearchOption.AllDirectories);

        if (statsFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[dim]No statistics.json files found in cache[/]");
            return Task.FromResult(0);
        }

        var deletedCount = 0;
        long totalSize = 0;

        foreach (var file in statsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileInfo = new FileInfo(file);
                totalSize += fileInfo.Length;
                File.Delete(file);
                deletedCount++;
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

        if (deletedCount > 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Deleted [cyan]{deletedCount}[/] statistics.json file(s) ({FormatSize(totalSize)})");
        }

        return Task.FromResult(0);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0))
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted statistics file: {Path}")]
    private static partial void LogFileDeleted(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete {Path}")]
    private static partial void LogDeleteFailed(ILogger logger, string path, Exception exception);
}
