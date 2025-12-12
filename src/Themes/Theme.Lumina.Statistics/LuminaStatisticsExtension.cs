using Spectara.Revela.Core.Themes;

namespace Spectara.Revela.Theme.Lumina.Statistics;

/// <summary>
/// Statistics charts extension for the Lumina theme
/// </summary>
/// <remarks>
/// Provides styled templates for displaying EXIF statistics:
/// - Camera usage distribution
/// - Lens popularity
/// - Aperture, focal length, and ISO distributions
///
/// All configuration is in extension.json (embedded resource).
/// Usage: {{ include 'statistics/page' stats }}
/// </remarks>
public sealed class LuminaStatisticsExtension()
    : EmbeddedThemeExtension(typeof(LuminaStatisticsExtension).Assembly);
