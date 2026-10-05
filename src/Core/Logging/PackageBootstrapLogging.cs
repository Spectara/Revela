namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for plugin bootstrap phase (before DI is available).
/// </summary>
internal static partial class PackageBootstrapLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' was skipped because it failed to configure its services: {Reason}")]
    public static partial void ConfigureServicesFailed(this ILogger logger, string pluginName, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ConfigureServices of plugin '{PluginName}' threw")]
    public static partial void ConfigureServicesFailedDetails(this ILogger logger, Exception exception, string pluginName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' was skipped because it requires [{MissingPlugins}], which are not installed")]
    public static partial void PluginDependencyMissing(this ILogger logger, string pluginName, string missingPlugins);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin '{PluginName}' extends [{MissingTargets}] which are not installed — extension features will be skipped")]
    public static partial void PluginExtensionTargetMissing(this ILogger logger, string pluginName, string missingTargets);
}
