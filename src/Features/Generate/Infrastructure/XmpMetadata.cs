namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Descriptive photo metadata read from an XMP packet (as written by Capture One, Lightroom, …).
/// </summary>
/// <param name="Title">XMP <c>dc:title</c> (the <c>x-default</c> alternative, else the first).</param>
/// <param name="Description">XMP <c>dc:description</c> (the <c>x-default</c> alternative, else the first).</param>
/// <param name="Keywords">XMP <c>dc:subject</c> entries, trimmed and de-duplicated (case-insensitive).</param>
/// <param name="Rating">XMP <c>xmp:Rating</c> (-1 rejected, 0 unrated, 1–5); <c>null</c> when absent or invalid.</param>
internal sealed record XmpMetadata(
    string? Title,
    string? Description,
    IReadOnlyList<string> Keywords,
    int? Rating)
{
    /// <summary>
    /// Metadata of an image without (usable) XMP.
    /// </summary>
    public static XmpMetadata Empty { get; } = new(null, null, [], null);
}
