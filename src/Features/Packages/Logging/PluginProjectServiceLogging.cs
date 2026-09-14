namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PluginProjectService using source-generated extension methods.
/// </summary>
internal static partial class PluginProjectServiceLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Updated {ProjectJsonPath} with plugin {PackageId} v{Version}")]
    public static partial void PluginAdded(this ILogger<PluginProjectService> logger, string projectJsonPath, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed {PackageId} from {ProjectJsonPath}")]
    public static partial void PluginRemoved(this ILogger<PluginProjectService> logger, string projectJsonPath, string packageId);
}
