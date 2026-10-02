using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Packages.Logging;
using Spectara.Revela.Sdk.Hosting;
using NuGetPackageSource = NuGet.Configuration.PackageSource;

namespace Spectara.Revela.Features.Packages.Services;

/// <summary>
/// Installs and removes package files (plugins and themes) in the plugin directory via NuGet.
/// </summary>
/// <remarks>
/// Only manages files: the nuspec package type is checked before extraction (<see cref="NupkgExtractor"/>);
/// declaring packages in <c>dependencies.packages</c> is done by <see cref="PackageInstallService"/>.
/// HttpClient is injected via Typed HttpClient pattern for URL-based downloads.
/// </remarks>
public sealed class PackageManager(
    HttpClient httpClient,
    NupkgExtractor extractor,
    ILogger<PackageManager> logger,
    INuGetSourceManager nugetSourceManager,
    IBuildInfo buildInfo) : IPackageInstaller
{
    /// <summary>
    /// Gets the plugin directory based on installation type.
    /// </summary>
    /// <remarks>
    /// Full edition (portable): {exe-dir}/plugins
    /// dotnet tool: %APPDATA%/Revela/plugins
    /// </remarks>
    public static string PluginDirectory => ConfigPathResolver.LocalPluginDirectory;

    /// <summary>
    /// Installs a plugin from a package ID, local .nupkg file, or URL.
    /// </summary>
    /// <param name="packageId">Package ID (e.g., 'Spectara.Revela.Plugins.Statistics'), local .nupkg path, or HTTPS URL.</param>
    /// <param name="requiredPackageType">
    /// Package type the nuspec must declare, checked before any file is written. <c>null</c> accepts any
    /// type (restore installs packages a project already declares).
    /// </param>
    /// <param name="version">
    /// Specific version to install (only for package IDs). When null, empty or <c>"latest"</c>, the highest stable version is chosen;
    /// prerelease versions are only considered when the running host is itself a prerelease build.
    /// </param>
    /// <param name="source">Custom NuGet source (https:// URL, local folder, or configured feed name; null = all configured sources).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="PackageInstallStatus.Installed"/> with the exact version, <see cref="PackageInstallStatus.WrongPackageType"/>
    /// (nothing written) or <see cref="PackageInstallStatus.Failed"/>.
    /// </returns>
    public async Task<PackageInstallResult> InstallAsync(
        string packageId,
        string? requiredPackageType,
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
                return await InstallFromNupkgAsync(packageId, requiredPackageType, targetDir, cancellationToken);
            }
            else if (Uri.TryCreate(packageId, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    if (!PackageTrustPolicy.IsAllowedSource(uri))
                    {
                        logger.InsecureSourceRejected(packageId);
                        return Failed;
                    }

                    // URL to .nupkg
                    logger.InstallingFromUrl(packageId);
                    return await InstallFromUrlAsync(uri, requiredPackageType, targetDir, cancellationToken);
                }
                else if (uri.Scheme == Uri.UriSchemeFile)
                {
                    // file:///path/to/plugin.nupkg → treat as local path
                    var filePath = uri.LocalPath;

                    if (!File.Exists(filePath))
                    {
                        logger.LocalPackageNotFound(filePath);
                        return Failed;
                    }

                    if (!filePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LocalPackageNotNupkg(filePath);
                        return Failed;
                    }

                    logger.InstallingFromFile(filePath);
                    return await InstallFromNupkgAsync(filePath, requiredPackageType, targetDir, cancellationToken);
                }
            }

            // Fall through: treat as NuGet package ID
            {
                if (!PackageIdRules.IsValid(packageId))
                {
                    logger.InvalidPackageId(packageId);
                    return Failed;
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
                        return Failed;
                    }

                    var sourceRepo = Repository.Factory.GetCoreV3(new NuGetPackageSource(sourceUrl));
                    return Logged(await ExtractFromNuGetAsync(packageId, version, requiredPackageType, sourceRepo, targetDir, cancellationToken));
                }
                else
                {
                    // No explicit source - try all configured sources
                    return await InstallFromMultipleSourcesAsync(packageId, version, requiredPackageType, targetDir, cancellationToken);
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
            return Failed;
        }
    }

    /// <summary>
    /// Uninstalls a package by removing its folder from the plugin directory.
    /// </summary>
    /// <remarks>
    /// Plugin configuration files are preserved for potential reinstallation.
    /// </remarks>
    /// <param name="packageId">The NuGet package ID of the plugin to uninstall.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was found and removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="packageId"/> is not a valid NuGet package ID.</exception>
    /// <exception cref="InvalidOperationException">The plugin folder escapes the plugin directory or is a symbolic link or junction.</exception>
    public bool Uninstall(string packageId, CancellationToken cancellationToken = default)
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

            // Delete plugin subdirectory with all contents (main DLL + dependencies)
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
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

    /// <inheritdoc />
    Task<bool> IPackageInstaller.UninstallAsync(string packageId, CancellationToken cancellationToken) =>
        Task.FromResult(Uninstall(packageId, cancellationToken));

    private static PackageInstallResult Failed { get; } = new(PackageInstallStatus.Failed);

    internal async Task<PackageInstallResult> ExtractFromNuGetAsync(
        string packageId,
        string? version,
        string? requiredPackageType,
        SourceRepository sourceRepo,
        string targetDir,
        CancellationToken cancellationToken)
    {
        if (!TryResolveRequestedVersion(version, out var requestedVersion))
        {
            logger.InvalidVersion(packageId, version!);
            return Failed;
        }

        var resource = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        if (resource is null)
        {
            logger.PackageNotFound(packageId);
            return Failed;
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
            return Failed;
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
                    return Failed;
                }
            }

            return await extractor.ExtractAsync(tempFile, targetDir, requiredPackageType, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<PackageInstallResult> InstallFromUrlAsync(
        Uri url,
        string? requiredPackageType,
        string targetDir,
        CancellationToken cancellationToken)
    {
        await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            return await InstallFromNupkgAsync(tempFile, requiredPackageType, targetDir, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal async Task<PackageInstallResult> InstallFromNupkgAsync(
        string nupkgPath,
        string? requiredPackageType,
        string targetDir,
        CancellationToken cancellationToken) =>
        Logged(await extractor.ExtractAsync(nupkgPath, targetDir, requiredPackageType, cancellationToken));

    private PackageInstallResult Logged(PackageInstallResult result)
    {
        if (result.Status == PackageInstallStatus.Installed)
        {
            logger.PluginInstalled(result.Package!.Id);
        }

        return result;
    }

    private async Task<PackageInstallResult> InstallFromMultipleSourcesAsync(
        string packageId,
        string? version,
        string? requiredPackageType,
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

            var result = Failed;
            try
            {
                logger.TryingSource(source.Name, source.Url);
                var sourceRepo = Repository.Factory.GetCoreV3(new NuGetPackageSource(source.Url));
                result = await ExtractFromNuGetAsync(packageId, version, requiredPackageType, sourceRepo, targetDir, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                logger.SourceFailed(source.Name);
            }

            if (result.Status == PackageInstallStatus.WrongPackageType)
            {
                return result;
            }

            if (result.Status == PackageInstallStatus.Installed)
            {
                logger.SuccessFromSource(packageId, source.Name);
                return Logged(result);
            }
        }

        logger.AllSourcesFailed(packageId);
        return Failed;
    }

    private async Task<string> ResolveSourceAsync(string source, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return source;
        }

        var sources = await nugetSourceManager.LoadSourcesAsync(cancellationToken);
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
