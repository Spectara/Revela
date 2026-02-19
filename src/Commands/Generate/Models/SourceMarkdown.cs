namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Represents a markdown file discovered during content scanning.
/// </summary>
/// <remarks>
/// Markdown files (*.md) other than _index.md can be placed between images
/// to add text content to galleries. The body is loaded at render time.
/// </remarks>
internal sealed record SourceMarkdown
{
    /// <summary>
    /// Full path to the markdown file.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Path relative to source directory (e.g., "01 Events\Fireworks\intro.md").
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Filename including extension (e.g., "intro.md").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long FileSize { get; init; }

    /// <summary>
    /// Last modification time (UTC).
    /// </summary>
    public required DateTime LastModified { get; init; }

    /// <summary>
    /// Gallery path this markdown belongs to (directory relative to source).
    /// </summary>
    public required string Gallery { get; init; }
}
