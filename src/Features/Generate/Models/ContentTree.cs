namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Content tree representing scanned content
/// </summary>
/// <remarks>
/// Result of scanning the source directory.
/// Contains discovered images, markdown files, and gallery structure.
/// </remarks>
internal sealed class ContentTree
{
    /// <summary>
    /// All source images found during scan
    /// </summary>
    public required IReadOnlyList<SourceImage> Images { get; init; }

    /// <summary>
    /// All markdown files found during scan (excluding _index.md).
    /// </summary>
    public required IReadOnlyList<SourceMarkdown> Markdowns { get; init; }

    /// <summary>
    /// Gallery structure discovered from directories
    /// </summary>
    public required IReadOnlyList<Gallery> Galleries { get; init; }

    /// <summary>
    /// Empty or colliding normalized output slugs detected during the scan.
    /// </summary>
    /// <remarks>
    /// Empty when every gallery and image resolves to a unique, non-empty output path.
    /// A non-empty list means distinct source paths would overwrite each other's output,
    /// so the scan step must fail before any rendering. See <see cref="SlugConflict"/>.
    /// </remarks>
    public IReadOnlyList<SlugConflict> SlugConflicts { get; init; } = [];
}

