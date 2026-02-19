namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Progress during content scanning.
/// </summary>
internal sealed class ContentProgress
{
    /// <summary>Current status message.</summary>
    public required string Status { get; init; }

    /// <summary>Number of galleries found so far.</summary>
    public int GalleriesFound { get; init; }

    /// <summary>Number of images found so far.</summary>
    public int ImagesFound { get; init; }
}
