using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Base class for theme plugins with embedded resources
/// </summary>
/// <remarks>
/// This base class handles all the boilerplate for theme plugins:
/// - Reads manifest.json from embedded resources for metadata and manifest
/// - Implements GetFile(), GetAllFiles(), ExtractToAsync()
///
/// Theme authors only need to create a minimal derived class:
/// <code>
/// public sealed class MyThemePlugin() : EmbeddedThemePlugin(typeof(MyThemePlugin).Assembly) { }
/// </code>
///
/// And provide a manifest.json file as embedded resource with:
/// - name, version, description, author (metadata)
/// - templates.layout, partials, assets, variables (manifest)
/// </remarks>
public abstract class EmbeddedThemePlugin : IThemePlugin
{
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Assembly assembly;
    private readonly string resourcePrefix;
    private readonly Lazy<ThemeJsonConfig> config;
    private readonly Lazy<EmbeddedThemeMetadata> metadata;
    private readonly Lazy<ThemeManifest> manifest;

    /// <summary>
    /// Creates a new embedded theme plugin
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources</param>
    protected EmbeddedThemePlugin(Assembly assembly)
    {
        this.assembly = assembly;

        // Determine resource prefix from assembly name
        // Resources are named: AssemblyName.Folder.File.ext
        resourcePrefix = assembly.GetName().Name + ".";

        config = new Lazy<ThemeJsonConfig>(LoadConfig);
        metadata = new Lazy<EmbeddedThemeMetadata>(() => CreateMetadata(config.Value));
        manifest = new Lazy<ThemeManifest>(() => CreateManifest(config.Value));
    }

    /// <inheritdoc />
    IPluginMetadata IPlugin.Metadata => Metadata;

    /// <inheritdoc />
    public IThemeMetadata Metadata => metadata.Value;

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Themes don't add configuration
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Themes don't register services
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services)
    {
        // Themes don't need initialization
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        // Themes don't provide commands
        yield break;
    }

    /// <inheritdoc />
    public ThemeManifest GetManifest() => manifest.Value;

    /// <inheritdoc />
    public Stream? GetFile(string relativePath)
    {
        // Normalize path separators - LogicalName preserves original separators (backslash on Windows)
        // Try both forward and backslash variants to handle cross-platform builds
        var forwardSlash = relativePath.Replace('\\', '/');
        var backSlash = relativePath.Replace('/', '\\');

        return assembly.GetManifestResourceStream(resourcePrefix + forwardSlash)
            ?? assembly.GetManifestResourceStream(forwardSlash)
            ?? assembly.GetManifestResourceStream(resourcePrefix + backSlash)
            ?? assembly.GetManifestResourceStream(backSlash);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllFiles()
    {
        // Get all resource names and return as relative paths
        // Resources might use dots (standard) or path separators (LogicalName with RecursiveDir)
        return assembly.GetManifestResourceNames()
            .Where(name => !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                // Strip prefix if present
                var relativeName = name.StartsWith(resourcePrefix, StringComparison.Ordinal)
                    ? name[resourcePrefix.Length..]
                    : name;

                // Check if name uses path separators (LogicalName with RecursiveDir)
                if (relativeName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || relativeName.Contains('/', StringComparison.Ordinal))
                {
                    // Already a path, just normalize separators
                    return relativeName.Replace('/', Path.DirectorySeparatorChar);
                }

                // Standard resource naming with dots - convert to path
                return ConvertResourceNameToPath(relativeName);
            });
    }

    /// <inheritdoc />
    public async Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default)
    {
        foreach (var relativePath in GetAllFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await using var source = GetFile(relativePath);
            if (source is null)
            {
                continue;
            }

            await using var target = File.Create(targetPath);
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Stream? GetSiteTemplate() =>
        // Load site.json from Configuration folder
        GetFile("Configuration/site.json");

    /// <inheritdoc />
    public Stream? GetImagesTemplate() =>
        // Load images.json from Configuration folder
        GetFile("Configuration/images.json");

    private ThemeJsonConfig LoadConfig()
    {
        // Try with prefix first (standard resource naming), then without (LogicalName override)
        var resourceNameWithPrefix = resourcePrefix + ManifestFileName;
        var stream = assembly.GetManifestResourceStream(resourceNameWithPrefix)
            ?? assembly.GetManifestResourceStream(ManifestFileName)
            ?? throw new InvalidOperationException(
                $"Theme plugin {assembly.GetName().Name} is missing embedded resource '{ManifestFileName}'");

        var config = JsonSerializer.Deserialize<ThemeJsonConfig>(stream, JsonOptions);

        return config ?? throw new InvalidOperationException(
            $"Failed to parse {ManifestFileName} in {assembly.GetName().Name}");
    }

    private static EmbeddedThemeMetadata CreateMetadata(ThemeJsonConfig config)
    {
        return new EmbeddedThemeMetadata
        {
            Name = config.Name ?? throw new InvalidOperationException("Theme name is required in manifest.json"),
            Version = config.Version ?? "1.0.0",
            Description = config.Description ?? string.Empty,
            Author = config.Author ?? "Unknown",
            PreviewImageUri = ParseUri(config.PreviewImageUrl),
            Tags = config.Tags ?? []
        };
    }

    private static ThemeManifest CreateManifest(ThemeJsonConfig config)
    {
        return new ThemeManifest
        {
            LayoutTemplate = config.Templates?.Layout ?? "layout.revela",
            Variables = config.Variables ?? []
        };
    }

    private static Uri? ParseUri(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return new Uri(url, UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Converts a resource name suffix to a file path
    /// </summary>
    /// <remarks>
    /// Resource names use dots as separators, but we need to determine
    /// which dots are folder separators vs file extension.
    /// Strategy: The last dot before a known extension is the extension separator.
    /// </remarks>
    private static string ConvertResourceNameToPath(string resourceSuffix)
    {
        // Find the file extension (last segment)
        var lastDot = resourceSuffix.LastIndexOf('.');
        if (lastDot < 0)
        {
            return resourceSuffix;
        }

        // Everything before last dot: replace dots with path separator
        var pathPart = resourceSuffix[..lastDot].Replace('.', Path.DirectorySeparatorChar);
        var extension = resourceSuffix[lastDot..];

        return pathPart + extension;
    }
}

/// <summary>
/// JSON configuration structure for theme.json
/// </summary>
/// <remarks>
/// Shared by both EmbeddedThemePlugin and LocalThemeAdapter
/// </remarks>
internal sealed class ThemeJsonConfig
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? PreviewImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public ThemeTemplatesConfig? Templates { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}

/// <summary>
/// Templates section in theme.json
/// </summary>
internal sealed class ThemeTemplatesConfig
{
    public string? Layout { get; set; }
}

/// <summary>
/// Metadata implementation for embedded themes
/// </summary>
internal sealed class EmbeddedThemeMetadata : IThemeMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public Uri? PreviewImageUri { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
