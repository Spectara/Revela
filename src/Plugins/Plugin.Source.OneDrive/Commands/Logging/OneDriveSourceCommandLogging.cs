namespace Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;

/// <summary>
/// High-performance logging for OneDriveSourceCommand using source-generated extension methods
/// </summary>
internal static partial class OneDriveSourceCommandLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Download failed")]
    public static partial void DownloadFailed(this ILogger<OneDriveSourceCommand> logger, Exception exception);
}
