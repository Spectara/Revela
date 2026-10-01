namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Represents a malformed <c>[[photo: …]]</c> token with its Markdown source location.
/// </summary>
internal sealed class PhotoBlockParseException : InvalidOperationException
{
    public PhotoBlockParseException()
        : base("Invalid photo token")
    {
    }

    public PhotoBlockParseException(string message)
        : base(message)
    {
    }

    public PhotoBlockParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PhotoBlockParseException(string sourcePath, int line, string token, string reason)
        : base(
            $"{sourcePath}:{line}: invalid photo token '{token}': {reason}. " +
            "Expected [[photo: <path>]] or [[photo: <path> | gallery]].")
    {
        SourcePath = sourcePath;
        Line = line;
        Token = token;
    }

    public string SourcePath { get; } = string.Empty;

    public int Line { get; }

    public string Token { get; } = string.Empty;
}
