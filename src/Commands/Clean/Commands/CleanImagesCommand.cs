using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Commands.Clean.Commands;

/// <summary>
/// Intelligently cleans unused image files from the output directory.
/// </summary>
/// <remarks>
/// <para>
/// Unlike 'clean output' which deletes everything, this command selectively removes:
/// </para>
/// <list type="bullet">
///   <item><description>Orphaned folders - images no longer in manifest (source was deleted)</description></item>
///   <item><description>Unused sizes - widths not in current theme config</description></item>
///   <item><description>Unused formats - formats disabled in current project config</description></item>
/// </list>
/// <para>
/// This is useful after changing image configuration (formats/sizes) without
/// wanting to regenerate all images.
/// </para>
/// <para>
/// <strong>Important:</strong> Run 'revela generate scan' first to ensure the manifest
/// reflects the current source directory state.
/// </para>
/// </remarks>
internal sealed partial class CleanImagesCommand(
    ILogger<CleanImagesCommand> logger,
    IPathResolver pathResolver,
    IManifestRepository manifestRepository,
    IOptionsMonitor<GenerateConfig> generateConfig,
    IOptionsMonitor<ThemeConfig> themeConfig)
{
    /// <summary>Order for this command in menu (between output=10 and cache=20).</summary>
    public const int Order = 15;

    /// <summary>Images subdirectory within output.</summary>
    private const string ImagesDirectory = "images";

    /// <summary>Gets full path to images output directory.</summary>
    private string ImagesPath => Path.Combine(pathResolver.OutputPath, ImagesDirectory);

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("images", "Clean unused image files (orphaned, wrong sizes/formats)");

        var dryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would be deleted without actually deleting"
        };
        command.Options.Add(dryRunOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var dryRun = parseResult.GetValue(dryRunOption);
            return await ExecuteAsync(dryRun, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(bool dryRun, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Load manifest first
        await manifestRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Check if images directory exists
        if (!Directory.Exists(ImagesPath))
        {
            AnsiConsole.MarkupLine("[dim]No images directory found - nothing to clean[/]");
            return 0;
        }

        // Get current configuration
        var activeFormats = generateConfig.CurrentValue.Images.GetActiveFormats();
        var configuredSizes = themeConfig.CurrentValue.Images.Sizes;

        if (activeFormats.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No image formats configured - run 'revela config image' first");
            return 1;
        }

        // Build set of valid image names from manifest
        var validImageNames = BuildValidImageNames();

        // Safety check: warn if manifest is empty but images exist
        if (validImageNames.Count == 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Manifest contains no images - run 'revela generate scan' first");
            AnsiConsole.MarkupLine("[dim]This prevents accidentally deleting all images when manifest is empty.[/]");
            return 1;
        }

        // Analyze what needs to be cleaned
        var analysis = AnalyzeImagesDirectory(validImageNames, activeFormats.Keys, configuredSizes, cancellationToken);

        if (analysis.IsEmpty)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Images directory is clean - nothing to remove");
            return 0;
        }

        // Display what will be cleaned
        DisplayAnalysis(analysis, dryRun);

        if (dryRun)
        {
            AnsiConsole.MarkupLine($"\n[dim]Dry run - no files were deleted. Remove --dry-run to delete.[/]");
            return 0;
        }

        // Perform cleanup
        var result = PerformCleanup(analysis, cancellationToken);

        // Display summary
        DisplaySummary(result);

        return 0;
    }

    /// <summary>
    /// Builds a set of valid image folder names from the manifest.
    /// </summary>
    private HashSet<string> BuildValidImageNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in manifestRepository.Images.Values)
        {
            // Image folder name is the filename without extension
            var folderName = Path.GetFileNameWithoutExtension(image.Filename);
            names.Add(folderName);
        }

        LogValidImagesFound(logger, names.Count);
        return names;
    }

    /// <summary>
    /// Analyzes the images directory to find files that should be cleaned.
    /// </summary>
    private CleanAnalysis AnalyzeImagesDirectory(
        HashSet<string> validImageNames,
        IEnumerable<string> activeFormats,
        IReadOnlyList<int> configuredSizes,
        CancellationToken cancellationToken)
    {
        var orphanedFolders = new List<CleanItem>();
        var unusedSizeFiles = new List<CleanItem>();
        var unusedFormatFiles = new List<CleanItem>();

        var activeFormatSet = new HashSet<string>(activeFormats, StringComparer.OrdinalIgnoreCase);
        var configuredSizeSet = new HashSet<int>(configuredSizes);

        // Build lookup for per-image sizes (includes original size)
        var imageSizesLookup = manifestRepository.Images.ToDictionary(
            kvp => Path.GetFileNameWithoutExtension(kvp.Value.Filename),
            kvp => new HashSet<int>(kvp.Value.Sizes),
            StringComparer.OrdinalIgnoreCase);

        foreach (var imageDir in Directory.EnumerateDirectories(ImagesPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = Path.GetFileName(imageDir);

            // Check if this folder is orphaned (not in manifest)
            if (!validImageNames.Contains(folderName))
            {
                var (fileCount, totalSize) = GetDirectoryStats(imageDir);
                orphanedFolders.Add(new CleanItem(imageDir, totalSize, fileCount, IsDirectory: true));
                LogOrphanedFolder(logger, folderName);
                continue;
            }

            // Get allowed sizes for this specific image
            var allowedSizes = imageSizesLookup.GetValueOrDefault(folderName) ?? configuredSizeSet;

            // Check individual files in non-orphaned folders
            foreach (var file in Directory.EnumerateFiles(imageDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file).TrimStart('.');

                // Parse size from filename (e.g., "640.jpg" → 640)
                var sizeStr = Path.GetFileNameWithoutExtension(fileName);
                if (!int.TryParse(sizeStr, out var size))
                {
                    // Non-standard filename, skip
                    continue;
                }

                var fileSize = new FileInfo(file).Length;

                // Check format first (higher priority) - case-insensitive comparison
                if (!activeFormatSet.Contains(extension))
                {
                    unusedFormatFiles.Add(new CleanItem(file, fileSize, 1, IsDirectory: false));
                    LogUnusedFormat(logger, fileName, extension);
                    continue;
                }

                // Check size (must be in configured sizes OR in image's allowed sizes)
                if (!configuredSizeSet.Contains(size) && !allowedSizes.Contains(size))
                {
                    unusedSizeFiles.Add(new CleanItem(file, fileSize, 1, IsDirectory: false));
                    LogUnusedSize(logger, fileName, size);
                }
            }
        }

        return new CleanAnalysis(orphanedFolders, unusedSizeFiles, unusedFormatFiles);
    }

    /// <summary>
    /// Displays the analysis results.
    /// </summary>
    private static void DisplayAnalysis(CleanAnalysis analysis, bool dryRun)
    {
        var verb = dryRun ? "Would delete" : "Will delete";

        if (analysis.OrphanedFolders.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Orphaned folders[/] (source images deleted):");
            foreach (var item in analysis.OrphanedFolders.Take(10))
            {
                var name = Path.GetFileName(item.Path);
                AnsiConsole.MarkupLine($"  [dim]•[/] {name}/ ({item.FileCount} files, {FormatSize(item.Size)})");
            }

            if (analysis.OrphanedFolders.Count > 10)
            {
                AnsiConsole.MarkupLine($"  [dim]... and {analysis.OrphanedFolders.Count - 10} more[/]");
            }
        }

        if (analysis.UnusedSizeFiles.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Unused sizes[/] (not in current config):");
            var grouped = analysis.UnusedSizeFiles
                .GroupBy(f => int.Parse(Path.GetFileNameWithoutExtension(Path.GetFileName(f.Path)), CultureInfo.InvariantCulture))
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var totalSize = group.Sum(f => f.Size);
                AnsiConsole.MarkupLine($"  [dim]•[/] {group.Key}px: {group.Count()} files ({FormatSize(totalSize)})");
            }
        }

        if (analysis.UnusedFormatFiles.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Unused formats[/] (disabled in config):");
            var grouped = analysis.UnusedFormatFiles
                .GroupBy(f => Path.GetExtension(f.Path).TrimStart('.'), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var totalSize = group.Sum(f => f.Size);
                AnsiConsole.MarkupLine($"  [dim]•[/] .{group.Key}: {group.Count()} files ({FormatSize(totalSize)})");
            }
        }

        AnsiConsole.MarkupLine($"\n[bold]{verb}:[/] {analysis.TotalFileCount} files ({FormatSize(analysis.TotalSize)})");
    }

    /// <summary>
    /// Performs the actual cleanup.
    /// </summary>
    private CleanResult PerformCleanup(CleanAnalysis analysis, CancellationToken cancellationToken)
    {
        var deletedFiles = 0;
        var deletedSize = 0L;
        var errors = 0;

        // Delete orphaned folders first (entire directories)
        foreach (var folder in analysis.OrphanedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Directory.Delete(folder.Path, recursive: true);
                deletedFiles += folder.FileCount;
                deletedSize += folder.Size;
                LogFolderDeleted(logger, folder.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogDeleteFailed(logger, folder.Path, ex);
                errors++;
            }
        }

        // Delete individual files (unused sizes/formats)
        var filesToDelete = analysis.UnusedSizeFiles.Concat(analysis.UnusedFormatFiles);
        var affectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in filesToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var directory = Path.GetDirectoryName(file.Path);
                if (directory != null)
                {
                    affectedDirectories.Add(directory);
                }

                File.Delete(file.Path);
                deletedFiles++;
                deletedSize += file.Size;
                LogFileDeleted(logger, file.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogDeleteFailed(logger, file.Path, ex);
                errors++;
            }
        }

        // Clean up empty directories
        var emptyDirsDeleted = 0;
        foreach (var dir in affectedDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try
                {
                    Directory.Delete(dir);
                    emptyDirsDeleted++;
                    LogEmptyFolderDeleted(logger, dir);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogDeleteFailed(logger, dir, ex);
                }
            }
        }

        return new CleanResult(deletedFiles, deletedSize, emptyDirsDeleted, errors);
    }

    /// <summary>
    /// Displays the cleanup summary.
    /// </summary>
    private static void DisplaySummary(CleanResult result)
    {
        if (result.DeletedFiles > 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Deleted [cyan]{result.DeletedFiles}[/] files ({FormatSize(result.DeletedSize)})");
        }

        if (result.EmptyFoldersDeleted > 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Removed [cyan]{result.EmptyFoldersDeleted}[/] empty folder(s)");
        }

        if (result.Errors > 0)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} [yellow]{result.Errors}[/] item(s) could not be deleted");
        }
    }

    private static (int fileCount, long totalSize) GetDirectoryStats(string path)
    {
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        var totalSize = files.Sum(f => new FileInfo(f).Length);
        return (files.Length, totalSize);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
        < 1024 * 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0)),
        _ => string.Format(CultureInfo.InvariantCulture, "{0:0.##} GB", bytes / (1024.0 * 1024.0 * 1024.0))
    };

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} valid images in manifest")]
    private static partial void LogValidImagesFound(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Orphaned folder: {FolderName}")]
    private static partial void LogOrphanedFolder(ILogger logger, string folderName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unused format: {FileName} (format: {Format})")]
    private static partial void LogUnusedFormat(ILogger logger, string fileName, string format);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unused size: {FileName} (size: {Size}px)")]
    private static partial void LogUnusedSize(ILogger logger, string fileName, int size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted folder: {Path}")]
    private static partial void LogFolderDeleted(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted file: {Path}")]
    private static partial void LogFileDeleted(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed empty folder: {Path}")]
    private static partial void LogEmptyFolderDeleted(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete {Path}")]
    private static partial void LogDeleteFailed(ILogger logger, string path, Exception exception);

    #endregion
}

/// <summary>
/// Item to be cleaned (file or directory).
/// </summary>
internal sealed record CleanItem(string Path, long Size, int FileCount, bool IsDirectory);

/// <summary>
/// Analysis result of what needs to be cleaned.
/// </summary>
internal sealed record CleanAnalysis(
    List<CleanItem> OrphanedFolders,
    List<CleanItem> UnusedSizeFiles,
    List<CleanItem> UnusedFormatFiles)
{
    public bool IsEmpty => OrphanedFolders.Count == 0 && UnusedSizeFiles.Count == 0 && UnusedFormatFiles.Count == 0;
    public int TotalFileCount => OrphanedFolders.Sum(f => f.FileCount) + UnusedSizeFiles.Count + UnusedFormatFiles.Count;
    public long TotalSize => OrphanedFolders.Sum(f => f.Size) + UnusedSizeFiles.Sum(f => f.Size) + UnusedFormatFiles.Sum(f => f.Size);
}

/// <summary>
/// Result of the cleanup operation.
/// </summary>
internal sealed record CleanResult(int DeletedFiles, long DeletedSize, int EmptyFoldersDeleted, int Errors);
