using System.IO.Compression;
using NuGet.Packaging;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Logging;

namespace Spectara.Revela.Features.Packages.Services;

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
    /// <param name="requiredPackageType">
    /// Package type the nuspec must declare, checked before anything is written; <c>null</c> accepts any type.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="PackageInstallStatus.Installed"/> with the package (ID, exact version, nuspec package types),
    /// <see cref="PackageInstallStatus.WrongPackageType"/> without writing files, or <see cref="PackageInstallStatus.Failed"/>.
    /// </returns>
    public async Task<PackageInstallResult> ExtractAsync(
        string nupkgPath,
        string targetDir,
        string? requiredPackageType,
        CancellationToken cancellationToken)
    {
        using var packageReader = new PackageArchiveReader(nupkgPath);
        var identity = await packageReader.GetIdentityAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.ExtractingPackage(identity.Id, identity.Version.ToString());
        }

        var nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);
        var package = new InstalledPackage(
            identity.Id,
            identity.Version.ToNormalizedString(),
            [.. nuspecReader.GetPackageTypes().Select(pt => pt.Name)]);
        if (requiredPackageType is not null
            && !package.PackageTypes.Contains(requiredPackageType, StringComparer.OrdinalIgnoreCase))
        {
            logger.WrongPackageType(identity.Id, requiredPackageType);
            return new PackageInstallResult(PackageInstallStatus.WrongPackageType, package);
        }

        // Extract lib/net10.0/*.dll files
        var libItems = await packageReader.GetLibItemsAsync(cancellationToken);
        var targetGroup = libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp" && g.TargetFramework.Version.Major >= 10)
                       ?? libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp");

        if (targetGroup is null || !targetGroup.Items.Any())
        {
            logger.NoCompatibleLibs(identity.Id);
            return new PackageInstallResult(PackageInstallStatus.Failed);
        }

        var fileCount = 0;

        // The ID comes from the untrusted .nuspec and becomes a directory name below.
        var pluginDir = Path.Combine(targetDir, identity.Id);
        if (!PackageIdRules.IsValid(identity.Id) || !PathContainment.IsStrictlyInside(targetDir, pluginDir))
        {
            logger.InvalidPackageId(identity.Id);
            return new PackageInstallResult(PackageInstallStatus.Failed);
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
            if (!PathContainment.IsStrictlyInside(pluginDirFull, destPath))
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
            return new PackageInstallResult(PackageInstallStatus.Failed);
        }

        logger.PackageExtracted(identity.Id, fileCount);
        return new PackageInstallResult(PackageInstallStatus.Installed, package);
    }
}
