using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Internal entry for resolved theme files (templates, assets).
/// </summary>
/// <param name="SourceType">Source type for metadata/display purposes.</param>
/// <param name="Path">Path to the file (local filesystem or embedded resource key).</param>
/// <param name="Extension">Theme extension reference, if from an extension.</param>
/// <param name="StreamFactory">Factory that opens a read stream for this entry.</param>
internal sealed record ResolvedEntry(
    FileSourceType SourceType,
    string Path,
    ITheme? Extension,
    Func<Stream?> StreamFactory);
