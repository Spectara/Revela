using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Calendar;

/// <summary>
/// Artifact identifiers published by the Calendar plugin.
/// </summary>
public static class CalendarArtifacts
{
    /// <summary>
    /// Generated <c>calendar.json</c> data consumed by calendar page templates:
    /// <c>.revela/calendar/&lt;page&gt;/calendar.json</c> (<see cref="ArtifactKind.Cache"/>, rebuilt from the
    /// manifest and the pages' <c>.ics</c> files).
    /// </summary>
    public static ArtifactId Data { get; } =
        new("calendar/data");
}
