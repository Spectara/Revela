namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Configuration for package management (NuGet feeds, cache settings)
/// </summary>
/// <remarks>
/// <para>
/// This configuration can be merged from multiple sources (in order, later wins):
/// </para>
/// <list type="number">
/// <item><b>revela.json</b> (global): User-wide package settings</item>
/// <item><b>project.json</b> (local): Project-specific settings (e.g., private company feed)</item>
/// </list>
/// <para>
/// nuget.org is always available as a built-in source and doesn't need to be configured.
/// </para>
/// <example>
/// <code>
/// // revela.json or project.json
/// {
///   "packages": {
///     "feeds": {
///       "MyCompany": "https://nuget.mycompany.com/v3/index.json",
///       "Local": "./plugins"
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class PackagesConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "packages";

    /// <summary>
    /// Configured NuGet feeds (Key = Name, Value = URL)
    /// </summary>
    /// <remarks>
    /// <para>
    /// nuget.org is always available as built-in source and doesn't appear here.
    /// </para>
    /// <para>
    /// URLs can be remote (https://...) or local paths (./plugins, C:\feeds).
    /// Relative paths are resolved relative to the config file location.
    /// </para>
    /// </remarks>
    public Dictionary<string, string> Feeds { get; init; } = [];
}
