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
[RevelaConfig("settings", ValidateDataAnnotations = false)]
public sealed class GlobalSettingsConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "settings";
    /// <summary>
    /// Whether to check for updates on startup
    /// </summary>
    public bool CheckUpdates { get; set; } = true;
}
