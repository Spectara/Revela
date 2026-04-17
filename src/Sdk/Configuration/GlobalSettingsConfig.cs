using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

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
[RevelaConfig("settings", ValidateDataAnnotations = false, ValidateOnStart = false)]
public sealed class GlobalSettingsConfig
{
    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckUpdates { get; init; } = true;
}
