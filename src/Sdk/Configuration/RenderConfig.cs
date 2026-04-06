namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Rendering configuration
/// </summary>
public sealed class RenderConfig
{
    /// <summary>
    /// Enable parallel rendering of galleries/pages.
    /// </summary>
    /// <remarks>
    /// Default is false; set to true to speed up rendering on multi-core machines.
    /// </remarks>
    public bool Parallel { get; init; }

    /// <summary>
    /// Optional maximum degree of parallelism.
    /// </summary>
    /// <remarks>
    /// When null, uses the default from ParallelOptions (Environment.ProcessorCount).
    /// </remarks>
    public int? MaxDegreeOfParallelism { get; init; }
}
