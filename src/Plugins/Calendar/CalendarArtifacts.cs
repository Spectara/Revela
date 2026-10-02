using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Plugins.Calendar;

/// <summary>
/// Artifact identifiers published by the Calendar plugin.
/// </summary>
public static class CalendarArtifacts
{
    /// <summary>Generated <c>calendar.json</c> data consumed by calendar page templates.</summary>
    public static ArtifactId Data { get; } =
        new("spectara.revela.calendar/data");
}
