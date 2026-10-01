using Markdig.Parsers;
using Markdig.Syntax;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Represents a standalone <c>[[photo: path]]</c> token in Markdown content.
/// </summary>
internal sealed class PhotoBlock : LeafBlock
{
    public PhotoBlock(BlockParser parser)
        : base(parser) => ProcessInlines = false;

    /// <summary>
    /// Gets the image path as written, resolved like a Markdown content image.
    /// </summary>
    public required string ImagePath { get; init; }

    /// <summary>
    /// Gets whether the photo page returns to this page (<c>true</c>) or uses the photo's
    /// primary context (<c>| gallery</c>).
    /// </summary>
    public bool UsesPageContext { get; init; }
}
