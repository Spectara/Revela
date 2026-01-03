using Spectara.Revela.Sdk.Themes;

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
/// All configuration is in manifest.json (embedded resource).
/// Usage: {{ include 'statistics/overview' stats }}
/// </remarks>
public sealed class LuminaStatisticsExtension()
    : EmbeddedThemeExtension(typeof(LuminaStatisticsExtension).Assembly);
