using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Base class for theme extensions with embedded resources
/// </summary>
/// <remarks>
/// <para>
/// This base class handles all the boilerplate for theme extensions:
/// - Reads manifest.json from embedded resources for metadata and manifest
/// - Implements GetFile(), ExtractToAsync()
/// </para>
/// <para>
/// Extension authors only need to create a minimal derived class:
/// <code>
/// public sealed class LuminaStatisticsExtension()
///     : EmbeddedThemeExtension(typeof(LuminaStatisticsExtension).Assembly) { }
/// </code>
/// </para>
/// <para>
/// And provide a manifest.json file as embedded resource with:
/// - name, version, description, author (metadata)
/// - targetTheme (theme this extends, e.g., "Lumina")
/// - partialPrefix (prefix for templates, e.g., "statistics")
/// - variables (optional theme variables)
/// </para>
/// </remarks>
public abstract class EmbeddedThemeExtension : IThemeExtension
{
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Assembly assembly;
    private readonly string resourcePrefix;
    private readonly Lazy<ExtensionJsonConfig> config;
    private readonly Lazy<EmbeddedExtensionMetadata> metadata;

    /// <summary>
    /// Creates a new embedded theme extension
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources</param>
    protected EmbeddedThemeExtension(Assembly assembly)
    {
        this.assembly = assembly;

        // Determine resource prefix from assembly name
        resourcePrefix = assembly.GetName().Name + ".";

        config = new Lazy<ExtensionJsonConfig>(LoadConfig);
        metadata = new Lazy<EmbeddedExtensionMetadata>(() => CreateMetadata(config.Value));
    }

    /// <inheritdoc />
    public IPluginMetadata Metadata => metadata.Value;

    /// <inheritdoc />
    public string TargetTheme => config.Value.TargetTheme ?? string.Empty;

    /// <inheritdoc />
    public string PartialPrefix => config.Value.PartialPrefix ?? string.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Variables =>
        config.Value.Variables ?? [];

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
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Extensions don't add configuration
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Extensions don't register services
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services)
    {
        // Extensions don't need initialization
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        // Extensions don't provide commands
        yield break;
    }

    /// <inheritdoc />
    public Stream? GetFile(string relativePath)
    {
        // MSBuild always normalizes embedded resource names to forward slashes
        // Try with prefix first, then without
        return assembly.GetManifestResourceStream(resourcePrefix + relativePath)
            ?? assembly.GetManifestResourceStream(relativePath);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllFiles()
    {
        // Get all resource names from the assembly
        return assembly.GetManifestResourceNames()
            .Where(name => !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                // Strip prefix if present
                var relativeName = name.StartsWith(resourcePrefix, StringComparison.Ordinal)
                    ? name[resourcePrefix.Length..]
                    : name;

                return relativeName;
            });
    }

    /// <inheritdoc />
    public async Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);

        // Extract manifest.json
        var configStream = GetConfigStream();
        if (configStream is not null)
        {
            await using (configStream)
            {
                var targetPath = Path.Combine(targetDirectory, ManifestFileName);
                await using var fileStream = File.Create(targetPath);
                await configStream.CopyToAsync(fileStream, cancellationToken);
            }
        }

        // Extract all files from the extension
        foreach (var file in GetAllFiles())
        {
            await ExtractResourceAsync(file, targetDirectory, cancellationToken);
        }
    }

    private async Task ExtractResourceAsync(
        string relativePath,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        using var stream = GetFile(relativePath);
        if (stream is null)
        {
            return;
        }

        var targetPath = Path.Combine(targetDirectory, relativePath);
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        await using var fileStream = File.Create(targetPath);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    private Stream? GetConfigStream()
    {
        // Try various patterns for manifest.json
        return assembly.GetManifestResourceStream(resourcePrefix + ManifestFileName)
            ?? assembly.GetManifestResourceStream(ManifestFileName);
    }

    private ExtensionJsonConfig LoadConfig()
    {
        using var stream = GetConfigStream()
            ?? throw new InvalidOperationException(
                $"Extension {assembly.GetName().Name} must have embedded {ManifestFileName}");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<ExtensionJsonConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize {ManifestFileName} in {assembly.GetName().Name}");
    }

    private static EmbeddedExtensionMetadata CreateMetadata(ExtensionJsonConfig cfg) => new()
    {
        Name = cfg.Name ?? "Unknown",
        Version = cfg.Version ?? "1.0.0",
        Description = cfg.Description ?? "",
        Author = cfg.Author ?? ""
    };

    /// <summary>
    /// Configuration model for manifest.json
    /// </summary>
    private sealed class ExtensionJsonConfig
    {
        public string? Name { get; init; }
        public string? Version { get; init; }
        public string? Description { get; init; }
        public string? Author { get; init; }
        public string? TargetTheme { get; init; }
        public string? PartialPrefix { get; init; }
        public Dictionary<string, string>? Variables { get; init; }
        public Dictionary<string, TemplateConfig>? Templates { get; init; }
    }

    /// <summary>
    /// Template configuration with default data sources
    /// </summary>
    private sealed class TemplateConfig
    {
        /// <summary>
        /// Default data sources for this template.
        /// Key = variable name, Value = default filename.
        /// </summary>
        public Dictionary<string, string>? Data { get; init; }
    }
}

/// <summary>
/// Metadata implementation for embedded theme extensions
/// </summary>
internal sealed class EmbeddedExtensionMetadata : IPluginMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
}
