namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Placeholder strategy for lazy-loaded images
/// </summary>
/// <remarks>
/// Controls what is shown while the actual image loads.
/// </remarks>
public enum PlaceholderStrategy
{
    /// <summary>No placeholder - images appear when fully loaded</summary>
    None,

    /// <summary>
    /// CSS-only LQIP hash - minimal markup, pure CSS rendering (~7 bytes)
    /// </summary>
    /// <remarks>
    /// Encodes image into a 20-bit integer decoded via CSS calc().
    /// Renders as 6 radial gradients over a base color (3×2 grid).
    /// Based on: https://leanrada.com/notes/css-only-lqip/
    /// </remarks>
    CssHash
}

/// <summary>
/// Image placeholder configuration
/// </summary>
/// <remarks>
/// <para>
/// Configures how placeholder images are generated for lazy loading.
/// CssHash encodes image data into a compact integer that CSS decodes
/// and renders as radial gradients.
/// </para>
/// <para>
/// The placeholder is embedded as a CSS variable and rendered as background,
/// providing instant visual feedback without additional HTTP requests.
/// </para>
/// <example>
/// <code>
/// // project.json - default (CssHash enabled)
/// {
///   "generate": {
///     "images": {
///       "placeholder": {}
///     }
///   }
/// }
///
/// // project.json - disable placeholders
/// {
///   "generate": {
///     "images": {
///       "placeholder": {
///         "strategy": "none"
///       }
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class PlaceholderConfig
{
    /// <summary>
    /// Placeholder generation strategy
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>none</c> - No placeholder (0 bytes)</item>
    ///   <item><c>csshash</c> - CSS-only LQIP hash (~7 bytes, default)</item>
    /// </list>
    /// </remarks>
    public PlaceholderStrategy Strategy { get; init; } = PlaceholderStrategy.CssHash;
}
