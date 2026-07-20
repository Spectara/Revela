using Markdig.Parsers;
using Markdig.Syntax;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Represents a standalone inline-gallery token in Markdown content.
/// </summary>
internal sealed class GalleryBlock : LeafBlock
{
    public GalleryBlock(BlockParser parser)
        : base(parser) => ProcessInlines = false;

    /// <summary>
    /// Gets the optional global image filter expression.
    /// </summary>
    public string? FilterExpression { get; init; }
}
