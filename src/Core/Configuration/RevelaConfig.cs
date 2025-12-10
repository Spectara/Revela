namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Root configuration for a Revela project
/// </summary>
public sealed class RevelaConfig
{
    /// <summary>Project-level settings (name, base URL, language)</summary>
    public ProjectSettings Project { get; init; } = new();

    /// <summary>Site metadata (title, description, author)</summary>
    public SiteSettings Site { get; init; } = new();

    /// <summary>Theme configuration</summary>
    public ThemeSettings Theme { get; init; } = new();

    /// <summary>Generate configuration (output, images, cameras)</summary>
    public GenerateSettings Generate { get; init; } = new();
}

/// <summary>
/// Project-level settings
/// </summary>
public sealed class ProjectSettings
{
    /// <summary>Project name used for identification</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base URL for the generated site (e.g., "https://example.com")</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Primary language code (e.g., "en", "de")</summary>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Can be relative path or absolute URL")]
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

/// <summary>
/// Site metadata for SEO and display
/// </summary>
public sealed class SiteSettings
{
    /// <summary>Site title displayed in browser and headers</summary>
    public string? Title { get; init; }

    /// <summary>Site description for SEO meta tags</summary>
    public string? Description { get; init; }

    /// <summary>Author name for attribution</summary>
    public string? Author { get; init; }

    /// <summary>Copyright notice</summary>
    public string? Copyright { get; init; }
}

/// <summary>
/// Theme configuration
/// </summary>
public sealed class ThemeSettings
{
    /// <summary>Theme name to use (from themes/ directory)</summary>
    public string Name { get; init; } = "default";
}

/// <summary>
/// Generate output configuration
/// </summary>
public sealed class GenerateSettings
{
    /// <summary>Output directory path (relative to project root)</summary>
    public string Output { get; init; } = "output";

    /// <summary>Image processing settings</summary>
    public ImageSettings Images { get; init; } = new();

    /// <summary>Camera model transformation settings</summary>
    public CameraSettings Cameras { get; init; } = new();
}

/// <summary>
/// Image processing configuration
/// </summary>
public sealed class ImageSettings
{
    /// <summary>JPEG/WebP quality (1-100, default 90)</summary>
    public int Quality { get; init; } = 90;

    /// <summary>Output formats to generate (e.g., "webp", "jpg")</summary>
    public IReadOnlyList<string> Formats { get; init; } = ["webp", "jpg"];

    /// <summary>Image widths to generate (in pixels)</summary>
    public IReadOnlyList<int> Sizes { get; init; } = [640, 1024, 1280, 1920, 2560];
}

/// <summary>
/// Camera model transformation settings
/// </summary>
/// <remarks>
/// Custom mappings override built-in defaults for Sony ILCE → α series.
/// Configure in project.json:
/// <code>
/// {
///   "generate": {
///     "cameras": {
///       "models": { "ILCE-7M4": "α 7 IV" },
///       "makes": { "SONY": "Sony" }
///     }
///   }
/// }
/// </code>
/// </remarks>
public sealed class CameraSettings
{
    /// <summary>
    /// Custom camera model mappings (e.g., "ILCE-7M4" → "α 7 IV").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Models { get; init; } = [];

    /// <summary>
    /// Custom manufacturer name mappings (e.g., "SONY" → "Sony").
    /// Merged with built-in defaults (custom values override defaults).
    /// </summary>
    public Dictionary<string, string> Makes { get; init; } = [];
}
