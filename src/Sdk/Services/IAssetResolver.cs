using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Resolves assets from multiple sources with priority-based override support.
/// </summary>
public interface IAssetResolver
{
    /// <summary>
    /// Initializes the resolver by scanning all asset sources.
    /// </summary>
    void Initialize(ITheme theme, IReadOnlyList<ITheme> extensions, string projectPath);

    /// <summary>
    /// Gets all CSS files in order.
    /// </summary>
    IReadOnlyList<string> GetStyleSheets();

    /// <summary>
    /// Gets all JS files in order.
    /// </summary>
    IReadOnlyList<string> GetScripts();

    /// <summary>
    /// Copies all resolved assets to the output directory.
    /// </summary>
    Task CopyToOutputAsync(string outputDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resolved asset entries with full source information.
    /// </summary>
    IReadOnlyList<ResolvedFileInfo> GetAllEntries();
}
