using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Resolves assets from multiple sources with priority-based override support.
/// </summary>
/// <remarks>
/// <para>
/// Resolution priority (highest first):
/// 1. Local project overrides (themes/{ThemeName}/Assets/)
/// 2. Theme extensions (Assets/ → {PartialPrefix}/)
/// 3. Base theme (Assets/)
/// </para>
/// <para>
/// Output structure:
/// - All assets copied to _assets/ in output
/// - CSS/JS files tracked separately for ordered inclusion
/// - Extension assets placed under {PartialPrefix}/ subfolder
/// </para>
/// <para>
/// Override behavior:
/// - Same filename = replace (keeps original position in order)
/// - New filename = append (added at end of respective phase)
/// </para>
/// </remarks>
public interface IAssetResolver
{
    /// <summary>
    /// Initializes the resolver by scanning all asset sources.
    /// </summary>
    /// <param name="theme">The base theme plugin</param>
    /// <param name="extensions">Theme extensions to include</param>
    /// <param name="projectPath">Project path for local overrides</param>
    void Initialize(IThemePlugin theme, IReadOnlyList<IThemeExtension> extensions, string projectPath);

    /// <summary>
    /// Gets all CSS files in order (Theme → Extensions → Local additions).
    /// </summary>
    /// <returns>Ordered list of CSS paths relative to _assets/</returns>
    IReadOnlyList<string> GetStyleSheets();

    /// <summary>
    /// Gets all JS files in order (Theme → Extensions → Local additions).
    /// </summary>
    /// <returns>Ordered list of JS paths relative to _assets/</returns>
    IReadOnlyList<string> GetScripts();

    /// <summary>
    /// Gets all other assets (fonts, icons, images).
    /// </summary>
    /// <returns>List of asset paths relative to _assets/</returns>
    IReadOnlyList<string> GetOtherAssets();

    /// <summary>
    /// Copies all resolved assets to the output directory.
    /// </summary>
    /// <param name="outputDirectory">Output directory path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CopyToOutputAsync(string outputDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resolved assets for debugging/logging.
    /// </summary>
    /// <returns>Dictionary of asset path → source description</returns>
    IReadOnlyDictionary<string, string> GetResolvedAssets();

    /// <summary>
    /// Gets all resolved asset entries with full source information.
    /// </summary>
    /// <returns>List of resolved asset entries</returns>
    IReadOnlyList<ResolvedFileInfo> GetAllEntries();
}

/// <summary>
/// Represents the source of a resolved asset.
/// </summary>
public enum AssetSourceType
{
    /// <summary>Asset from base theme</summary>
    Theme,

    /// <summary>Asset from theme extension</summary>
    Extension,

    /// <summary>Asset from local project override</summary>
    Local
}
