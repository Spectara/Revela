using System.IO.Compression;
using NuGet.Packaging;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Extracts NuGet packages (.nupkg) to the plugin directory.
/// </summary>
/// <remarks>
/// Handles the low-level extraction of plugin files from .nupkg archives.
/// Each plugin is installed into its own subdirectory for isolation.
/// </remarks>
public sealed class NupkgExtractor(ILogger<NupkgExtractor> logger)
{
    /// <summary>
    /// Extracts a .nupkg file into its own subdirectory of the target directory.
    /// </summary>
    /// <param name="nupkgPath">Path to the .nupkg file.</param>
    /// <param name="targetDir">Root plugin directory (e.g., plugins/).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed package (ID, exact version, nuspec package types), or null if extraction failed.</returns>
    public async Task<InstalledPackage?> ExtractAsync(
        string nupkgPath,
        string targetDir,
        CancellationToken cancellationToken)
    {
        using var packageReader = new PackageArchiveReader(nupkgPath);
        var identity = await packageReader.GetIdentityAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.ExtractingPackage(identity.Id, identity.Version.ToString());
        }

        // Extract lib/net10.0/*.dll files
        var libItems = await packageReader.GetLibItemsAsync(cancellationToken);
        var targetGroup = libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp" && g.TargetFramework.Version.Major >= 10)
                       ?? libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp");

        if (targetGroup is null || !targetGroup.Items.Any())
        {
            logger.NoCompatibleLibs(identity.Id);
            return null;
        }

        var fileCount = 0;

        // The ID comes from the untrusted .nuspec and becomes a directory name below.
        var pluginDir = Path.Combine(targetDir, identity.Id);
        if (!PackageIdRules.IsValid(identity.Id) || !PackageIdRules.IsContainedIn(targetDir, pluginDir))
        {
            logger.InvalidPackageId(identity.Id);
            return null;
        }

        using var archive = await ZipFile.OpenReadAsync(nupkgPath, cancellationToken);

        // All files go into plugins/{PackageId}/ subfolder
        // This keeps main DLL and dependencies together for clean isolation
        _ = Directory.CreateDirectory(pluginDir);

        var pluginDirFull = Path.GetFullPath(pluginDir);

        foreach (var item in targetGroup.Items)
        {
            var entry = archive.GetEntry(item);
            if (entry is null)
            {
                continue;
            }

            // Defense-in-depth against ZipSlip: even though we already strip the path with
            // Path.GetFileName, refuse anything that decodes to an absolute or escaping path
            // before composing destPath.
            var fileName = Path.GetFileName(item);
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(fileName))
            {
                logger.SkippedSuspiciousEntry(item);
                continue;
            }

            var destPath = Path.GetFullPath(Path.Combine(pluginDirFull, fileName));
            var rel = Path.GetRelativePath(pluginDirFull, destPath);
            if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            {
                logger.SkippedSuspiciousEntry(item);
                continue;
            }

            await using var entryStream = await entry.OpenAsync(cancellationToken);
            await using var fileStream = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true);
            await entryStream.CopyToAsync(fileStream, cancellationToken);

            logger.ExtractedFile(fileName, pluginDir);
            fileCount++;
        }

        if (fileCount == 0)
        {
            logger.NoFilesExtracted(identity.Id);
            return null;
        }

        var nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);
        var packageTypes = nuspecReader.GetPackageTypes().Select(pt => pt.Name).ToList();

        logger.PackageExtracted(identity.Id, fileCount);
        return new InstalledPackage(identity.Id, identity.Version.ToNormalizedString(), packageTypes);
    }
}
