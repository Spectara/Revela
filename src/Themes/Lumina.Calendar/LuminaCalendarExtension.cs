using Spectara.Revela.Sdk.Themes;

namespace Spectara.Revela.Themes.Lumina.Calendar;

/// <summary>
/// Availability calendar extension for the Lumina theme.
/// </summary>
/// <remarks>
/// Provides styled templates for displaying availability calendars
/// with booked/free days, arrive/depart diagonals (nights mode),
/// and responsive grid layout.
///
/// All configuration is in manifest.json (embedded resource).
/// </remarks>
public sealed class LuminaCalendarExtension()
    : EmbeddedThemeExtension(typeof(LuminaCalendarExtension).Assembly);
