using System.IO.Compression;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Logging;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin installation, updates and removal via NuGet
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor pattern.
/// Creates plugin directory automatically if it doesn't exist.
/// Supports NuGet packages from feeds or local .nupkg files.
/// </remarks>
public sealed class PluginManager(ILogger<PluginManager> logger)
{
    private readonly SourceRepository repository = Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json"));

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
                return await InstallFromNupkgFileAsync(packageIdOrPath, targetDir, cancellationToken);
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
                var sourceRepo = source is not null
                    ? Repository.Factory.GetCoreV3(new PackageSource(source))
                    : repository;
                return await InstallFromNuGetAsync(packageIdOrPath, version, sourceRepo, targetDir, cancellationToken);
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

            // Install from downloaded file
            return await InstallFromNupkgFileAsync(tempFile, targetDir, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<bool> InstallFromUrlAsync(Uri url, string targetDir, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            return await InstallFromNupkgFileAsync(tempFile, targetDir, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<bool> InstallFromNupkgFileAsync(string nupkgPath, string targetDir, CancellationToken cancellationToken)
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

        foreach (var item in net10Group.Items)
        {
            var entry = archive.GetEntry(item);
            if (entry is null)
            {
                continue;
            }

            var fileName = Path.GetFileName(item);
            var destPath = Path.Combine(targetDir, fileName);

            await using var entryStream = await Task.Run(() => entry.Open(), cancellationToken);
            await using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);

            logger.ExtractedFile(fileName, targetDir);
            fileCount++;
        }

        // Also extract .deps.json if present
        var depsEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase));
        if (depsEntry is not null)
        {
            var destPath = Path.Combine(targetDir, depsEntry.Name);
            await using var entryStream = await Task.Run(() => depsEntry.Open(), cancellationToken);
            await using var fileStream = File.Create(destPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);
            logger.ExtractedFile(depsEntry.Name, targetDir);
            fileCount++;
        }

        if (fileCount == 0)
        {
            logger.NoFilesExtracted(identity.Id);
            return false;
        }

        logger.PluginInstalled(identity.Id, fileCount);
        return true;
    }



    public async Task<bool> UpdatePluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        logger.UpdatingPlugin(packageId);
        // Uninstall old version, install new version
        _ = await UninstallPluginAsync(packageId, global, cancellationToken);
        return await InstallAsync(packageId, version: null, source: null, global, cancellationToken);
    }

    public Task<bool> UninstallPluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            logger.UninstallingPlugin(packageId);

            var pluginDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            var found = false;

            // Delete plugin directory with dependencies
            var pluginPath = Path.Combine(pluginDir, packageId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
                found = true;
            }

            // Delete main DLL file
            var dllPath = Path.Combine(pluginDir, $"{packageId}.dll");
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
                found = true;
            }

            // Delete .deps.json file
            var depsPath = Path.Combine(pluginDir, $"{packageId}.deps.json");
            if (File.Exists(depsPath))
            {
                File.Delete(depsPath);
                found = true;
            }

            // Note: Keep config file (*.json) - user may want to reinstall with same settings

            if (found)
            {
                logger.PluginUninstalled(packageId);
                return Task.FromResult(true);
            }

            logger.PluginNotFound(packageId);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.UninstallFailed(ex, packageId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Lists all installed plugins from both local and global directories.
    /// </summary>
    /// <remarks>
    /// Lists all DLLs in the root of plugin directories.
    /// Dependencies should be in subfolders (named after the plugin DLL).
    /// Convention: plugins/MyPlugin.dll + plugins/MyPlugin/*.dll (deps)
    /// </remarks>
    public static IEnumerable<(string Name, string Location)> ListInstalledPlugins()
    {
        var results = new List<(string Name, string Location)>();

        // Check local plugins (only root DLLs, not in subfolders)
        if (Directory.Exists(LocalPluginDirectory))
        {
            foreach (var dll in Directory.GetFiles(LocalPluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                results.Add((Path.GetFileNameWithoutExtension(dll), "local"));
            }
        }

        // Check global plugins (only root DLLs, not in subfolders)
        if (Directory.Exists(GlobalPluginDirectory))
        {
            foreach (var dll in Directory.GetFiles(GlobalPluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                results.Add((Path.GetFileNameWithoutExtension(dll), "global"));
            }
        }

        return results;
    }
}

