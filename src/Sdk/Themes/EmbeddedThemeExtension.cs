using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Base class for theme extensions with embedded resources.
/// </summary>
/// <remarks>
/// Handles all the boilerplate for theme extensions:
/// reads manifest.json, implements GetFile(), ExtractToAsync().
///
/// Extension authors only need a minimal derived class:
/// <code>
/// public sealed class LuminaStatisticsExtension()
///     : EmbeddedThemeExtension(typeof(LuminaStatisticsExtension).Assembly) { }
/// </code>
/// </remarks>
public abstract class EmbeddedThemeExtension : IThemeExtension
{
    private readonly EmbeddedResourceProvider resources;
    private readonly Lazy<ExtensionJsonConfig> config;
    private readonly Lazy<PluginMetadata> pluginMetadata;

    /// <summary>
    /// Creates a new embedded theme extension.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources.</param>
    protected EmbeddedThemeExtension(Assembly assembly)
    {
        resources = new EmbeddedResourceProvider(assembly);
        config = new Lazy<ExtensionJsonConfig>(resources.LoadManifest<ExtensionJsonConfig>);
        pluginMetadata = new Lazy<PluginMetadata>(() => CreateMetadata(config.Value));
    }

    /// <inheritdoc />
    public PluginMetadata Metadata => pluginMetadata.Value;

    /// <inheritdoc />
    public string TargetTheme => config.Value.TargetTheme ?? string.Empty;

    /// <inheritdoc />
    public string PartialPrefix => config.Value.PartialPrefix ?? string.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Variables =>
        config.Value.Variables ?? new Dictionary<string, string>();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey)
    {
        var templates = config.Value.Templates;
        if (templates is null)
        {
            return new Dictionary<string, string>();
        }

        if (templates.TryGetValue(templateKey, out var templateConfig) && templateConfig.Data is not null)
        {
            return templateConfig.Data;
        }

        return new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Extensions don't register services
    }

    /// <inheritdoc />
    public Stream? GetFile(string relativePath) =>
        resources.GetFile(relativePath);

    /// <inheritdoc />
    public IEnumerable<string> GetAllFiles() =>
        resources.GetAllFiles();

    /// <inheritdoc />
    public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
        resources.ExtractToAsync(targetDirectory, cancellationToken);

    private static PluginMetadata CreateMetadata(ExtensionJsonConfig cfg) => new()
    {
        Name = cfg.Name ?? "Unknown",
        Version = cfg.Version ?? "1.0.0",
        Description = cfg.Description ?? "",
        Author = cfg.Author ?? ""
    };

    /// <summary>
    /// Configuration model for extension manifest.json.
    /// </summary>
    internal sealed class ExtensionJsonConfig
    {
        public string? Name { get; init; }
        public string? Version { get; init; }
        public string? Description { get; init; }
        public string? Author { get; init; }
        public string? TargetTheme { get; init; }
        public string? PartialPrefix { get; init; }
        public IReadOnlyDictionary<string, string>? Variables { get; init; }
        public IReadOnlyDictionary<string, TemplateConfig>? Templates { get; init; }
    }

    /// <summary>
    /// Template configuration with default data sources.
    /// </summary>
    internal sealed class TemplateConfig
    {
        public IReadOnlyDictionary<string, string>? Data { get; init; }
    }
}
