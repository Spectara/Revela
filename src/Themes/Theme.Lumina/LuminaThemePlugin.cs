using Spectara.Revela.Core.Themes;

namespace Spectara.Revela.Theme.Lumina;

/// <summary>
/// Lumina theme plugin - elegant photography portfolio design
/// </summary>
/// <remarks>
/// This theme provides a complete, production-ready design including:
/// - CSS-only hamburger navigation
/// - Responsive image grid
/// - CSS-based lightbox
/// - EXIF data overlay
/// - Mobile-first design
///
/// All configuration is in theme.json (embedded resource).
/// </remarks>
public sealed class LuminaThemePlugin() : EmbeddedThemePlugin(typeof(LuminaThemePlugin).Assembly);

