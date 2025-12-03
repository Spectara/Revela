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

    /// <summary>Build configuration (output, images, cache)</summary>
    public BuildSettings Build { get; init; } = new();

    /// <summary>Navigation menu structure</summary>
    public IReadOnlyList<NavigationItem> Navigation { get; init; } = [];
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
}

/// <summary>
/// Site metadata for SEO and display
/// </summary>
public sealed class SiteSettings
{
    /// <summary>Site title displayed in browser and headers</summary>
    public string Title { get; init; } = string.Empty;

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
/// Build output configuration
/// </summary>
public sealed class BuildSettings
{
    /// <summary>Output directory path (relative to project root)</summary>
    public string Output { get; init; } = "output";

    /// <summary>Image processing settings</summary>
    public ImageSettings Images { get; init; } = new();

    /// <summary>Cache settings for build optimization</summary>
    public CacheSettings Cache { get; init; } = new();
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
/// Cache configuration for build optimization
/// </summary>
public sealed class CacheSettings
{
    /// <summary>Whether caching is enabled</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Cache EXIF data to avoid re-reading</summary>
    public bool Exif { get; init; } = true;

    /// <summary>Cache generated HTML</summary>
    public bool Html { get; init; } = true;

    /// <summary>Cache directory path (relative to project root)</summary>
    public string Directory { get; init; } = ".revela/cache";
}

/// <summary>
/// Navigation menu item (supports nested hierarchy)
/// </summary>
public sealed class NavigationItem
{
    /// <summary>Display name for the navigation link</summary>
    public required string Name { get; init; }

    /// <summary>External URL (mutually exclusive with Path)</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string? Url { get; init; }

    /// <summary>Internal path to gallery or page</summary>
    public string? Path { get; init; }

    /// <summary>Nested child navigation items</summary>
    public IReadOnlyList<NavigationItem>? Children { get; init; }
}

