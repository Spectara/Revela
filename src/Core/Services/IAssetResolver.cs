using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

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
    /// Gets the CSS files that apply to a given page-type scope, in order.
    /// </summary>
    /// <param name="scope">
    /// Page scope token (e.g. <c>index</c>, <c>gallery</c>, <c>photo</c>, or a plugin
    /// prefix such as <c>statistics</c>). When null, all stylesheets are returned.
    /// </param>
    /// <remarks>
    /// A declared stylesheet is included when it declares the <c>all</c> token,
    /// has no scope restriction, or declares the requested <paramref name="scope"/>.
    /// Undeclared stylesheets are not returned for scoped queries.
    /// </remarks>
    IReadOnlyList<string> GetStyleSheets(string? scope);

    /// <summary>
    /// Gets all JS files in order.
    /// </summary>
    IReadOnlyList<string> GetScripts();

    /// <summary>
    /// Gets the JS files that apply to a given page-type scope, in order.
    /// </summary>
    /// <param name="scope">
    /// Page scope token. When null, all scripts are returned.
    /// </param>
    /// <remarks>
    /// A declared script is included when it declares the <c>all</c> token,
    /// has no scope restriction, or declares the requested <paramref name="scope"/>.
    /// Undeclared scripts are not returned for scoped queries.
    /// </remarks>
    IReadOnlyList<string> GetScripts(string? scope);

    /// <summary>
    /// Copies all resolved assets to the output directory.
    /// </summary>
    Task CopyToOutputAsync(string outputDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resolved asset entries with full source information.
    /// </summary>
    IReadOnlyList<ResolvedFileInfo> GetAllEntries();
}
