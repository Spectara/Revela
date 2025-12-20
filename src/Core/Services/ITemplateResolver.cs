using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Resolves templates from multiple sources with priority-based override support.
/// </summary>
/// <remarks>
/// <para>
/// Resolution priority (highest first):
/// 1. Local project overrides (themes/{ThemeName}/)
/// 2. Theme extensions (by partialPrefix)
/// 3. Base theme (embedded resources)
/// </para>
/// <para>
/// Key derivation:
/// - Theme: Body/Gallery.revela → body/gallery
/// - Extension: Partials/Cameras.revela + prefix "statistics" → statistics/cameras
/// - Local: Body/Gallery.revela → body/gallery (overrides theme)
/// </para>
/// </remarks>
public interface ITemplateResolver
{
    /// <summary>
    /// Initializes the resolver by scanning all template sources.
    /// </summary>
    /// <param name="theme">The base theme plugin</param>
    /// <param name="extensions">Theme extensions to include</param>
    /// <param name="projectPath">Project path for local overrides</param>
    void Initialize(IThemePlugin theme, IReadOnlyList<IThemeExtension> extensions, string projectPath);

    /// <summary>
    /// Gets a template by its key.
    /// </summary>
    /// <param name="key">Template key (e.g., "body/gallery", "statistics/overview")</param>
    /// <returns>Template content as stream, or null if not found</returns>
    Stream? GetTemplate(string key);

    /// <summary>
    /// Gets the layout template path.
    /// </summary>
    /// <returns>Path to layout template (convention: Layout.revela)</returns>
    string GetLayoutPath();

    /// <summary>
    /// Gets all resolved template keys for debugging/logging.
    /// </summary>
    /// <returns>Dictionary of key → source description</returns>
    IReadOnlyDictionary<string, string> GetResolvedTemplates();

    /// <summary>
    /// Checks if a template exists.
    /// </summary>
    /// <param name="key">Template key to check</param>
    /// <returns>True if template exists in any source</returns>
    bool HasTemplate(string key);

    /// <summary>
    /// Gets all resolved template entries with full source information.
    /// </summary>
    /// <returns>List of resolved template entries</returns>
    IReadOnlyList<ResolvedFileInfo> GetAllEntries();
}

/// <summary>
/// Represents a resolved file with source information.
/// </summary>
/// <param name="Key">Template/asset key (e.g., "body/gallery", "statistics/overview")</param>
/// <param name="OriginalPath">Original path in source (e.g., "Body/Gallery.revela")</param>
/// <param name="Source">Source type (Theme, Extension, Local)</param>
/// <param name="ExtensionName">Extension name if from extension, null otherwise</param>
public sealed record ResolvedFileInfo(
    string Key,
    string OriginalPath,
    FileSourceType Source,
    string? ExtensionName);

/// <summary>
/// Represents the source type of a resolved file.
/// </summary>
public enum FileSourceType
{
    /// <summary>File from base theme</summary>
    Theme,

    /// <summary>File from theme extension</summary>
    Extension,

    /// <summary>File from local project override</summary>
    Local
}

/// <summary>
/// Represents the source of a resolved template.
/// </summary>
public enum TemplateSourceType
{
    /// <summary>Template from base theme</summary>
    Theme,

    /// <summary>Template from theme extension</summary>
    Extension,

    /// <summary>Template from local project override</summary>
    Local
}
