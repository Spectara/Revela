namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PluginManager using source-generated extension methods
/// </summary>
internal static partial class PluginManagerLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin: {packageId}")]
    public static partial void InstallingPlugin(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Package {packageId} not found")]
    public static partial void PackageNotFound(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing {packageId} v{version}")]
    public static partial void InstallingVersion(this ILogger logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin {packageId}")]
    public static partial void InstallFailed(this ILogger logger, Exception exception, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating plugin: {packageId}")]
    public static partial void UpdatingPlugin(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling plugin: {packageId}")]
    public static partial void UninstallingPlugin(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalled {packageId}")]
    public static partial void PluginUninstalled(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {packageId} not found")]
    public static partial void PluginNotFound(this ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to uninstall plugin {packageId}")]
    public static partial void UninstallFailed(this ILogger logger, Exception exception, string packageId);
}
