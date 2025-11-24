namespace Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;

/// <summary>
/// High-performance logging for OneDriveSourceCommand using source-generated extension methods
/// </summary>
internal static partial class OneDriveSourceCommandLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "OneDrive download failed")]
    public static partial void DownloadFailed(this ILogger logger, Exception exception);
}
