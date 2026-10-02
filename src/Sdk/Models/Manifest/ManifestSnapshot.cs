namespace Spectara.Revela.Sdk.Models.Manifest;

/// <summary>
/// Immutable view of the scanned site returned by
/// <see cref="Abstractions.IManifestReader.TryLoadAsync"/>.
/// </summary>
public sealed record ManifestSnapshot
{
    /// <summary>
    /// Root node of the site tree (home page); children are galleries and pages.
    /// </summary>
    public required ManifestEntry Root { get; init; }

    /// <summary>
    /// Every scanned image, keyed by its source path relative to the source
    /// directory (forward slashes). An image shown on several pages (for example
    /// through a filter) appears once.
    /// </summary>
    public required IReadOnlyDictionary<string, ImageContent> Images { get; init; }
}
