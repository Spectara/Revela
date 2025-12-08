namespace Spectara.Revela.Commands.Generate;

/// <summary>
/// Supported image file extensions for the site generator.
/// </summary>
/// <remarks>
/// Centralizes the list of image extensions used throughout the generate feature.
/// Used by content scanning and navigation building.
/// </remarks>
public static class SupportedImageExtensions
{
    /// <summary>
    /// File extensions recognized as images (case-insensitive comparison required).
    /// </summary>
    public static readonly string[] All = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    /// <summary>
    /// Checks if a file extension is a supported image format.
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".jpg").</param>
    /// <returns>True if the extension is a supported image format.</returns>
    public static bool IsSupported(string extension)
        => All.Contains(extension, StringComparer.OrdinalIgnoreCase);
}
