using Markdig.Parsers;
using Markdig.Syntax;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Parses standalone top-level <c>[[photo: path]]</c> and <c>[[photo: path | gallery]]</c> blocks.
/// </summary>
/// <remarks>
/// The path is everything between <c>photo:</c> and the optional <c>| option</c>, trimmed, so
/// paths may contain spaces without angle brackets. Text such as <c>[[photography]]</c> is not a
/// token; anything else starting with <c>[[photo</c> must be a valid token.
/// </remarks>
internal sealed class PhotoBlockParser : BlockParser
{
    private const string TokenStart = "[[photo";
    private const string TokenPrefix = "[[photo:";
    private const string TokenSuffix = "]]";
    private const string GalleryOption = "gallery";
    private readonly string sourcePath;

    public PhotoBlockParser(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        this.sourcePath = sourcePath;
        OpeningCharacters = ['['];
    }

    /// <summary>
    /// Gets whether <paramref name="text"/> starts with something meant as a photo token
    /// (as opposed to a longer word such as <c>[[photography]]</c>).
    /// </summary>
    public static bool StartsWithToken(ReadOnlySpan<char> text) =>
        text.StartsWith(TokenStart, StringComparison.Ordinal) &&
        (text.Length == TokenStart.Length || !char.IsLetterOrDigit(text[TokenStart.Length]));

    /// <inheritdoc />
    public override BlockState TryOpen(BlockProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        if (processor.IsCodeIndent || processor.CurrentBlock is ParagraphBlock ||
            processor.GetCurrentContainerOpened() is not MarkdownDocument)
        {
            return BlockState.None;
        }

        var line = processor.Line.ToString().Trim();
        if (!StartsWithToken(line))
        {
            return BlockState.None;
        }

        var lineNumber = processor.LineIndex + 1;
        if (!line.StartsWith(TokenPrefix, StringComparison.Ordinal) ||
            !line.EndsWith(TokenSuffix, StringComparison.Ordinal))
        {
            throw new PhotoBlockParseException(sourcePath, lineNumber, line, "malformed photo token");
        }

        var parts = line[TokenPrefix.Length..^TokenSuffix.Length].Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length > 2)
        {
            throw new PhotoBlockParseException(sourcePath, lineNumber, line, "only one option is allowed");
        }

        var imagePath = parts[0];
        if (imagePath.Length == 0)
        {
            throw new PhotoBlockParseException(sourcePath, lineNumber, line, "photo path cannot be empty");
        }

        var usesPageContext = true;
        if (parts.Length == 2)
        {
            if (!parts[1].Equals(GalleryOption, StringComparison.Ordinal))
            {
                throw new PhotoBlockParseException(
                    sourcePath,
                    lineNumber,
                    line,
                    $"unknown option '{parts[1]}'; the only option is '{GalleryOption}'");
            }

            usesPageContext = false;
        }

        var block = new PhotoBlock(this)
        {
            ImagePath = imagePath,
            UsesPageContext = usesPageContext,
            Line = processor.LineIndex,
            Column = processor.Column,
            Span = new SourceSpan(processor.Start, processor.Line.End)
        };

        processor.NewBlocks.Push(block);
        return BlockState.BreakDiscard;
    }
}
