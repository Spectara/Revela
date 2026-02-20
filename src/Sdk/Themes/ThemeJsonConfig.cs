using System.Text.Json;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// JSON configuration structure for theme manifest files (manifest.json / theme.json).
/// </summary>
/// <remarks>
/// Used by both <see cref="EmbeddedThemePlugin"/> (reads embedded manifest.json)
/// and LocalThemeAdapter (reads theme.json from disk).
/// Properties are mutable for JSON deserialization.
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

    /// <summary>Preview image URI.</summary>
    public Uri? PreviewImage { get; set; }

    /// <summary>Tags for theme discovery.</summary>
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>Template configuration.</summary>
    public ThemeTemplatesConfig? Templates { get; set; }

    /// <summary>Theme variables with default values.</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; set; }

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
