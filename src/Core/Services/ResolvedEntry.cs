using Spectara.Revela.Sdk.Abstractions;

using Spectara.Revela.Sdk.Services;
namespace Spectara.Revela.Core.Services;

/// <summary>
/// Internal entry for resolved theme files (templates, assets).
/// </summary>
internal sealed record ResolvedEntry(
    FileSourceType SourceType,
    string Path,
    ITheme? Extension);
