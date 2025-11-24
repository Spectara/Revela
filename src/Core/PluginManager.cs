using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Logging;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin installation, updates and removal via NuGet
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor with optional parameter.
/// Creates plugin directory automatically if it doesn't exist.
/// </remarks>
public sealed class PluginManager(ILogger<PluginManager>? logger = null)
{
    private readonly string pluginDirectory = InitializePluginDirectory();
    private readonly ILogger<PluginManager> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginManager>.Instance;
    private readonly SourceRepository repository = Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json"));

    private static string InitializePluginDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "Revela", "plugins");
        _ = Directory.CreateDirectory(path);
        return path;
    }

    public async Task<bool> InstallPluginAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.InstallingPlugin(packageId);

            var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
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

            logger.InstallingVersion(packageId, targetVersion.ToString());

            // TODO: Implement actual package download and extraction
            // For now, this is a placeholder

            return true;
        }
        catch (Exception ex)
        {
            logger.InstallFailed(ex, packageId);
            return false;
        }
    }

    public async Task<bool> UpdatePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        logger.UpdatingPlugin(packageId);
        // Uninstall old version, install new version
        _ = await UninstallPluginAsync(packageId, cancellationToken);
        return await InstallPluginAsync(packageId, null, cancellationToken);
    }

    public Task<bool> UninstallPluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            logger.UninstallingPlugin(packageId);

            var pluginPath = Path.Combine(pluginDirectory, packageId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
                logger.PluginUninstalled(packageId);
                return Task.FromResult(true);
            }
            else
            {
                logger.PluginNotFound(packageId);
                return Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            logger.UninstallFailed(ex, packageId);
            return Task.FromResult(false);
        }
    }

    public IEnumerable<string> ListInstalledPlugins()
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return [];
        }

        return Directory.GetDirectories(pluginDirectory)
            .Select(Path.GetFileName)
            .OfType<string>(); // Filters out nulls and casts to non-nullable
    }
}

