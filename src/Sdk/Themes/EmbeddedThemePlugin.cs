using System.Reflection;
using System.Text.Json;

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
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Assembly assembly;
    private readonly string resourcePrefix;
    private readonly Lazy<ThemeJsonConfig> config;
    private readonly Lazy<ThemeMetadata> themeMetadata;
    private readonly Lazy<ThemeManifest> manifest;

    /// <summary>
    /// Creates a new embedded theme plugin.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources.</param>
    protected EmbeddedThemePlugin(Assembly assembly)
    {
        this.assembly = assembly;

        // Determine resource prefix from assembly name
        // Resources are named: AssemblyName.Folder.File.ext
        resourcePrefix = assembly.GetName().Name + ".";

        config = new Lazy<ThemeJsonConfig>(LoadConfig);
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
        GetFile("Configuration/site.json");

    /// <inheritdoc />
    public Stream? GetImagesTemplate() =>
        GetFile("Configuration/images.json");

    private ThemeJsonConfig LoadConfig()
    {
        var resourceNameWithPrefix = resourcePrefix + ManifestFileName;
        var stream = assembly.GetManifestResourceStream(resourceNameWithPrefix)
            ?? assembly.GetManifestResourceStream(ManifestFileName)
            ?? throw new InvalidOperationException(
                $"Theme plugin {assembly.GetName().Name} is missing embedded resource '{ManifestFileName}'");

        var config = JsonSerializer.Deserialize<ThemeJsonConfig>(stream, JsonOptions);

        return config ?? throw new InvalidOperationException(
            $"Failed to parse {ManifestFileName} in {assembly.GetName().Name}");
    }

    private static ThemeMetadata CreateMetadata(ThemeJsonConfig config) => new()
    {
        Name = config.Name ?? throw new InvalidOperationException("Theme name is required in manifest.json"),
        Version = config.Version ?? "1.0.0",
        Description = config.Description ?? string.Empty,
        Author = config.Author ?? "Unknown",
        PreviewImageUri = ParseUri(config.PreviewImageUrl),
        Tags = config.Tags ?? []
    };

    private static ThemeManifest CreateManifest(ThemeJsonConfig config) => new()
    {
        LayoutTemplate = config.Templates?.Layout ?? "layout.revela",
        Variables = config.Variables ?? []
    };

    private static Uri? ParseUri(string? url) =>
        string.IsNullOrEmpty(url) ? null : new Uri(url, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Converts a resource name suffix to a file path.
    /// </summary>
    private static string ConvertResourceNameToPath(string resourceSuffix)
    {
        var lastDot = resourceSuffix.LastIndexOf('.');
        if (lastDot < 0)
        {
            return resourceSuffix;
        }

        var pathPart = resourceSuffix[..lastDot].Replace('.', Path.DirectorySeparatorChar);
        var extension = resourceSuffix[lastDot..];

        return pathPart + extension;
    }
}

/// <summary>
/// JSON configuration structure for theme.json.
/// </summary>
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
/// Templates section in theme.json.
/// </summary>
internal sealed class ThemeTemplatesConfig
{
    public string? Layout { get; set; }
}
