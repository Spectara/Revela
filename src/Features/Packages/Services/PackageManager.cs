using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;

using NuGetPackageSource = NuGet.Configuration.PackageSource;

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
    INuGetSourceManager nugetSourceManager,
    IBuildInfo buildInfo) : IPackageInstaller
{
    /// <summary>
    /// Gets the bundled packages directory (next to executable).
    /// </summary>
    /// <remarks>
    /// Used as a local NuGet feed for offline-first installation.
    /// Contains .nupkg files bundled with the application.
    /// </remarks>
    public static string BundledPackagesDirectory => ConfigPathResolver.BundledPackagesDirectory;

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
    /// <param name="packageId">Package ID (e.g., 'Spectara.Revela.Plugins.Statistics'), local .nupkg path, or HTTPS URL.</param>
    /// <param name="version">
    /// Specific version to install (only for package IDs). When null, empty or <c>"latest"</c>, the highest stable version is chosen;
    /// prerelease versions are only considered when the running host is itself a prerelease build.
    /// </param>
    /// <param name="source">Custom NuGet source (https:// URL, local folder, or configured feed name; null = all configured sources).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed package with its exact version, or null if installation failed.</returns>
    public async Task<InstalledPackage?> InstallAsync(
        string packageId,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetDir = PluginDirectory;
            _ = Directory.CreateDirectory(targetDir);

            // Detect installation type
            if (File.Exists(packageId) && packageId.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                // Local .nupkg file
                logger.InstallingFromFile(packageId);
                return await InstallFromNupkgAsync(packageId, targetDir, Path.GetFullPath(packageId), cancellationToken);
            }
            else if (Uri.TryCreate(packageId, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    if (!PackageTrustPolicy.IsAllowedSource(uri))
                    {
                        logger.InsecureSourceRejected(packageId);
                        return null;
                    }

                    // URL to .nupkg
                    logger.InstallingFromUrl(packageId);
                    return await InstallFromUrlAsync(uri, targetDir, cancellationToken);
                }
                else if (uri.Scheme == Uri.UriSchemeFile)
                {
                    // file:///path/to/plugin.nupkg → treat as local path
                    var filePath = uri.LocalPath;

                    if (!File.Exists(filePath))
                    {
                        logger.LocalPackageNotFound(filePath);
                        return null;
                    }

                    if (!filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LocalPackageNotNupkg(filePath);
                        return null;
                    }

                    logger.InstallingFromFile(filePath);
                    return await InstallFromNupkgAsync(filePath, targetDir, filePath, cancellationToken);
                }
            }

            // Fall through: treat as NuGet package ID
            {
                if (!PackageIdRules.IsValid(packageId))
                {
                    logger.InvalidPackageId(packageId);
                    return null;
                }

                // Package ID from NuGet feed
                logger.InstallingPlugin(packageId);

                if (source is not null)
                {
                    // Explicit source - try named source or treat as URL
                    var sourceUrl = await ResolveSourceAsync(source, cancellationToken);
                    if (!PackageTrustPolicy.IsAllowedSource(sourceUrl))
                    {
                        logger.InsecureSourceRejected(sourceUrl);
                        return null;
                    }

                    var sourceRepo = Repository.Factory.GetCoreV3(new NuGetPackageSource(sourceUrl));
                    var extracted = await ExtractFromNuGetAsync(packageId, version, sourceRepo, targetDir, cancellationToken);
                    return extracted is null ? null : await RegisterExtractedPluginAsync(extracted, cancellationToken);
                }
                else
                {
                    // No explicit source - try all configured sources
                    return await InstallFromMultipleSourcesAsync(packageId, version, targetDir, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.InstallFailed(ex, packageId);
            return null;
        }
    }

    /// <summary>
    /// Updates a plugin to the latest version by reinstalling it.
    /// </summary>
    /// <param name="packageId">The NuGet package ID of the plugin to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The installed package with its exact version, or null if the update failed.</returns>
    public async Task<InstalledPackage?> UpdatePluginAsync(string packageId, CancellationToken cancellationToken = default)
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
    /// <exception cref="ArgumentException"><paramref name="packageId"/> is not a valid NuGet package ID.</exception>
    /// <exception cref="InvalidOperationException">The plugin folder escapes the plugin directory or is a symbolic link or junction.</exception>
    public async Task<bool> UninstallPluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        // The ID becomes a path segment below the plugin directory, so reject anything that
        // could traverse, be rooted or contain separators before touching the filesystem.
        if (!PackageIdValidator.IsValidPackageId(packageId))
        {
            throw new ArgumentException($"'{packageId}' is not a valid package ID.", nameof(packageId));
        }

        var pluginDir = PluginDirectory;
        var pluginPath = Path.Combine(pluginDir, packageId);
        if (!DirectoryDeletionGuard.TryValidateContainedDirectory(pluginPath, pluginDir, out var unsafeReason))
        {
            throw new InvalidOperationException(unsafeReason);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.UninstallingPlugin(packageId);

            var found = false;

            // Delete plugin subdirectory with all contents (main DLL + dependencies)
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
                await projectService.RemovePackageAsync(packageId, cancellationToken);
                logger.PluginUninstalled(packageId);
                return true;
            }

            logger.PluginNotFound(packageId);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
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

    /// <inheritdoc />
    Task<bool> IPackageInstaller.UninstallAsync(string packageId, CancellationToken cancellationToken) =>
        UninstallPluginAsync(packageId, cancellationToken);

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

    internal async Task<InstalledPackage?> ExtractFromNuGetAsync(
        string packageId,
        string? version,
        SourceRepository sourceRepo,
        string targetDir,
        CancellationToken cancellationToken)
    {
        if (!TryResolveRequestedVersion(version, out var requestedVersion))
        {
            logger.InvalidVersion(packageId, version!);
            return null;
        }

        var resource = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        if (resource is null)
        {
            logger.PackageNotFound(packageId);
            return null;
        }

        using var cacheContext = new SourceCacheContext();

        var versions = await resource.GetAllVersionsAsync(
            packageId,
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken);

        // Prereleases are only picked implicitly when the host itself is a prerelease.
        var includePrerelease = IsPrereleaseHost();
        var targetVersion = requestedVersion
            ?? versions.Where(v => includePrerelease || !v.IsPrerelease).MaxBy(v => v);

        if (targetVersion is null)
        {
            logger.PackageNotFound(packageId);
            return null;
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
                    return null;
                }
            }

            return await extractor.ExtractAsync(tempFile, targetDir, sourceRepo.PackageSource.Source, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<InstalledPackage?> InstallFromUrlAsync(Uri url, string targetDir, CancellationToken cancellationToken)
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

    internal async Task<InstalledPackage?> InstallFromNupkgAsync(string nupkgPath, string targetDir, string installedFrom, CancellationToken cancellationToken)
    {
        var extracted = await extractor.ExtractAsync(nupkgPath, targetDir, installedFrom, cancellationToken);
        return extracted is null ? null : await RegisterExtractedPluginAsync(extracted, cancellationToken);
    }

    private async Task<InstalledPackage?> RegisterExtractedPluginAsync(InstalledPackage package, CancellationToken cancellationToken)
    {
        try
        {
            await projectService.AddPackageAsync(package.Id, package.Version, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.ProjectRegistrationFailed(ex, package.Id);
            return null;
        }

        logger.PluginInstalled(package.Id);
        return package;
    }

    private async Task<InstalledPackage?> InstallFromMultipleSourcesAsync(
        string packageId,
        string? version,
        string targetDir,
        CancellationToken cancellationToken)
    {
        var sources = await nugetSourceManager.LoadSourcesAsync(cancellationToken);
        logger.TryingMultipleSources(packageId, sources.Count);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PackageTrustPolicy.IsAllowedSource(source.Url))
            {
                logger.InsecureSourceSkipped(source.Name, source.Url);
                continue;
            }

            var extracted = (InstalledPackage?)null;
            try
            {
                logger.TryingSource(source.Name, source.Url);
                var sourceRepo = Repository.Factory.GetCoreV3(new NuGetPackageSource(source.Url));
                extracted = await ExtractFromNuGetAsync(packageId, version, sourceRepo, targetDir, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                logger.SourceFailed(source.Name);
            }

            if (extracted is not null)
            {
                var registered = await RegisterExtractedPluginAsync(extracted, cancellationToken);
                if (registered is not null)
                {
                    logger.SuccessFromSource(packageId, source.Name);
                }

                return registered;
            }
        }

        logger.AllSourcesFailed(packageId);
        return null;
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

    private bool IsPrereleaseHost() =>
        NuGetVersion.TryParse(buildInfo.Version, out var hostVersion) && hostVersion.IsPrerelease;

    /// <summary>
    /// Parses a requested version. Missing, empty and <c>"latest"</c> mean "no explicit version"
    /// and yield <see langword="null"/> (resolved per the stable/prerelease policy).
    /// </summary>
    private static bool TryResolveRequestedVersion(string? version, out NuGetVersion? requested)
    {
        requested = null;
        if (string.IsNullOrWhiteSpace(version) || version.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!NuGetVersion.TryParse(version, out var parsed))
        {
            return false;
        }

        requested = parsed;
        return true;
    }
}
