using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Resolves templates from multiple sources with priority-based override support.
/// </summary>
public interface ITemplateResolver
{
    /// <summary>
    /// Initializes the resolver by scanning all template sources.
    /// </summary>
    void Initialize(ITheme theme, IReadOnlyList<ITheme> extensions, string projectPath);

    /// <summary>
    /// Gets a template by its key.
    /// </summary>
    Stream? GetTemplate(string key);

    /// <summary>
    /// Gets all resolved template entries with full source information.
    /// </summary>
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
