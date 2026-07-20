using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Reports token-like gallery text inside nested Markdown containers.
/// </summary>
internal sealed class GalleryTokenWarningInlineParser : InlineParser
{
    private const string TokenPrefix = "[[gallery";
    private readonly HashSet<int> warnedPositions = [];
    private readonly ContentImageContext context;

    public GalleryTokenWarningInlineParser(ContentImageContext context)
    {
        this.context = context;
        OpeningCharacters = ['['];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        ArgumentNullException.ThrowIfNull(processor);

        if (processor.Block?.Parent is MarkdownDocument || !slice.Match(TokenPrefix) ||
            !warnedPositions.Add(slice.Start))
        {
            return false;
        }

        var galleryContext = context.GalleryBlocks;
        galleryContext?.ReportWarning(
            $"{galleryContext.SourcePath}:{processor.Block?.Line + 1}: text looks like a gallery token but is nested; only top-level blocks are recognized.");

        return false;
    }
}
