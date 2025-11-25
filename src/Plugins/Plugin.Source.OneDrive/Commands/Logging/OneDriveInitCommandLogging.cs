namespace Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;

/// <summary>
/// High-performance logging for OneDriveInitCommand using source-generated extension methods
/// </summary>
internal static partial class OneDriveInitCommandLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Initialization failed")]
    public static partial void InitFailed(this ILogger<OneDriveInitCommand> logger, Exception exception);
}
