using System.IO.Compression;
using System.Text.Json;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core;

/// <summary>
/// Extracts NuGet packages (.nupkg) to the plugin directory and creates metadata files.
/// </summary>
/// <remarks>
/// Handles the low-level extraction of plugin files from .nupkg archives.
/// Each plugin is installed into its own subdirectory for isolation.
/// Metadata is persisted as {PackageId}.meta.json alongside the DLLs.
/// </remarks>
public sealed class NupkgExtractor(ILogger<NupkgExtractor> logger)
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Extracts a .nupkg file to the target directory and creates metadata.
    /// </summary>
    /// <param name="nupkgPath">Path to the .nupkg file.</param>
    /// <param name="targetDir">Root plugin directory (e.g., plugins/).</param>
    /// <param name="installedFrom">Source URL or file path for metadata tracking.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted package identity, or null if extraction failed.</returns>
    public async Task<PackageIdentity?> ExtractAsync(
        string nupkgPath,
        string targetDir,
        string installedFrom,
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
        using var archive = await ZipFile.OpenReadAsync(nupkgPath, cancellationToken);

        // All files go into plugins/{PackageId}/ subfolder
        // This keeps main DLL and dependencies together for clean isolation
        var pluginDir = Path.Combine(targetDir, identity.Id);
        _ = Directory.CreateDirectory(pluginDir);

        foreach (var item in targetGroup.Items)
        {
            var entry = archive.GetEntry(item);
            if (entry is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(item);
            var destPath = Path.Combine(pluginDir, fileName);

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

        // Create plugin.meta.json with metadata from .nuspec (in plugin subfolder)
        await CreateMetadataAsync(packageReader, identity, installedFrom, pluginDir, cancellationToken);

        logger.PackageExtracted(identity.Id, fileCount);
        return identity;
    }

    private async Task CreateMetadataAsync(
        PackageArchiveReader packageReader,
        PackageIdentity identity,
        string installedFrom,
        string targetDir,
        CancellationToken cancellationToken)
    {
        using var nuspecStream = await packageReader.GetNuspecAsync(cancellationToken);
        var nuspecReader = new NuspecReader(nuspecStream);

        // Parse authors
        var authors = nuspecReader.GetAuthors()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? [];

        // Parse package types (e.g., RevelaPlugin, RevelaTheme)
        var packageTypes = nuspecReader.GetPackageTypes()
            .Select(pt => pt.Name)
            .ToList();

        // Parse dependencies
        var dependencyGroups = await packageReader.GetPackageDependenciesAsync(cancellationToken);
        var dependencies = dependencyGroups
            .SelectMany(g => g.Packages)
            .ToDictionary(d => d.Id, d => d.VersionRange.MinVersion?.ToString() ?? "*");

        // Determine source type
        var source = installedFrom.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "url"
                   : File.Exists(installedFrom) ? "nupkg"
                   : "nuget";

        var metadata = new InstalledPluginInfo
        {
            Name = identity.Id,
            Version = identity.Version.ToString(),
            Source = source,
            InstalledFrom = installedFrom,
            InstalledAt = DateTime.UtcNow.ToString("O"),
            Authors = authors,
            Description = nuspecReader.GetDescription(),
            Dependencies = dependencies,
            PackageTypes = packageTypes
        };

        // Write to plugin.meta.json
        var metadataPath = Path.Combine(targetDir, $"{identity.Id}.meta.json");
        await using var fileStream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(fileStream, metadata, MetadataJsonOptions, cancellationToken);

        logger.MetadataCreated(metadataPath);
    }
}
