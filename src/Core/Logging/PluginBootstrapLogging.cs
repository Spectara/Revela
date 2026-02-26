namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for plugin bootstrap phase (before DI is available).
/// </summary>
internal static partial class PluginBootstrapLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin '{PluginName}' failed to configure configuration")]
    public static partial void ConfigureConfigurationFailed(this ILogger logger, Exception exception, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin '{PluginName}' failed to configure services")]
    public static partial void ConfigureServicesFailed(this ILogger logger, Exception exception, string pluginName);
}
