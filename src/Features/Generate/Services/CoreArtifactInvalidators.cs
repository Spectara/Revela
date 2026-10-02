using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Removes the scan manifest (<see cref="CoreArtifacts.Manifest"/>, <see cref="ArtifactKind.Cache"/>).
/// </summary>
/// <remarks>The next scan rebuilds it from the source files.</remarks>
internal sealed class ManifestInvalidator(IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    public ArtifactId Artifact => CoreArtifacts.Manifest;

    public ArtifactKind Kind => ArtifactKind.Cache;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [];

    public ValueTask<OperationResult> InvalidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifestPath = ManifestService.GetManifestPath(projectEnvironment.Value.Path);
        return ValueTask.FromResult(OutputArtifactFiles.DeleteFiles(manifestPath, manifestPath + ".tmp"));
    }
}

/// <summary>
/// Removes the rendered site (<see cref="CoreArtifacts.RenderedSite"/>, <see cref="ArtifactKind.Output"/>):
/// everything in the output directory except the processed images.
/// </summary>
internal sealed class RenderedSiteInvalidator(
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    public ArtifactId Artifact => CoreArtifacts.RenderedSite;

    public ArtifactKind Kind => ArtifactKind.Output;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [];

    public ValueTask<OperationResult> InvalidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = pathResolver.OutputPath;
        if (!Directory.Exists(outputPath))
        {
            return ValueTask.FromResult(OperationResult.Ok());
        }

        if (!OutputArtifactFiles.TryValidate(outputPath, pathResolver, projectEnvironment, out var failure))
        {
            return ValueTask.FromResult(failure);
        }

        return ValueTask.FromResult(OutputArtifactFiles.Run(() =>
        {
            foreach (var entry in new DirectoryInfo(outputPath).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is DirectoryInfo && string.Equals(entry.Name, ProcessedImagesInvalidator.ImagesDirectory, StringComparison.Ordinal))
                {
                    continue;
                }

                OutputArtifactFiles.Delete(entry);
            }

            OutputArtifactFiles.DeleteIfEmpty(outputPath);
        }));
    }
}

/// <summary>
/// Removes the processed images (<see cref="CoreArtifacts.ProcessedImages"/>, <see cref="ArtifactKind.Output"/>):
/// the <c>images</c> folder in the output and the image processing state that describes it.
/// </summary>
internal sealed class ProcessedImagesInvalidator(
    IPathResolver pathResolver,
    IOptions<ProjectEnvironment> projectEnvironment) : IArtifactInvalidator
{
    /// <summary>The folder in the output that holds the image variants.</summary>
    internal const string ImagesDirectory = "images";

    public ArtifactId Artifact => CoreArtifacts.ProcessedImages;

    public ArtifactKind Kind => ArtifactKind.Output;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [];

    public ValueTask<OperationResult> InvalidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outputPath = pathResolver.OutputPath;
        var outputExists = Directory.Exists(outputPath);
        if (outputExists && !OutputArtifactFiles.TryValidate(outputPath, pathResolver, projectEnvironment, out var failure))
        {
            return ValueTask.FromResult(failure);
        }

        return ValueTask.FromResult(OutputArtifactFiles.Run(() =>
        {
            if (outputExists)
            {
                var imagesPath = new DirectoryInfo(Path.Combine(outputPath, ImagesDirectory));
                if (imagesPath.Exists)
                {
                    OutputArtifactFiles.Delete(imagesPath);
                }

                OutputArtifactFiles.DeleteIfEmpty(outputPath);
            }

            // The state goes last: while variants remain, it must keep describing them.
            var statePath = ImageStateStore.GetStatePath(projectEnvironment.Value.Path);
            OutputArtifactFiles.DeleteFile(statePath);
            OutputArtifactFiles.DeleteFile(statePath + ".tmp");
        }));
    }
}

/// <summary>
/// Deletion helpers shared by the core artifact invalidators.
/// </summary>
internal static class OutputArtifactFiles
{
    /// <summary>
    /// Applies the destructive-path guard to the configured output directory.
    /// </summary>
    public static bool TryValidate(
        string outputPath,
        IPathResolver pathResolver,
        IOptions<ProjectEnvironment> projectEnvironment,
        out OperationResult failure)
    {
        if (DirectoryDeletionGuard.TryValidateOutputDirectory(
                outputPath,
                projectEnvironment.Value.Path,
                pathResolver.SourcePath,
                out var reason))
        {
            failure = OperationResult.Ok();
            return true;
        }

        failure = OperationResult.Fail(reason);
        return false;
    }

    public static OperationResult DeleteFiles(params string[] paths) => Run(() =>
    {
        foreach (var path in paths)
        {
            DeleteFile(path);
        }
    });

    /// <summary>Deletes a file; a missing file or folder is not an error.</summary>
    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static OperationResult Run(Action delete)
    {
        try
        {
            delete();
            return OperationResult.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Fail(exception.Message);
        }
    }

    /// <summary>
    /// Deletes a file or folder; a linked folder is removed as a link, never followed.
    /// </summary>
    public static void Delete(FileSystemInfo entry)
    {
        if (entry is DirectoryInfo directory)
        {
            directory.Delete(recursive: (directory.Attributes & FileAttributes.ReparsePoint) == 0);
        }
        else
        {
            entry.Delete();
        }
    }

    public static void DeleteIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }
}
