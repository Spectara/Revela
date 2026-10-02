using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Statistics.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Plugins.Statistics.Commands;

/// <summary>
/// Cleans statistics JSON files from the cache directory.
/// </summary>
internal sealed partial class CleanStatisticsCommand(
    ILogger<CleanStatisticsCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment) : IPipelineStep
{
    private string CachePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache);

    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "statistics";

    ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletion = Delete(cancellationToken);
        return new ValueTask<OperationResult>(deletion.Failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail(
                $"Could not delete '{deletion.Failures[0].Path}': {deletion.Failures[0].Message}"));
    }

    // ── CLI command ──

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("statistics", "Clean statistics JSON files from cache");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Deletes all statistics JSON files and reports the result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success, 1 = a file could not be deleted).</returns>
    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // A missing cache is expected after 'clean cache' or 'clean all': exit silently.
        if (!Directory.Exists(CachePath))
        {
            return Task.FromResult(0);
        }

        var deletion = Delete(cancellationToken);

        foreach (var failure in deletion.Failures)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to delete {Markup.Escape(failure.Path)}: {Markup.Escape(failure.Message)}");
        }

        if (deletion.DeletedCount > 0)
        {
            AnsiConsole.MarkupLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{OutputMarkers.Success} Deleted [cyan]{deletion.DeletedCount}[/] {StatisticsDataInvalidator.FileName} file(s) ({deletion.DeletedBytes / 1024.0:0.#} KB)"));
        }
        else if (deletion.Failures.Count == 0)
        {
            AnsiConsole.MarkupLine($"[dim]No {StatisticsDataInvalidator.FileName} files found in cache[/]");
        }

        return Task.FromResult(deletion.Failures.Count == 0 ? 0 : 1);
    }

    private DerivedFileDeletion Delete(CancellationToken cancellationToken)
    {
        var deletion = DerivedFiles.DeleteAll(CachePath, StatisticsDataInvalidator.FileName, cancellationToken);
        foreach (var failure in deletion.Failures)
        {
            LogDeleteFailed(logger, failure.Path, failure.Message);
        }

        return deletion;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete {Path}: {Reason}")]
    private static partial void LogDeleteFailed(ILogger logger, string path, string reason);
}
