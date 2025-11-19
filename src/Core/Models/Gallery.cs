namespace Spectara.Revela.Core.Models;

/// <summary>
/// Represents a gallery containing images
/// </summary>
public sealed class Gallery
{
    public required string Path { get; init; }
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

