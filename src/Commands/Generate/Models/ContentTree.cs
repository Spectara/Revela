namespace Spectara.Revela.Commands.Generate.Models;

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
}
