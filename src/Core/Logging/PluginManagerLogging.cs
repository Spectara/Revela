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

    // ZIP installation logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin from ZIP: {zipPath} to {targetDir}")]
    public static partial void InstallingFromZip(this ILogger<PluginManager> logger, string zipPath, string targetDir);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin from ZIP: {zipPath}")]
    public static partial void InstallFromZipFailed(this ILogger<PluginManager> logger, Exception exception, string zipPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "ZIP file not found: {zipPath}")]
    public static partial void ZipFileNotFound(this ILogger<PluginManager> logger, string zipPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted {fileName} to {targetDir}")]
    public static partial void ExtractedFile(this ILogger<PluginManager> logger, string fileName, string targetDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No DLL files found in ZIP: {zipPath}")]
    public static partial void NoDllsInZip(this ILogger<PluginManager> logger, string zipPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installed {dllCount} plugin DLL(s) to {targetDir}")]
    public static partial void PluginInstalledFromZip(this ILogger<PluginManager> logger, int dllCount, string targetDir);
}
