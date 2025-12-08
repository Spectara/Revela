namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Result of content scanning.
/// </summary>
public sealed class ContentResult
{
    /// <summary>Whether scanning succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Number of galleries found.</summary>
    public int GalleryCount { get; init; }

    /// <summary>Number of images found.</summary>
    public int ImageCount { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
