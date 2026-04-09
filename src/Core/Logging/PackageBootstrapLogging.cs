namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for plugin bootstrap phase (before DI is available).
/// </summary>
internal static partial class PackageBootstrapLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin '{PluginName}' failed to configure configuration")]
    public static partial void ConfigureConfigurationFailed(this ILogger logger, Exception exception, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin '{PluginName}' failed to configure services")]
    public static partial void ConfigureServicesFailed(this ILogger logger, Exception exception, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin '{PluginName}' requires [{MissingPlugins}] which are not installed — plugin will not be loaded")]
    public static partial void PluginDependencyMissing(this ILogger logger, string pluginName, string missingPlugins);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin '{PluginName}' extends [{MissingTargets}] which are not installed — extension features will be skipped")]
    public static partial void PluginExtensionTargetMissing(this ILogger logger, string pluginName, string missingTargets);
}

