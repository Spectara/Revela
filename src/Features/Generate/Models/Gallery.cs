namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Represents a gallery containing images
/// </summary>
public sealed class Gallery
{
    /// <summary>
    /// Original filesystem path relative to source directory
    /// </summary>
    /// <remarks>
    /// Used for finding images and matching source files.
    /// Example: "01 Events/Fireworks"
    /// </remarks>
    public required string Path { get; init; }

    /// <summary>
    /// URL-safe path for output directory and links
    /// </summary>
    /// <remarks>
    /// Normalized version of Path with slugified segments.
    /// Example: "events/fireworks/"
    /// </remarks>
    public required string Slug { get; init; }

    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Cover { get; init; }
    public DateTime? Date { get; init; }
    public bool Featured { get; init; }
    public int Weight { get; init; }
    public IReadOnlyList<Image> Images { get; init; } = [];
    public IReadOnlyList<Gallery> SubGalleries { get; init; } = [];
}
