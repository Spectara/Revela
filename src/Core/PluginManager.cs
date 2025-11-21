using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin installation, updates and removal via NuGet
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor with optional parameter.
/// Creates plugin directory automatically if it doesn't exist.
/// </remarks>
public sealed partial class PluginManager(ILogger<PluginManager>? logger = null)
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
            LogInstallingPlugin(logger, packageId);

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
                LogPackageNotFound(logger, packageId);
                return false;
            }

            LogInstallingVersion(logger, packageId, targetVersion.ToString());

            // TODO: Implement actual package download and extraction
            // For now, this is a placeholder

            return true;
        }
        catch (Exception ex)
        {
            LogInstallFailed(logger, ex, packageId);
            return false;
        }
    }

    public async Task<bool> UpdatePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        LogUpdatingPlugin(logger, packageId);
        // Uninstall old version, install new version
        _ = await UninstallPluginAsync(packageId, cancellationToken);
        return await InstallPluginAsync(packageId, null, cancellationToken);
    }

    public Task<bool> UninstallPluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            LogUninstallingPlugin(logger, packageId);

            var pluginPath = Path.Combine(pluginDirectory, packageId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
                LogPluginUninstalled(logger, packageId);
                return Task.FromResult(true);
            }
            else
            {
                LogPluginNotFound(logger, packageId);
                return Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            LogUninstallFailed(logger, ex, packageId);
            return Task.FromResult(false);
        }
    }

    // High-performance logging using source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {PackageId}")]
    private static partial void LogInstallingPlugin(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {PackageId} not found")]
    private static partial void LogPackageNotFound(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {PackageId} v{Version}")]
    private static partial void LogInstallingVersion(ILogger logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {PackageId}")]
    private static partial void LogInstallFailed(ILogger logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating plugin: {PackageId}")]
    private static partial void LogUpdatingPlugin(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {PackageId}")]
    private static partial void LogUninstallingPlugin(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {PackageId}")]
    private static partial void LogPluginUninstalled(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PackageId} not found")]
    private static partial void LogPluginNotFound(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {PackageId}")]
    private static partial void LogUninstallFailed(ILogger logger, Exception exception, string packageId);

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

