using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Context for template rendering, loaded from configuration
/// </summary>
/// <remarks>
/// <para>
/// This maps the JSON configuration to a hierarchical object model used by templates.
/// Site metadata is loaded dynamically from site.json to support theme-specific properties.
/// </para>
/// <para>
/// For section-based configuration (generate, dependencies), use the proper
/// config classes like <see cref="Core.Configuration.GenerateConfig"/>.
/// </para>
/// </remarks>
internal sealed record RenderContext
{
    /// <summary>Project-level settings (name, base URL, language)</summary>
    public required RenderProjectSettings Project { get; init; }

    /// <summary>
    /// Site metadata loaded dynamically from site.json.
    /// Supports arbitrary properties defined by the theme.
    /// </summary>
    public JsonElement? Site { get; init; }

    /// <summary>Theme name</summary>
    public required string ThemeName { get; init; }
}

/// <summary>
/// Project settings for rendering context
/// </summary>
internal sealed record RenderProjectSettings
{
    /// <summary>Project name used for identification</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base URL for the generated site (e.g., "https://example.com")</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Primary language code (e.g., "en", "de")</summary>
    public string Language { get; init; } = "en";

    /// <summary>
    /// Base path/URL for image references in generated HTML.
    /// Use absolute URL for CDN (e.g., "https://cdn.example.com/images/").
    /// When null, uses relative paths (e.g., "images/" or "../images/").
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Can be relative path or absolute URL")]
    public string? ImageBasePath { get; init; }

    /// <summary>
    /// Base path for subdirectory hosting (e.g., "/photos/" for hosting at example.com/photos/).
    /// Must start and end with "/". Default is "/" for root hosting.
    /// </summary>
    public string BasePath { get; init; } = "/";
}
