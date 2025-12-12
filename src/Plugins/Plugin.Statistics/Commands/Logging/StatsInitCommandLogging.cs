namespace Spectara.Revela.Plugin.Statistics.Commands.Logging;

/// <summary>
/// High-performance logging for StatsInitCommand using source-generated extension methods
/// </summary>
internal static partial class StatsInitCommandLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Created config file: {Path}")]
    public static partial void ConfigCreated(this ILogger<StatsInitCommand> logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created markdown file: {Path}")]
    public static partial void MarkdownCreated(this ILogger<StatsInitCommand> logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Initialization failed")]
    public static partial void InitFailed(this ILogger<StatsInitCommand> logger, Exception exception);
}
