using Microsoft.Extensions.Options;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Features.Generate.Commands;

/// <summary>
/// Shared implementation of the generic clean commands (<c>cache</c>, <c>output</c>, <c>all</c>).
/// </summary>
/// <remarks>
/// The commands delete no folders themselves: they ask every registered owner, core and
/// plugins alike, to invalidate its artifacts of the selected kinds (<see cref="IArtifactLifecycle.InvalidateAllAsync"/>).
/// Durable artifacts are never selected.
/// </remarks>
internal sealed partial class KindClean(
    ILogger<KindClean> logger,
    IArtifactLifecycle artifactLifecycle,
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment)
{
    /// <summary>
    /// Invalidates all artifacts of the given kinds.
    /// </summary>
    /// <remarks>
    /// When output artifacts are selected, an unsafe output path (filesystem root, home, project
    /// or source directory) fails before any owner deletes anything.
    /// </remarks>
    public async Task<OperationResult> RunAsync(
        IReadOnlyCollection<ArtifactKind> kinds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (kinds.Contains(ArtifactKind.Output)
            && !OutputArtifactFiles.TryValidate(pathResolver.OutputPath, pathResolver, projectEnvironment, out var unsafeOutput))
        {
            LogUnsafeOutputPath(logger, unsafeOutput.ErrorMessage ?? string.Empty);
            return unsafeOutput;
        }

        var result = await artifactLifecycle.InvalidateAllAsync(kinds, cancellationToken);
        if (!result.Success)
        {
            LogCleanFailed(logger, result.ErrorMessage ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Runs the clean for the CLI and prints the outcome.
    /// </summary>
    /// <returns>Exit code (0 = success, 1 = failure).</returns>
    public async Task<int> RunCommandAsync(
        IReadOnlyCollection<ArtifactKind> kinds,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(kinds, cancellationToken);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} {successMessage}");
            return 0;
        }

        AnsiConsole.MarkupLine($"{OutputMarkers.Error} {Markup.Escape(result.ErrorMessage ?? "Clean failed")}");
        if (kinds.Contains(ArtifactKind.Output))
        {
            AnsiConsole.MarkupLine("[dim]If the output path is wrong, check 'paths.output' in project.json.[/]");
        }

        return 1;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unsafe output path: {Reason}")]
    private static partial void LogUnsafeOutputPath(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Clean failed: {Reason}")]
    private static partial void LogCleanFailed(ILogger logger, string reason);
}
