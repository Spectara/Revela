namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for PackageContext using source-generated extension methods.
/// </summary>
internal static partial class PackageContextLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Commands of plugin '{PluginName}' are unavailable because they failed to register: {Reason}")]
    public static partial void CommandRegistrationFailed(this ILogger<PackageContext> logger, string pluginName, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Command registration of plugin '{PluginName}' threw")]
    public static partial void CommandRegistrationFailedDetails(this ILogger<PackageContext> logger, string pluginName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' tried to register duplicate command '{CommandName}' under '{ParentPath}'")]
    public static partial void DuplicateSubcommand(this ILogger<PackageContext> logger, string pluginName, string commandName, string parentPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginName}' tried to register duplicate root command '{CommandName}'")]
    public static partial void DuplicateRootCommand(this ILogger<PackageContext> logger, string pluginName, string commandName);
}

