using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Core;

/// <summary>
/// Orchestrates plugin installation, updates, and removal via NuGet.
/// </summary>
/// <remarks>
/// Delegates extraction to <see cref="NupkgExtractor"/>, project.json management
/// to <see cref="PluginProjectService"/>, and search to <see cref="PackageSearchService"/>.
/// HttpClient is injected via Typed HttpClient pattern for URL-based downloads.
/// </remarks>
public sealed class PackageManager(
    HttpClient httpClient,
    NupkgExtractor extractor,
    PluginProjectService projectService,
    ILogger<PackageManager> logger,
    INuGetSourceManager nugetSourceManager)
{
    /// <summary>
    /// Gets the bundled packages directory (next to executable).
    /// </summary>
    /// <remarks>
    /// Used as a local NuGet feed for offline-first installation.
    /// Contains .nupkg files bundled with the application.
    /// </remarks>
    public static string BundledPackagesDirectory => Path.Combine(AppContext.BaseDirectory, "packages");

    /// <summary>
    /// Gets the plugin directory based on installation type.
    /// </summary>
    /// <remarks>
    /// Standalone: {exe-dir}/plugins
    /// dotnet tool: %APPDATA%/Revela/plugins
    /// </remarks>
    public static string PluginDirectory => ConfigPathResolver.LocalPluginDirectory;

    /// <summary>
    /// Installs a plugin from a package ID, local .nupkg file, or URL.
    /// </summary>
    /// <param name="packageIdOrPath">Package ID (e.g., 'Spectara.Revela.Plugins.Statistics'), local .nupkg path, or HTTP(S) URL.</param>
    /// <param name="version">Specific version to install (only for package IDs).</param>
    /// <param name="source">Custom NuGet source URL (null = use default nuget.org).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if installation succeeded.</returns>
    public async Task<bool> InstallAsync(
        string packageIdOrPath,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDir = PluginDirectory;
            _ = Directory.CreateDirectory(targetDir);

            // Detect installation type
            if (File.Exists(packageIdOrPath) && packageIdOrPath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                // Local .nupkg file
                logger.InstallingFromFile(packageIdOrPath);
                return await InstallFromNupkgAsync(packageIdOrPath, targetDir, Path.GetFullPath(packageIdOrPath), cancellationToken);
            }
            else if (Uri.TryCreate(packageIdOrPath, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    // URL to .nupkg
                    logger.InstallingFromUrl(packageIdOrPath);
                    return await InstallFromUrlAsync(uri, targetDir, cancellationToken);
                }
                else if (uri.Scheme == Uri.UriSchemeFile)
                {
                    // file:///path/to/plugin.nupkg → treat as local path
                    var filePath = uri.LocalPath;

                    if (!File.Exists(filePath))
                    {
                        logger.LocalPackageNotFound(filePath);
                        return false;
                    }

                    if (!filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LocalPackageNotNupkg(filePath);
                        return false;
                    }

                    logger.InstallingFromFile(filePath);
                    return await InstallFromNupkgAsync(filePath, targetDir, filePath, cancellationToken);
                }
            }

            // Fall through: treat as NuGet package ID
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
    /// Updates a plugin to the latest version by reinstalling it.
    /// </summary>
    /// <param name="packageId">The NuGet package ID of the plugin to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the update succeeded.</returns>
    public async Task<bool> UpdatePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        logger.UpdatingPlugin(packageId);
        _ = await UninstallPluginAsync(packageId, cancellationToken);
        return await InstallAsync(packageId, version: null, source: null, cancellationToken);
    }

    /// <summary>
    /// Uninstalls a plugin by removing its files and project.json entry.
    /// </summary>
    /// <remarks>
    /// Handles both new structure (plugins/{PackageId}/) and legacy (root DLL).
    /// Plugin configuration files are preserved for potential reinstallation.
    /// </remarks>
    /// <param name="packageId">The NuGet package ID of the plugin to uninstall.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was found and removed.</returns>
    public async Task<bool> UninstallPluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.UninstallingPlugin(packageId);

            var pluginDir = PluginDirectory;
            var found = false;

            // Delete plugin subdirectory with all contents (main DLL + dependencies)
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

            if (found)
            {
                await projectService.RemovePluginAsync(packageId, cancellationToken);
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
    /// Lists all installed plugins from the plugin directory.
    /// </summary>
    /// <remarks>
    /// Plugins can be installed in two ways:
    /// 1. Subdirectory: plugins/{PackageId}/{PackageId}.dll (with dependencies)
    /// 2. Root DLL: plugins/{PackageId}.dll (development/legacy)
    /// </remarks>
    public static IEnumerable<(string Name, string Location)> ListInstalledPlugins()
    {
        List<(string Name, string Location)> results = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(PluginDirectory))
        {
            return results;
        }

        // Check subdirectories first (installed plugins)
        foreach (var subDir in Directory.GetDirectories(PluginDirectory))
        {
            var folderName = Path.GetFileName(subDir);
            var mainDll = Path.Combine(subDir, $"{folderName}.dll");
            if (File.Exists(mainDll) && seen.Add(folderName))
            {
                results.Add((folderName, "installed"));
            }
        }

        // Check root DLLs (development/legacy)
        foreach (var dll in Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (seen.Add(name))
            {
                results.Add((name, "installed"));
            }
        }

        return results;
    }

    private async Task<bool> InstallFromNuGetAsync(
        string packageId,
        string? version,
        SourceRepository sourceRepo,
        string targetDir,
        CancellationToken cancellationToken)
    {
        var resource = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        using var cacheContext = new SourceCacheContext();

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.InstallingVersion(packageId, targetVersion.ToString());
        }

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

            return await InstallFromNupkgAsync(tempFile, targetDir, sourceRepo.PackageSource.Source, cancellationToken);
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
        await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            return await InstallFromNupkgAsync(tempFile, targetDir, url.ToString(), cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<bool> InstallFromNupkgAsync(string nupkgPath, string targetDir, string installedFrom, CancellationToken cancellationToken)
    {
        var identity = await extractor.ExtractAsync(nupkgPath, targetDir, installedFrom, cancellationToken);
        if (identity is null)
        {
            return false;
        }

        await projectService.AddPluginAsync(identity.Id, identity.Version.ToString(), cancellationToken);
        logger.PluginInstalled(identity.Id);
        return true;
    }

    private async Task<bool> InstallFromMultipleSourcesAsync(
        string packageId,
        string? version,
        string targetDir,
        CancellationToken cancellationToken)
    {
        var sources = await nugetSourceManager.LoadSourcesAsync(cancellationToken);
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
                logger.SourceFailed(source.Name);
            }
        }

        logger.AllSourcesFailed(packageId);
        return false;
    }

    private async Task<string> ResolveSourceAsync(string source, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return source;
        }

        var sources = await nugetSourceManager.GetAllSourcesAsync(cancellationToken);
        var namedSource = sources.FirstOrDefault(s => s.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

        if (namedSource is not null)
        {
            logger.UsingNamedSource(source, namedSource.Url);
            return namedSource.Url;
        }

        logger.SourceNotFoundTreatingAsUrl(source);
        return source;
    }
}
