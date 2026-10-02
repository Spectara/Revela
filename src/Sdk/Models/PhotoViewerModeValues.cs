namespace Spectara.Revela.Sdk.Models;

/// <summary>
/// The one mapping between <see cref="PhotoViewerMode"/> and its configuration/template value
/// (<c>page</c>, <c>lightbox</c>, <c>none</c>) used in theme.json, project.json, front matter
/// (<c>photo_viewer</c>) and templates (<c>viewer_mode</c>).
/// </summary>
public static class PhotoViewerModeValues
{
    private static readonly PhotoViewerMode[] Modes = [PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None];

    /// <summary>
    /// Gets the lowercase value of a mode, e.g. <c>lightbox</c>.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The value used in configuration files and templates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined mode.</exception>
    public static string ToValue(this PhotoViewerMode mode) => mode switch
    {
        PhotoViewerMode.Page => "page",
        PhotoViewerMode.Lightbox => "lightbox",
        PhotoViewerMode.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown photo viewer mode.")
    };

    /// <summary>
    /// Parses a mode value, ignoring case and surrounding whitespace. Unlike
    /// <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> it rejects numbers.
    /// </summary>
    /// <param name="value">The value, e.g. <c>Lightbox</c>.</param>
    /// <param name="mode">The parsed mode, or <see cref="PhotoViewerMode.None"/> when parsing fails.</param>
    /// <returns><c>true</c> for <c>page</c>, <c>lightbox</c> or <c>none</c>.</returns>
    public static bool TryParse(string? value, out PhotoViewerMode mode)
    {
        var trimmed = value?.Trim();
        foreach (var candidate in Modes)
        {
            if (string.Equals(trimmed, candidate.ToValue(), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }

        mode = PhotoViewerMode.None;
        return false;
    }
}
