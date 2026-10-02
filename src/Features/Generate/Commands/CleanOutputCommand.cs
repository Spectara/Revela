using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Cleans the output directory and the output state in <see cref="ProjectPaths.State"/>.
/// </summary>
/// <remarks>
/// <para>
/// The state records what Revela produced in the output (image variants, pre-compressed
/// sidecars); without the output it would describe files that no longer exist.
/// </para>
/// <para>
/// Refuses to delete an output path that is a filesystem root or that is, or contains,
/// the project, source or home directory (see <see cref="DirectoryDeletionGuard"/>).
/// </para>
/// </remarks>
internal sealed partial class CleanOutputCommand(
    ILogger<CleanOutputCommand> logger,
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment) : IPipelineStep
{
    // ── IPipelineStep (service-level, no UI) ──

    string IPipelineStep.Category => PipelineCategories.Clean;

    string IPipelineStep.Name => "output";


    ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidateOutputPath(out var unsafeReason))
        {
            return new ValueTask<OperationResult>(OperationResult.Fail(unsafeReason));
        }

        if (!Directory.Exists(OutputPath))
        {
            return new ValueTask<OperationResult>(DeleteState());
        }

        try
        {
            Directory.Delete(OutputPath, recursive: true);
            LogDirectoryDeleted(logger, OutputPath, 0);
            return new ValueTask<OperationResult>(DeleteState());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, OutputPath, ex);
            return new ValueTask<OperationResult>(OperationResult.Fail($"Failed to delete {OutputPath}: {ex.Message}"));
        }
    }

    // ── CLI command ──
    /// <summary>Gets full path to output directory (supports hot-reload).</summary>
    private string OutputPath => pathResolver.OutputPath;

    /// <summary>Gets full path to the output state (what Revela produced in the output).</summary>
    private string StatePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.State);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("output", $"Clean output directory and its state ({ProjectPaths.State})");

        command.SetAction(async (parseResult, cancellationToken) => await ExecuteAsync(cancellationToken));

        return command;
    }

    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidateOutputPath(out var unsafeReason))
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(unsafeReason)}");
            AnsiConsole.MarkupLine("[dim]Check 'paths.output' in project.json. Nothing was deleted.[/]");
            return Task.FromResult(1);
        }

        // Nothing to clean - exit silently (goal already achieved)
        if (!Directory.Exists(OutputPath))
        {
            return Task.FromResult(ReportStateDeletion(DeleteState()));
        }

        var target = AnalyzeDirectory(OutputPath);

        try
        {
            Directory.Delete(OutputPath, recursive: true);
            LogDirectoryDeleted(logger, target.Path, target.FileCount);

            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Deleted [cyan]{Markup.Escape(OutputPath)}[/] ({target.FileCount} files, {FormatSize(target.TotalSize)})");
        }
        catch (IOException ex)
        {
            LogDeleteFailed(logger, OutputPath, ex);
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to delete {Markup.Escape(OutputPath)}: {Markup.Escape(ex.Message)}");
            return Task.FromResult(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(logger, OutputPath, ex);
            AnsiConsole.MarkupLine($"{OutputMarkers.Error} Access denied: {Markup.Escape(OutputPath)}");
            return Task.FromResult(1);
        }

        return Task.FromResult(ReportStateDeletion(DeleteState()));
    }

    /// <summary>
    /// Deletes the output state: without the output it would claim files that no longer exist.
    /// </summary>
    private OperationResult DeleteState()
    {
        if (!Directory.Exists(StatePath))
        {
            return OperationResult.Ok();
        }

        try
        {
            Directory.Delete(StatePath, recursive: true);
            LogDirectoryDeleted(logger, StatePath, 0);
            return OperationResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogDeleteFailed(logger, StatePath, ex);
            return OperationResult.Fail($"Failed to delete {ProjectPaths.State}: {ex.Message}");
        }
    }

    private static int ReportStateDeletion(OperationResult result)
    {
        if (result.Success)
        {
            return 0;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(result.ErrorMessage ?? string.Empty)}");
        return 1;
    }

    private bool TryValidateOutputPath([NotNullWhen(false)] out string? unsafeReason)
    {
        if (DirectoryDeletionGuard.TryValidateOutputDirectory(
            OutputPath,
            projectEnvironment.Value.Path,
            pathResolver.SourcePath,
            out unsafeReason))
        {
            return true;
        }

        LogUnsafeOutputPath(logger, unsafeReason);
        return false;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Unsafe output path: {Reason}")]
    private static partial void LogUnsafeOutputPath(ILogger logger, string reason);
}

/// <summary>
/// Information about a directory to be cleaned.
/// </summary>
internal sealed record CleanTarget(string Path, int FileCount, long TotalSize);

