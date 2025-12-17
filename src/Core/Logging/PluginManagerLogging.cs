namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PluginManager using source-generated extension methods
/// </summary>
internal static partial class PluginManagerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {packageId}")]
    public static partial void InstallingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {packageId} not found")]
    public static partial void PackageNotFound(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {packageId} v{version}")]
    public static partial void InstallingVersion(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {packageId}")]
    public static partial void InstallFailed(this ILogger<PluginManager> logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating plugin: {packageId}")]
    public static partial void UpdatingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {packageId}")]
    public static partial void UninstallingPlugin(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {packageId}")]
    public static partial void PluginUninstalled(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {packageId} not found")]
    public static partial void PluginNotFound(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {packageId}")]
    public static partial void UninstallFailed(this ILogger<PluginManager> logger, Exception exception, string packageId);

    // NuGet package installation logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from file: {filePath}")]
    public static partial void InstallingFromFile(this ILogger<PluginManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from URL: {url}")]
    public static partial void InstallingFromUrl(this ILogger<PluginManager> logger, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download package {packageId} v{version}")]
    public static partial void DownloadFailed(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting package: {packageId} v{version}")]
    public static partial void ExtractingPackage(this ILogger<PluginManager> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No compatible libraries found in package {packageId}")]
    public static partial void NoCompatibleLibs(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted {fileName} to {targetDir}")]
    public static partial void ExtractedFile(this ILogger<PluginManager> logger, string fileName, string targetDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No files extracted from package {packageId}")]
    public static partial void NoFilesExtracted(this ILogger<PluginManager> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {packageId} installed successfully ({fileCount} file(s))")]
    public static partial void PluginInstalled(this ILogger<PluginManager> logger, string packageId, int fileCount);
}
