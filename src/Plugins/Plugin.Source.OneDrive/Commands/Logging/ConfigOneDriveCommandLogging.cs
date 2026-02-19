namespace Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;

/// <summary>
/// High-performance logging for ConfigOneDriveCommand using source-generated extension methods
/// </summary>
internal static partial class ConfigOneDriveCommandLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "OneDrive config saved to {Path}")]
    public static partial void ConfigSaved(this ILogger<ConfigOneDriveCommand> logger, string path);
}
