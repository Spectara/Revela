namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Theme configuration settings.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "theme" section of project.json.
/// Contains theme selection and theme-specific options.
/// </para>
/// <para>
/// Image sizes can be overridden locally via theme/images.json.
/// If not present, sizes come from the theme's GetImagesTemplate().
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "theme": {
///     "name": "Lumina"
///   }
/// }
///
/// // theme/images.json (optional override)
/// {
///   "theme": {
///     "images": {
///       "sizes": [640, 1280, 2560]
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class ThemeConfig
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "theme";

    /// <summary>
    /// Name of the theme to use (e.g., "Lumina").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Image settings from theme (sizes for responsive breakpoints).
    /// </summary>
    public ThemeImagesConfig Images { get; init; } = new();
}

/// <summary>
/// Theme image settings (responsive breakpoints).
/// </summary>
/// <remarks>
/// <para>
/// The theme defines which image sizes to generate based on its CSS breakpoints.
/// Users typically don't need to change this unless they modify the theme's CSS.
/// </para>
/// <para>
/// To override, create a local theme/images.json file with custom sizes.
/// This completely replaces theme defaults (no merging).
/// </para>
/// </remarks>
public sealed class ThemeImagesConfig
{
    /// <summary>
    /// Image sizes to generate (in pixels) for responsive images.
    /// </summary>
    /// <remarks>
    /// These match the theme's CSS breakpoints. Empty means use theme defaults.
    /// The value represents the dimension specified by <see cref="ResizeMode"/>.
    /// </remarks>
    public IReadOnlyList<int> Sizes { get; init; } = [];

    /// <summary>
    /// Which dimension to use for resizing images.
    /// </summary>
    /// <remarks>
    /// - "longest": Size applies to the longest side (portrait=height, landscape=width).
    ///   Best for justified galleries where images have varying aspect ratios.
    /// - "width": Size applies to width (traditional, all images same width).
    ///   Best for grid layouts with fixed columns.
    /// - "height": Size applies to height (all images same height).
    ///   Best for filmstrip/carousel layouts.
    /// </remarks>
    public string ResizeMode { get; init; } = "longest";
}
