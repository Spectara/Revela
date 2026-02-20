using System.Reflection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Base class for theme plugins with embedded resources.
/// </summary>
/// <remarks>
/// Handles all the boilerplate for theme plugins:
/// reads manifest.json from embedded resources, implements GetFile(), GetAllFiles(), ExtractToAsync().
///
/// Theme authors only need a minimal derived class:
/// <code>
/// public sealed class MyThemePlugin() : EmbeddedThemePlugin(typeof(MyThemePlugin).Assembly) { }
/// </code>
/// </remarks>
public abstract class EmbeddedThemePlugin : IThemePlugin
{
    private readonly EmbeddedResourceProvider resources;
    private readonly Lazy<ThemeJsonConfig> config;
    private readonly Lazy<ThemeMetadata> themeMetadata;
    private readonly Lazy<ThemeManifest> manifest;

    /// <summary>
    /// Creates a new embedded theme plugin.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources.</param>
    protected EmbeddedThemePlugin(Assembly assembly)
    {
        resources = new EmbeddedResourceProvider(assembly);
        config = new Lazy<ThemeJsonConfig>(resources.LoadManifest<ThemeJsonConfig>);
        themeMetadata = new Lazy<ThemeMetadata>(() => CreateMetadata(config.Value));
        manifest = new Lazy<ThemeManifest>(() => CreateManifest(config.Value));
    }

    /// <inheritdoc />
    PluginMetadata IPlugin.Metadata => Metadata;

    /// <inheritdoc />
    public ThemeMetadata Metadata => themeMetadata.Value;

    /// <inheritdoc />
    public void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        // Themes don't register services
    }

    /// <inheritdoc />
    public ThemeManifest GetManifest() => manifest.Value;

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
        GetFile("Configuration/site.json");

    /// <inheritdoc />
    public Stream? GetImagesTemplate() =>
        GetFile("Configuration/images.json");

    private static ThemeMetadata CreateMetadata(ThemeJsonConfig config) => new()
    {
        Name = config.Name ?? throw new InvalidOperationException("Theme name is required in manifest.json"),
        Version = config.Version ?? "1.0.0",
        Description = config.Description ?? string.Empty,
        Author = config.Author ?? "Unknown",
        PreviewImageUri = config.PreviewImage,
        Tags = config.Tags ?? []
    };

    private static ThemeManifest CreateManifest(ThemeJsonConfig config) => new()
    {
        LayoutTemplate = config.Templates?.Layout ?? "layout.revela",
        Variables = config.Variables ?? new Dictionary<string, string>()
    };
}
