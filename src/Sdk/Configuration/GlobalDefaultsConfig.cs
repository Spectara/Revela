using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Default settings for new projects (from revela.json)
/// </summary>
/// <remarks>
/// <para>
/// Binds to the "defaults" section in revela.json:
/// </para>
/// <code>
/// {
///   "defaults": {
///     "theme": "Lumina"
///   }
/// }
/// </code>
/// </remarks>
[RevelaConfig("defaults", ValidateDataAnnotations = false)]
public sealed class GlobalDefaultsConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "defaults";
    /// <summary>
    /// Default theme for new projects
    /// </summary>
    /// <remarks>
    /// Can be a short name (e.g., "Lumina") or full package ID
    /// (e.g., "Spectara.Revela.Themes.Lumina").
    /// </remarks>
    public string Theme { get; set; } = "Lumina";
}
