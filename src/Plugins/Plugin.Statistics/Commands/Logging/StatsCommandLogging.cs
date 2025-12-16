namespace Spectara.Revela.Plugin.Statistics.Commands.Logging;

/// <summary>
/// High-performance logging for StatsCommand using source-generated extension methods
/// </summary>
internal static partial class StatsCommandLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading manifest...")]
    public static partial void LoadingManifest(this ILogger<StatsCommand> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating statistics for {Count} pages")]
    public static partial void GeneratingStats(this ILogger<StatsCommand> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated statistics JSON for {Path} ({Count} images)")]
    public static partial void GeneratedJsonFile(this ILogger<StatsCommand> logger, string path, int count);
}
