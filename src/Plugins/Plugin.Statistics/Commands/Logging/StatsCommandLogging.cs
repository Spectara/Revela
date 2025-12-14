namespace Spectara.Revela.Plugin.Statistics.Commands.Logging;

/// <summary>
/// High-performance logging for StatsCommand using source-generated extension methods
/// </summary>
internal static partial class StatsCommandLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading manifest...")]
    public static partial void LoadingManifest(this ILogger<StatsCommand> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Aggregating statistics from {Count} images")]
    public static partial void Aggregating(this ILogger<StatsCommand> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Writing statistics.json")]
    public static partial void WritingJson(this ILogger<StatsCommand> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Writing _index.revela with frontmatter")]
    public static partial void WritingMarkdown(this ILogger<StatsCommand> logger);
}
