namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Global CLI settings (from revela.json root level)
/// </summary>
/// <remarks>
/// <para>
/// Binds to root-level properties in revela.json:
/// </para>
/// <code>
/// {
///   "checkUpdates": true
/// }
/// </code>
/// </remarks>
public sealed class GlobalSettingsConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "settings";

    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckUpdates { get; init; } = true;
}
