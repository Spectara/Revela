using System.Reflection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Base class for NuGet-based themes and extensions with embedded resources.
/// </summary>
/// <remarks>
/// <para>
/// Unified base class for both base themes and extensions.
/// Handles all the boilerplate: reads manifest.json from embedded resources,
/// implements GetFile(), GetAllFiles(), ExtractToAsync().
/// </para>
/// <para>
/// Theme authors only need a minimal derived class:
/// </para>
/// <code>
/// // Base theme:
/// public sealed class LuminaTheme() : EmbeddedTheme(typeof(LuminaTheme).Assembly);
///
/// // Extension:
/// public sealed class LuminaStatisticsExtension() : EmbeddedTheme(typeof(LuminaStatisticsExtension).Assembly);
/// </code>
/// <para>
/// Whether a theme is an extension is determined by the manifest.json content
/// (presence of <c>targetTheme</c> and <c>prefix</c> fields).
/// </para>
/// </remarks>
public abstract class EmbeddedTheme : ITheme
{
    private readonly EmbeddedResourceProvider resources;
    private readonly Lazy<ThemeJsonConfig> config;
    private readonly Lazy<PackageMetadata> packageMetadata;
    private readonly Lazy<ThemeManifest> manifest;

    /// <summary>
    /// Creates a new embedded theme.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources.</param>
    protected EmbeddedTheme(Assembly assembly)
    {
        resources = new EmbeddedResourceProvider(assembly);
        config = new Lazy<ThemeJsonConfig>(resources.LoadManifest<ThemeJsonConfig>);
        packageMetadata = new Lazy<PackageMetadata>(() => CreateMetadata(config.Value));
        manifest = new Lazy<ThemeManifest>(() => CreateManifest(config.Value));
    }

    /// <inheritdoc />
    public PackageMetadata Metadata => packageMetadata.Value;

    /// <inheritdoc />
    public string? Prefix => config.Value.Prefix;

    /// <inheritdoc />
    public string? TargetTheme => config.Value.TargetTheme;

    /// <inheritdoc />
    public ThemeManifest Manifest => manifest.Value;

    /// <inheritdoc />
    public Stream? GetFile(string relativePath) =>
        resources.GetFile(relativePath);

    /// <inheritdoc />
    public IEnumerable<string> GetAllFiles() =>
        resources.GetAllFiles();

    /// <inheritdoc />
    public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
        resources.ExtractToAsync(targetDirectory, cancellationToken);

    /// <inheritdoc />
    public Stream? GetSiteTemplate() =>
        Prefix is null ? GetFile("Configuration/site.json") : null;

    /// <inheritdoc />
    public Stream? GetImagesTemplate() =>
        Prefix is null ? GetFile("Configuration/images.json") : null;

    /// <inheritdoc />
    private static readonly IReadOnlyDictionary<string, string> EmptyDefaults = new Dictionary<string, string>();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey)
    {
        var templateDefaults = config.Value.TemplateDefaults;
        if (templateDefaults is null)
        {
            return EmptyDefaults;
        }

        if (templateDefaults.TryGetValue(templateKey, out var templateConfig) && templateConfig.Data is not null)
        {
            return templateConfig.Data;
        }

        return EmptyDefaults;
    }

    private static PackageMetadata CreateMetadata(ThemeJsonConfig config)
    {
        var name = config.Name ?? throw new InvalidOperationException("Theme name is required in manifest.json");

        return new PackageMetadata
        {
            Id = $"Spectara.Revela.Themes.{name}",
            Name = name,
            Version = config.Version ?? "1.0.0",
            Description = config.Description ?? string.Empty,
            Author = config.Author ?? "Unknown",
            PreviewImageUri = config.PreviewImage,
            Tags = config.Tags ?? []
        };
    }

    private static ThemeManifest CreateManifest(ThemeJsonConfig config) => new()
    {
        LayoutTemplate = config.Templates?.Layout ?? "layout.revela",
        Variables = config.Variables ?? new Dictionary<string, string>()
    };
}
