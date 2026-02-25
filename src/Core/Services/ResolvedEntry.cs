using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Internal entry for resolved theme files (templates, assets).
/// </summary>
/// <remarks>
/// Shared by <see cref="TemplateResolver"/> and <see cref="AssetResolver"/>
/// to represent a resolved file from any source (theme, extension, or local).
/// </remarks>
internal sealed record ResolvedEntry(
    FileSourceType SourceType,
    string Path,
    IThemeExtension? Extension);
