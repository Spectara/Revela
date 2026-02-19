namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PluginContext using source-generated extension methods.
/// </summary>
internal static partial class PluginContextLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to register commands for plugin '{PluginName}'")]
    public static partial void CommandRegistrationFailed(this ILogger<PluginContext> logger, string pluginName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' tried to register duplicate command '{CommandName}' under '{ParentPath}'")]
    public static partial void DuplicateSubcommand(this ILogger<PluginContext> logger, string pluginName, string commandName, string parentPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' tried to register duplicate root command '{CommandName}'")]
    public static partial void DuplicateRootCommand(this ILogger<PluginContext> logger, string pluginName, string commandName);
}
