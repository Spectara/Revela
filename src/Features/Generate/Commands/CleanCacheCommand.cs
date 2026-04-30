using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Cleans the cache directory.
/// </summary>
internal sealed partial class CleanCacheCommand(
    ILogger<CleanCacheCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "cache";


    ValueTask<PipelineStepResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(CachePath))
        {
            return new ValueTask<PipelineStepResult>(PipelineStepResult.Ok());
        }

        try
        {
            Directory.Delete(CachePath, recursive: true);
            LogDirectoryDeleted(logger, CachePath, 0);
            return new ValueTask<PipelineStepResult>(PipelineStepResult.Ok());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, CachePath, ex);
            return new ValueTask<PipelineStepResult>(PipelineStepResult.Fail($"Failed to delete cache: {ex.Message}"));
        }
    }

    // ── CLI command ──
    /// <summary>Gets full path to cache directory.</summary>
    private string CachePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("cache", "Clean cache directory (.cache)");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Nothing to clean - exit silently (goal already achieved)
        if (!Directory.Exists(CachePath))
        {
            return Task.FromResult(0);
        }

        var target = AnalyzeDirectory(CachePath);

        try
        {
            Directory.Delete(CachePath, recursive: true);
            LogDirectoryDeleted(logger, target.Path, target.FileCount);

            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Deleted [cyan]{ProjectPaths.Cache}/[/] ({target.FileCount} files, {FormatSize(target.TotalSize)})");
        }
        catch (IOException ex)
        {
            LogDeleteFailed(logger, CachePath, ex);
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to delete {ProjectPaths.Cache}: {Markup.Escape(ex.Message)}");
            return Task.FromResult(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(logger, CachePath, ex);
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Access denied: {ProjectPaths.Cache}");
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

