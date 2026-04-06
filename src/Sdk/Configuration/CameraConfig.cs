namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Camera model transformation settings
/// </summary>
/// <remarks>
/// Custom mappings override built-in defaults for Sony ILCE → α series.
/// </remarks>
public sealed class CameraConfig
{
    /// <summary>
    /// Custom camera model mappings (e.g., "ILCE-7M4" → "α 7 IV").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Models { get; init; } = [];

    /// <summary>
    /// Custom manufacturer name mappings (e.g., "SONY" → "Sony").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Makes { get; init; } = [];
}
