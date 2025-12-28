namespace Spectara.Revela.Plugin.Statistics.Pipeline;

/// <summary>
/// High-performance logging for StatisticsPipelineStep using source-generated extension methods.
/// </summary>
internal static partial class StatisticsPipelineStepLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading manifest...")]
    public static partial void LoadingManifest(this ILogger<StatisticsPipelineStep> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating statistics for {Count} pages")]
    public static partial void GeneratingStats(this ILogger<StatisticsPipelineStep> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated statistics JSON for {Path} ({Count} images)")]
    public static partial void GeneratedJsonFile(this ILogger<StatisticsPipelineStep> logger, string path, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Statistics generation failed")]
    public static partial void GenerationFailed(this ILogger<StatisticsPipelineStep> logger, Exception exception);
}
