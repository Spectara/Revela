using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Package dependencies and the NuGet feeds they are restored from.
/// </summary>
/// <remarks>
/// <para>
/// <c>revela.json</c> (global) and <c>project.json</c> (local) use the same shape and are
/// merged per key by the .NET configuration system (later layers win):
/// </para>
/// <example>
/// <code>
/// {
///   "theme": { "name": "Lumina" },
///   "dependencies": {
///     "feeds":    { "test": "../my-feed", "myFeed": "https://example.com/v3/index.json" },
///     "packages": { "Spectara.Revela.Themes.Lumina": "1.0.0", "Acme.Revela.Watermark": "1.0.0" }
///   }
/// }
/// </code>
/// </example>
/// <para>
/// The package type (theme or plugin) is read from the installed package itself, never
/// derived from its ID. The root <c>plugins</c> node is reserved for plugin settings.
/// </para>
/// </remarks>
[RevelaConfig("dependencies")]
public sealed class DependenciesConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "dependencies";

    /// <summary>
    /// Required packages: package ID → exact version.
    /// </summary>
    /// <remarks>
    /// Install commands always persist the exact installed version. A missing value or
    /// <c>"latest"</c> resolves to the newest stable version (prereleases only for
    /// prerelease hosts).
    /// </remarks>
    public Dictionary<string, string?> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Additional NuGet feeds: name → URL or folder path.
    /// </summary>
    /// <remarks>
    /// nuget.org and the bundled <c>packages/</c> folder are always available and do not
    /// appear here. Relative folder paths are resolved relative to the file that declares
    /// them. Feeds declared only in <c>project.json</c> require explicit consent before use.
    /// </remarks>
    public Dictionary<string, string> Feeds { get; } = new(StringComparer.OrdinalIgnoreCase);
}
