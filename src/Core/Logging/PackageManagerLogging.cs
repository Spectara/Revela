namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PackageManager using source-generated extension methods.
/// </summary>
internal static partial class PackageManagerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {PackageId}")]
    public static partial void InstallingPlugin(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {PackageId} not found")]
    public static partial void PackageNotFound(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {PackageId} v{Version}")]
    public static partial void InstallingVersion(this ILogger<PackageManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {PackageId}")]
    public static partial void InstallFailed(this ILogger<PackageManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating plugin: {PackageId}")]
    public static partial void UpdatingPlugin(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {PackageId}")]
    public static partial void UninstallingPlugin(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {PackageId}")]
    public static partial void PluginUninstalled(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PackageId} not found")]
    public static partial void PluginNotFound(this ILogger<PackageManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {PackageId}")]
    public static partial void UninstallFailed(this ILogger<PackageManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from file: {FilePath}")]
    public static partial void InstallingFromFile(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Local plugin package file was not found: {FilePath}")]
    public static partial void LocalPackageNotFound(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Local plugin package must be a .nupkg file: {FilePath}")]
    public static partial void LocalPackageNotNupkg(this ILogger<PackageManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from URL: {Url}")]
    public static partial void InstallingFromUrl(this ILogger<PackageManager> logger, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download package {PackageId} v{Version}")]
    public static partial void DownloadFailed(this ILogger<PackageManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PackageId} installed successfully")]
    public static partial void PluginInstalled(this ILogger<PackageManager> logger, string packageId);

    // Multi-source discovery logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Using named source '{SourceName}' -> {Url}")]
    public static partial void UsingNamedSource(this ILogger<PackageManager> logger, string sourceName, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{Source}' not found in config, treating as path/URL")]
    public static partial void SourceNotFoundTreatingAsUrl(this ILogger<PackageManager> logger, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trying {SourceCount} source(s) for package {PackageId}")]
    public static partial void TryingMultipleSources(this ILogger<PackageManager> logger, string packageId, int sourceCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trying source '{SourceName}' ({Url})")]
    public static partial void TryingSource(this ILogger<PackageManager> logger, string sourceName, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully installed {PackageId} from source '{SourceName}'")]
    public static partial void SuccessFromSource(this ILogger<PackageManager> logger, string packageId, string sourceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Source '{SourceName}' failed, trying next")]
    public static partial void SourceFailed(this ILogger<PackageManager> logger, string sourceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "All sources failed for package {PackageId}")]
    public static partial void AllSourcesFailed(this ILogger<PackageManager> logger, string packageId);
}


