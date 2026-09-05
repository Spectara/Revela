using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Unified JSON configuration for theme manifest files (manifest.json / theme.json).
/// </summary>
/// <remarks>
/// Supports both base themes and extensions in one format:
/// <list type="bullet">
/// <item>Base themes: Name, Version, Description, Author, PreviewImage, Tags, Templates</item>
/// <item>Extensions: Name, Version, Description, Author, TargetTheme, Prefix, TemplateDefaults</item>
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

    /// <summary>Default data sources for extension templates.</summary>
    public IReadOnlyDictionary<string, TemplateDataConfig>? TemplateDefaults { get; set; }

    /// <summary>
    /// Optional stylesheet page-type scope declarations. Each entry restricts a
    /// stylesheet to the listed scope tokens; omitting a scope loads the declared
    /// stylesheet on every page.
    /// </summary>
    public IReadOnlyList<AssetDeclaration>? Stylesheets { get; set; }

    /// <summary>
    /// Optional script page-type scope declarations. Uses the same scope tokens
    /// and load-everywhere default as <see cref="Stylesheets"/>.
    /// </summary>
    public IReadOnlyList<AssetDeclaration>? Scripts { get; set; }

    /// <summary>Photo viewer modes supported by a base theme.</summary>
    public IReadOnlyList<string>? PhotoViewers { get; set; }

    /// <summary>Default photo viewer mode for a base theme.</summary>
    public string? DefaultPhotoViewer { get; set; }

    /// <summary>Shared JSON serialization options for theme config files.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolverChain = { ThemeJsonContext.Default }
    };

    /// <summary>Trim-safe typed metadata for source-generated deserialization.</summary>
    public static JsonTypeInfo<ThemeJsonConfig> JsonTypeInfo => ThemeJsonContext.Default.ThemeJsonConfig;

    /// <summary>
    /// Projects this JSON configuration into a validated runtime theme manifest.
    /// </summary>
    public ThemeManifest CreateManifest() => new()
    {
        LayoutTemplate = Templates?.Layout ?? "layout.revela",
        Stylesheets = Stylesheets,
        Scripts = Scripts,
        PhotoViewer = CreatePhotoViewerCapabilities()
    };

    private PhotoViewerCapabilities? CreatePhotoViewerCapabilities()
    {
        if (TargetTheme is not null)
        {
            if (PhotoViewers is not null)
            {
                throw new InvalidOperationException(
                    "Theme extension field 'photoViewers' must be absent.");
            }

            if (DefaultPhotoViewer is not null)
            {
                throw new InvalidOperationException(
                    "Theme extension field 'defaultPhotoViewer' must be absent.");
            }

            return null;
        }

        if (PhotoViewers is null)
        {
            throw new InvalidOperationException("Base theme field 'photoViewers' is required.");
        }

        if (PhotoViewers.Count == 0)
        {
            throw new InvalidOperationException("Base theme field 'photoViewers' must be non-empty.");
        }

        if (DefaultPhotoViewer is null)
        {
            throw new InvalidOperationException("Base theme field 'defaultPhotoViewer' is required.");
        }

        var supported = new List<PhotoViewerMode>(PhotoViewers.Count);
        var uniqueModes = new HashSet<PhotoViewerMode>();

        foreach (var value in PhotoViewers)
        {
            if (!Enum.TryParse<PhotoViewerMode>(value, ignoreCase: true, out var mode)
                || !Enum.IsDefined(mode))
            {
                throw new InvalidOperationException(
                    $"Base theme field 'photoViewers' contains unknown value '{value}'.");
            }

            if (!uniqueModes.Add(mode))
            {
                throw new InvalidOperationException(
                    $"Base theme field 'photoViewers' contains duplicate value '{value}'.");
            }

            supported.Add(mode);
        }

        if (!Enum.TryParse<PhotoViewerMode>(DefaultPhotoViewer, ignoreCase: true, out var defaultMode)
            || !Enum.IsDefined(defaultMode))
        {
            throw new InvalidOperationException(
                $"Base theme field 'defaultPhotoViewer' contains unknown value '{DefaultPhotoViewer}'.");
        }

        if (!uniqueModes.Contains(defaultMode))
        {
            throw new InvalidOperationException(
                $"Base theme field 'defaultPhotoViewer' value '{DefaultPhotoViewer}' is not listed in 'photoViewers'.");
        }

        return new PhotoViewerCapabilities
        {
            Supported = supported,
            Default = defaultMode
        };
    }
}

/// <summary>
/// Source-generated JSON serializer context for theme configuration types.
/// </summary>
[JsonSerializable(typeof(ThemeJsonConfig))]
[JsonSerializable(typeof(AssetDeclaration))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class ThemeJsonContext : JsonSerializerContext;

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
