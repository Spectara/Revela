using Spectara.Revela.Core.Themes;

namespace Spectara.Revela.Theme.Expose;

/// <summary>
/// Expose theme plugin - original photography portfolio design
/// </summary>
/// <remarks>
/// This theme is based on the original Expose static site generator
/// and provides a complete, production-ready design including:
/// - CSS-only hamburger navigation
/// - Responsive image grid
/// - CSS-based lightbox
/// - EXIF data overlay
/// - Mobile-first design
///
/// All configuration is in theme.json (embedded resource).
/// </remarks>
public sealed class ExposeThemePlugin() : EmbeddedThemePlugin(typeof(ExposeThemePlugin).Assembly);

