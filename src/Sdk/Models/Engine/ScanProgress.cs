namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Progress during a content scan operation.
/// </summary>
public sealed record ScanProgress
{
    /// <summary>Current status message.</summary>
    public required string Status { get; init; }

    /// <summary>Number of galleries found so far.</summary>
    public int GalleriesFound { get; init; }

    /// <summary>Number of images found so far.</summary>
    public int ImagesFound { get; init; }
}
