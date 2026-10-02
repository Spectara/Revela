namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// How long an artifact lives, declared by its owner. Decides which generic clean
/// command removes it.
/// </summary>
/// <remarks>
/// Generic clean commands invalidate every registered artifact of a kind, together with
/// the artifacts that depend on it: <c>revela clean cache</c> removes <see cref="Cache"/>,
/// <c>revela clean output</c> removes <see cref="Output"/>, <c>revela clean all</c> removes
/// both. <see cref="Durable"/> artifacts are only removed by their owner's own clean command.
/// </remarks>
public enum ArtifactKind
{
    /// <summary>
    /// Cheap to rebuild from the source (and durable artifacts), for example the scan manifest
    /// or a plugin's per-page data file. May be deleted at any time; losing it only costs time.
    /// </summary>
    Cache,

    /// <summary>
    /// Belongs to the output or describes it, for example the rendered site, processed images,
    /// pre-compressed sidecars or a record of which output files a plugin created. Removed
    /// together with the output.
    /// </summary>
    Output,

    /// <summary>
    /// Expensive or impossible to reproduce, for example captions written by an AI service.
    /// Never removed by <c>clean cache</c>, <c>clean output</c> or <c>clean all</c>; only by the
    /// owner's own clean command. A durable artifact may only depend on other durable artifacts.
    /// </summary>
    Durable,
}
