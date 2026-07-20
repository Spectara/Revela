using Spectara.Revela.Features.Generate.Filtering;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Represents an invalid inline-gallery filter with its Markdown source location.
/// </summary>
internal sealed class GalleryBlockParseException : InvalidOperationException
{
    public GalleryBlockParseException()
        : base("Invalid inline gallery filter")
    {
    }

    public GalleryBlockParseException(string message)
        : base(message)
    {
    }

    public GalleryBlockParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GalleryBlockParseException(
        string sourcePath,
        int line,
        string filterExpression,
        FilterParseException innerException)
        : base(
            $"{sourcePath}:{line}: invalid inline gallery filter at position {innerException.Position}: {innerException.Message}",
            innerException)
    {
        SourcePath = sourcePath;
        Line = line;
        FilterExpression = filterExpression;
        FilterPosition = innerException.Position;
    }

    public string SourcePath { get; } = string.Empty;

    public int Line { get; }

    public string FilterExpression { get; } = string.Empty;

    public int FilterPosition { get; }
}
