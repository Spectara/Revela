using System.IO.Compression;
using System.Text.Json;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin installation, updates and removal via NuGet
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor pattern.
/// Creates plugin directory automatically if it doesn't exist.
/// Supports NuGet packages from feeds or local .nupkg files.
/// Multi-source discovery tries all configured sources automatically.
/// HttpClient is injected via Typed HttpClient pattern for proper pooling.
/// </remarks>
public sealed class PluginManager(HttpClient httpClient, ILogger<PluginManager> logger)
{
    /// <summary>
    /// Gets the local plugin directory (next to executable).
    /// </summary>
    public static string LocalPluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>
    /// Gets the global plugin directory (in user's AppData).
    /// </summary>
    public static string GlobalPluginDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Revela", "plugins");
        }
    }

    /// <summary>
    /// Installs a plugin from a package ID, local .nupkg file, or URL.
    /// </summary>
    /// <param name="packageIdOrPath">Package ID (e.g., 'Spectara.Revela.Plugin.Statistics'), local .nupkg path, or HTTP(S) URL</param>
    /// <param name="version">Specific version to install (only for package IDs)</param>
    /// <param name="source">Custom NuGet source URL (null = use default nuget.org)</param>
    /// <param name="global">If true, installs to global AppData; otherwise next to executable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if installation succeeded</returns>
    public async Task<bool> InstallAsync(
        string packageIdOrPath,
        string? version = null,
        string? source = null,
        bool global = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            _ = Directory.CreateDirectory(targetDir);

            // Detect installation type
            if (File.Exists(packageIdOrPath) && packageIdOrPath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                // Local .nupkg file
                logger.InstallingFromFile(packageIdOrPath);
                return await InstallFromNupkgFileAsync(packageIdOrPath, targetDir, Path.GetFullPath(packageIdOrPath), cancellationToken);
            }
            else if (Uri.TryCreate(packageIdOrPath, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // URL to .nupkg
                logger.InstallingFromUrl(packageIdOrPath);
                return await InstallFromUrlAsync(uri, targetDir, cancellationToken);
            }
            else
            {
                // Package ID from NuGet feed
                logger.InstallingPlugin(packageIdOrPath);

                if (source is not null)
                {
                    // Explicit source - try named source or treat as URL
                    var sourceUrl = await ResolveSourceAsync(source, cancellationToken);
                    var sourceRepo = Repository.Factory.GetCoreV3(new PackageSource(sourceUrl));
                    return await InstallFromNuGetAsync(packageIdOrPath, version, sourceRepo, targetDir, cancellationToken);
                }
                else
                {
                    // No explicit source - try all configured sources
                    return await InstallFromMultipleSourcesAsync(packageIdOrPath, version, targetDir, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.InstallFailed(ex, packageIdOrPath);
            return false;
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility. Use InstallAsync instead.
    /// </summary>
    [Obsolete("Use InstallAsync instead")]
    public Task<bool> InstallPluginAsync(string packageId, string? version = null, bool global = false, CancellationToken cancellationToken = default)
        => InstallAsync(packageId, version, source: null, global, cancellationToken);

    private async Task<bool> InstallFromNuGetAsync(
        string packageId,
        string? version,
        SourceRepository sourceRepo,
        string targetDir,
        CancellationToken cancellationToken)
    {
        var resource = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        using var cacheContext = new SourceCacheContext();

        // Get all versions
        var versions = await resource.GetAllVersionsAsync(
            packageId,
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken);

        var targetVersion = version is not null
            ? NuGetVersion.Parse(version)
            : versions.MaxBy(v => v);

        if (targetVersion is null)
        {
            logger.PackageNotFound(packageId);
            return false;
        }

        logger.InstallingVersion(packageId, targetVersion.ToString());

        // Download package to temp file
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var packageStream = File.Create(tempFile))
            {
                var success = await resource.CopyNupkgToStreamAsync(
                    packageId,
                    targetVersion,
                    packageStream,
                    cacheContext,
                    NuGet.Common.NullLogger.Instance,
                    cancellationToken);

                if (!success)
                {
                    logger.DownloadFailed(packageId, targetVersion.ToString());
                    return false;
                }
            }

            // Install from downloaded file - use source URL for metadata
            return await InstallFromNupkgFileAsync(tempFile, targetDir, sourceRepo.PackageSource.Source, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Resolves a source name to URL (checks revela.json feeds, falls back to treating as URL)
    /// </summary>
    private async Task<string> ResolveSourceAsync(string source, CancellationToken cancellationToken)
    {
        // Check if it's already a URL
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return source;
        }

        // Try to resolve as named source
        var sources = await Services.NuGetSourceManager.GetAllSourcesAsync(cancellationToken);
        var namedSource = sources.FirstOrDefault(s => s.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

        if (namedSource is not null)
        {
            logger.UsingNamedSource(source, namedSource.Url);
            return namedSource.Url;
        }

        // Fall back to treating as URL (will fail if invalid)
        logger.SourceNotFoundTreatingAsUrl(source);
        return source;
    }

    /// <summary>
    /// Tries to install from all configured sources until one succeeds
    /// </summary>
    private async Task<bool> InstallFromMultipleSourcesAsync(
        string packageId,
        string? version,
        string targetDir,
        CancellationToken cancellationToken)
    {
        var sources = await Services.NuGetSourceManager.LoadSourcesAsync(cancellationToken);
        logger.TryingMultipleSources(packageId, sources.Count);

        foreach (var source in sources)
        {
            try
            {
                logger.TryingSource(source.Name, source.Url);
                var sourceRepo = Repository.Factory.GetCoreV3(new PackageSource(source.Url));
                var success = await InstallFromNuGetAsync(packageId, version, sourceRepo, targetDir, cancellationToken);

                if (success)
                {
                    logger.SuccessFromSource(packageId, source.Name);
                    return true;
                }
            }
            catch
            {
                // Continue to next source
                logger.SourceFailed(source.Name);
            }
        }

        // All sources failed
        logger.AllSourcesFailed(packageId);
        return false;
    }

    private async Task<bool> InstallFromUrlAsync(Uri url, string targetDir, CancellationToken cancellationToken)
    {
        await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            return await InstallFromNupkgFileAsync(tempFile, targetDir, url.ToString(), cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<bool> InstallFromNupkgFileAsync(string nupkgPath, string targetDir, string installedFrom, CancellationToken cancellationToken)
    {
        using var packageReader = new PackageArchiveReader(nupkgPath);
        var nuspec = await packageReader.GetNuspecAsync(cancellationToken);
        var identity = await packageReader.GetIdentityAsync(cancellationToken);

        logger.ExtractingPackage(identity.Id, identity.Version.ToString());

        // Extract lib/net10.0/*.dll files
        var libItems = await packageReader.GetLibItemsAsync(cancellationToken);
        var net10Group = libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp" && g.TargetFramework.Version.Major >= 10)
                      ?? libItems.FirstOrDefault(g => g.TargetFramework.Framework == ".NETCoreApp");

        if (net10Group is null || !net10Group.Items.Any())
        {
            logger.NoCompatibleLibs(identity.Id);
            return false;
        }

        var fileCount = 0;
        using var archive = await Task.Run(() => ZipFile.OpenRead(nupkgPath), cancellationToken);

        // All files go into plugins/{PackageId}/ subfolder
        // This keeps main DLL and dependencies together for clean isolation
        var pluginDir = Path.Combine(targetDir, identity.Id);
        _ = Directory.CreateDirectory(pluginDir);

        foreach (var item in net10Group.Items)
        {
            var entry = archive.GetEntry(item);
            if (entry is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(item);
            var destPath = Path.Combine(pluginDir, fileName);

            await using var entryStream = await Task.Run(() => entry.Open(), cancellationToken);
            await using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);

            logger.ExtractedFile(fileName, pluginDir);
            fileCount++;
        }

        if (fileCount == 0)
        {
            logger.NoFilesExtracted(identity.Id);
            return false;
        }

        // Create plugin.meta.json with metadata from .nuspec (in plugin subfolder)
        await CreatePluginMetadataAsync(packageReader, identity, installedFrom, pluginDir, cancellationToken);

        // Update project.json if it exists in current directory
        await UpdateProjectJsonAsync(identity.Id, identity.Version.ToString(), cancellationToken);

        logger.PluginInstalled(identity.Id, fileCount);
        return true;
    }

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private async Task CreatePluginMetadataAsync(
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

    /// <summary>
    /// Updates project.json in the current working directory by adding the installed plugin.
    /// </summary>
    /// <remarks>
    /// Silently skips if project.json doesn't exist (optional feature).
    /// Reads, modifies, and writes back with pretty-print formatting.
    /// </remarks>
    private async Task UpdateProjectJsonAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        var projectJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "project.json");
        if (!File.Exists(projectJsonPath))
        {
            // project.json is optional - don't log warning
            return;
        }

        try
        {
            // Read existing project.json
            var jsonText = await File.ReadAllTextAsync(projectJsonPath, cancellationToken);
            using var jsonDoc = JsonDocument.Parse(jsonText);

            // Convert to mutable dictionary
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);
            if (root is null)
            {
                return;
            }

            // Get or create plugins section
            if (!root.TryGetValue("plugins", out var pluginsElement))
            {
                root["plugins"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>());
            }

            var plugins = pluginsElement.Deserialize<Dictionary<string, string>>() ?? [];
            plugins[packageId] = version;

            root["plugins"] = JsonSerializer.SerializeToElement(plugins);

            // Write back with pretty-print
            await using var writeStream = File.Create(projectJsonPath);
            await JsonSerializer.SerializeAsync(writeStream, root, MetadataJsonOptions, cancellationToken);

            logger.ProjectJsonUpdated(projectJsonPath, packageId, version);
        }
        catch (Exception ex)
        {
            // Log warning but don't fail installation
            logger.ProjectJsonUpdateFailed(ex, projectJsonPath);
        }
    }

    /// <summary>
    /// Removes a plugin entry from project.json in the current working directory.
    /// </summary>
    /// <remarks>
    /// Silently skips if project.json doesn't exist or plugin not in file.
    /// </remarks>
    private async Task RemoveFromProjectJsonAsync(string packageId, CancellationToken cancellationToken)
    {
        var projectJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "project.json");
        if (!File.Exists(projectJsonPath))
        {
            return;
        }

        try
        {
            // Read existing project.json
            var jsonText = await File.ReadAllTextAsync(projectJsonPath, cancellationToken);
            using var jsonDoc = JsonDocument.Parse(jsonText);

            // Convert to mutable dictionary
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);
            if (root is null || !root.TryGetValue("plugins", out var pluginsElement))
            {
                return;
            }

            var plugins = pluginsElement.Deserialize<Dictionary<string, string>>() ?? [];
            if (plugins.Remove(packageId))
            {
                root["plugins"] = JsonSerializer.SerializeToElement(plugins);

                // Write back with pretty-print
                await using var writeStream = File.Create(projectJsonPath);
                await JsonSerializer.SerializeAsync(writeStream, root, MetadataJsonOptions, cancellationToken);

                logger.ProjectJsonPluginRemoved(projectJsonPath, packageId);
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't fail uninstallation
            logger.ProjectJsonRemoveFailed(ex, projectJsonPath, packageId);
        }
    }

    public async Task<bool> UpdatePluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        logger.UpdatingPlugin(packageId);
        // Uninstall old version, install new version
        _ = await UninstallPluginAsync(packageId, global, cancellationToken);
        return await InstallAsync(packageId, version: null, source: null, global, cancellationToken);
    }

    public async Task<bool> UninstallPluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.UninstallingPlugin(packageId);

            var pluginDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            var found = false;

            // Delete plugin subdirectory with all contents (main DLL + dependencies)
            // New structure: plugins/{PackageId}/*.dll
            var pluginPath = Path.Combine(pluginDir, packageId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
                found = true;
            }

            // Legacy: Also check for root DLL (old structure or development builds)
            var dllPath = Path.Combine(pluginDir, $"{packageId}.dll");
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
                found = true;
            }

            // Legacy: Delete .meta.json file in root (old structure)
            var metaPath = Path.Combine(pluginDir, $"{packageId}.meta.json");
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            // Note: Keep config file (*.json in plugins/ config folder) - user may want to reinstall with same settings

            if (found)
            {
                // Remove from project.json if it exists
                await RemoveFromProjectJsonAsync(packageId, cancellationToken);

                logger.PluginUninstalled(packageId);
                return true;
            }

            logger.PluginNotFound(packageId);
            return false;
        }
        catch (Exception ex)
        {
            logger.UninstallFailed(ex, packageId);
            return false;
        }
    }

    /// <summary>
    /// Searches all configured NuGet sources for packages matching the search term.
    /// </summary>
    /// <param name="searchTerm">Search term (e.g., "Spectara.Revela.Theme")</param>
    /// <param name="packageTypeFilter">Filter by package type (e.g., "RevelaTheme", "RevelaPlugin")</param>
    /// <param name="includePrerelease">Include prerelease versions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching packages from all sources</returns>
    public async Task<IReadOnlyList<PackageSearchResult>> SearchPackagesAsync(
        string searchTerm,
        string? packageTypeFilter = null,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PackageSearchResult>();
        var sources = await Services.NuGetSourceManager.LoadSourcesAsync(cancellationToken);

        logger.SearchingPackages(searchTerm, sources.Count);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sourceRepo = Repository.Factory.GetCoreV3(new PackageSource(source.Url));
                var searchResource = await sourceRepo.GetResourceAsync<PackageSearchResource>(cancellationToken);

                if (searchResource is null)
                {
                    logger.SearchResourceNotAvailable(source.Name);
                    continue;
                }

                var searchFilter = new SearchFilter(includePrerelease);
                var packages = await searchResource.SearchAsync(
                    searchTerm,
                    searchFilter,
                    skip: 0,
                    take: 50,
                    NuGet.Common.NullLogger.Instance,
                    cancellationToken);

                foreach (var package in packages)
                {
                    // Skip if already added from another source (prefer first source)
                    if (results.Any(r => r.Id.Equals(package.Identity.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Infer package types from naming convention
                    // Spectara.Revela.Theme.* -> RevelaTheme
                    // Spectara.Revela.Plugin.* -> RevelaPlugin
                    var packageTypes = InferPackageTypes(package.Identity.Id);

                    // Apply package type filter if specified
                    if (!string.IsNullOrEmpty(packageTypeFilter))
                    {
                        if (!packageTypes.Contains(packageTypeFilter, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    results.Add(new PackageSearchResult
                    {
                        Id = package.Identity.Id,
                        Version = package.Identity.Version.ToString(),
                        Description = package.Description,
                        Authors = package.Authors?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                        PackageTypes = packageTypes,
                        SourceName = source.Name,
                        DownloadCount = package.DownloadCount
                    });
                }

                logger.SearchSourceCompleted(source.Name, packages.Count());
            }
            catch (Exception ex)
            {
                logger.SearchSourceFailed(source.Name, ex);
            }
        }

        logger.SearchCompleted(searchTerm, results.Count);
        return results;
    }

    /// <summary>
    /// Lists all installed plugins from both local and global directories.
    /// </summary>
    /// <remarks>
    /// Plugins can be installed in two ways:
    /// 1. Subdirectory: plugins/{PackageId}/{PackageId}.dll (with dependencies)
    /// 2. Root DLL: plugins/{PackageId}.dll (development/legacy)
    /// </remarks>
    public static IEnumerable<(string Name, string Location)> ListInstalledPlugins()
    {
        var results = new List<(string Name, string Location)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ScanDirectory(string directory, string location)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            // Check subdirectories first (installed plugins)
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var folderName = Path.GetFileName(subDir);
                var mainDll = Path.Combine(subDir, $"{folderName}.dll");
                if (File.Exists(mainDll) && seen.Add(folderName))
                {
                    results.Add((folderName, location));
                }
            }

            // Check root DLLs (development/legacy)
            foreach (var dll in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (seen.Add(name))
                {
                    results.Add((name, location));
                }
            }
        }

        ScanDirectory(LocalPluginDirectory, "local");
        ScanDirectory(GlobalPluginDirectory, "global");

        return results;
    }

    /// <summary>
    /// Infers package types from naming convention.
    /// </summary>
    /// <remarks>
    /// Since NuGet search API doesn't return PackageTypes directly,
    /// we infer them from the package ID naming convention:
    /// - Spectara.Revela.Theme.* → RevelaTheme
    /// - Spectara.Revela.Plugin.* → RevelaPlugin
    /// </remarks>
    private static List<string> InferPackageTypes(string packageId)
    {
        var types = new List<string>();

        if (packageId.StartsWith("Spectara.Revela.Theme.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add("RevelaTheme");

            // Theme extensions also have RevelaPlugin type
            // e.g., Spectara.Revela.Theme.Lumina.Statistics
            var themePart = packageId["Spectara.Revela.Theme.".Length..];
            if (themePart.Contains('.', StringComparison.Ordinal))
            {
                types.Add("RevelaPlugin");
            }
        }
        else if (packageId.StartsWith("Spectara.Revela.Plugin.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add("RevelaPlugin");
        }

        return types;
    }
}
