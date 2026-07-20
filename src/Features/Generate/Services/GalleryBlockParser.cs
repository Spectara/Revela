using Markdig.Parsers;
using Markdig.Syntax;
using Spectara.Revela.Features.Generate.Filtering;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Parses standalone top-level <c>[[gallery]]</c> blocks.
/// </summary>
internal sealed class GalleryBlockParser : BlockParser
{
    private const string BareToken = "[[gallery]]";
    private const string FilterPrefix = "[[gallery:";
    private const string TokenSuffix = "]]";
    private readonly string sourcePath;

    public GalleryBlockParser(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        this.sourcePath = sourcePath;
        OpeningCharacters = ['['];
    }

    /// <inheritdoc />
    public override BlockState TryOpen(BlockProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        if (processor.IsCodeIndent || processor.CurrentBlock is ParagraphBlock ||
            processor.GetCurrentContainerOpened() is not MarkdownDocument)
        {
            return BlockState.None;
        }

        var line = processor.Line.ToString().TrimEnd();
        string? filterExpression;

        if (line.Equals(BareToken, StringComparison.Ordinal))
        {
            filterExpression = null;
        }
        else if (line.StartsWith(FilterPrefix, StringComparison.Ordinal) &&
                 line.EndsWith(TokenSuffix, StringComparison.Ordinal))
        {
            filterExpression = line[FilterPrefix.Length..^TokenSuffix.Length].Trim();
            ValidateFilter(filterExpression, processor.LineIndex + 1);
        }
        else
        {
            return BlockState.None;
        }

        var block = new GalleryBlock(this)
        {
            FilterExpression = filterExpression,
            Line = processor.LineIndex,
            Column = processor.Column,
            Span = new SourceSpan(processor.Start, processor.Line.End)
        };

        processor.NewBlocks.Push(block);
        return BlockState.BreakDiscard;
    }

    private void ValidateFilter(string filterExpression, int line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
            {
                throw new FilterParseException("Filter expression cannot be empty", 0);
            }

            FilterService.ParseQuery(filterExpression);
        }
        catch (FilterParseException ex)
        {
            throw new GalleryBlockParseException(sourcePath, line, filterExpression, ex);
        }
    }
}
