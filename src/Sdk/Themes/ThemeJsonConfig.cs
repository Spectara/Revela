using System.Text.Json;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Unified JSON configuration for theme manifest files (manifest.json / theme.json).
/// </summary>
/// <remarks>
/// Supports both base themes and extensions in one format:
/// <list type="bullet">
/// <item>Base themes: Name, Version, Description, Author, PreviewImage, Tags, Templates, Variables</item>
/// <item>Extensions: Name, Version, Description, Author, TargetTheme, Prefix, Variables, TemplateDefaults</item>
/// </list>
/// </remarks>
public sealed class ThemeJsonConfig
{
    /// <summary>Theme display name.</summary>
    public string? Name { get; set; }

    /// <summary>Theme version (SemVer).</summary>
    public string? Version { get; set; }

    /// <summary>Theme description.</summary>
    public string? Description { get; set; }

    /// <summary>Theme author.</summary>
    public string? Author { get; set; }

    /// <summary>Preview image URI (base themes only).</summary>
    public Uri? PreviewImage { get; set; }

    /// <summary>Tags for theme discovery (base themes only).</summary>
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>Target theme name for extensions (null for base themes).</summary>
    public string? TargetTheme { get; set; }

    /// <summary>Prefix for extension templates and assets (null for base themes).</summary>
    public string? Prefix { get; set; }

    /// <summary>Template configuration (layout path).</summary>
    public ThemeTemplatesConfig? Templates { get; set; }

    /// <summary>Theme variables with default values.</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; set; }

    /// <summary>Default data sources for extension templates.</summary>
    public IReadOnlyDictionary<string, TemplateDataConfig>? TemplateDefaults { get; set; }

    /// <summary>Shared JSON serialization options for theme config files.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Templates section in theme configuration.
/// </summary>
public sealed class ThemeTemplatesConfig
{
    /// <summary>Main layout template path.</summary>
    public string? Layout { get; set; }
}

/// <summary>
/// Template data configuration for extension default data sources.
/// </summary>
public sealed class TemplateDataConfig
{
    /// <summary>Default data source filenames (variable name → filename).</summary>
    public IReadOnlyDictionary<string, string>? Data { get; set; }
}
