using System.Diagnostics.CodeAnalysis;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Project-level configuration
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "project" section of project.json.
/// Contains project identification and URL settings.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "project": {
///     "name": "My Portfolio",
///     "baseUrl": "https://photos.example.com",
///     "language": "en",
///     "basePath": "/"
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class ProjectConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "project";

    /// <summary>
    /// Project name used for identification
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Base URL for the generated site (e.g., "https://example.com")
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Primary language code (e.g., "en", "de")
    /// </summary>
    public string Language { get; init; } = "en";

    /// <summary>
    /// Base path/URL for image references in generated HTML.
    /// Use absolute URL for CDN (e.g., "https://cdn.example.com/images/").
    /// When null, uses relative paths (e.g., "images/" or "../images/").
    /// </summary>
    /// <example>
    /// CDN: "https://cdn.example.com/images/" → src="https://cdn.example.com/images/photo/640.jpg"
    /// Default: null → src="images/photo/640.jpg" or src="../images/photo/640.jpg"
    /// </example>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Can be relative path or absolute URL")]
    public string? ImageBasePath { get; init; }

    /// <summary>
    /// Base path for subdirectory hosting (e.g., "/photos/" for hosting at example.com/photos/).
    /// Must start and end with "/". Default is "/" for root hosting.
    /// Used for CSS, navigation links, and site title link.
    /// </summary>
    /// <example>
    /// Root hosting: "/" → href="main.css"
    /// Subdirectory: "/photos/" → href="/photos/main.css"
    /// </example>
    public string BasePath { get; init; } = "/";
}
