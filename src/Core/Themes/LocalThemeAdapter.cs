using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core.Themes;

/// <summary>
/// Adapts a local directory to the IThemePlugin interface
/// </summary>
/// <remarks>
/// Allows using themes from local folders (e.g., project/themes/my-theme/)
/// without requiring them to be packaged as NuGet plugins.
///
/// Structure expected:
/// <code>
/// themes/my-theme/
/// ├── theme.json       # Required: theme manifest
/// ├── Layout.revela    # Main layout template
/// ├── Assets/          # CSS, JS, fonts, images (auto-scanned)
/// ├── Body/            # Body templates (Gallery.revela, Page.revela)
/// └── Partials/        # Partial templates (Navigation.revela, Image.revela)
/// </code>
/// </remarks>
public sealed class LocalThemeAdapter : IThemePlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ThemeManifest manifest;
    private readonly LocalThemeMetadata metadata;

    /// <summary>
    /// Get the theme directory path
    /// </summary>
    public string ThemeDirectory { get; }

    /// <summary>
    /// Creates a new LocalThemeAdapter for the specified directory
    /// </summary>
    /// <param name="themeDirectory">Absolute path to the theme directory</param>
    public LocalThemeAdapter(string themeDirectory)
    {
        ThemeDirectory = themeDirectory;

        if (!Directory.Exists(themeDirectory))
        {
            throw new DirectoryNotFoundException($"Theme directory not found: {themeDirectory}");
        }

        var themeJsonPath = Path.Combine(themeDirectory, "theme.json");
        if (!File.Exists(themeJsonPath))
        {
            throw new FileNotFoundException("theme.json not found in theme directory", themeJsonPath);
        }

        // Parse theme.json
        var json = File.ReadAllText(themeJsonPath);
        var themeConfig = JsonSerializer.Deserialize<ThemeJsonConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse theme.json");

        metadata = new LocalThemeMetadata
        {
            Name = themeConfig.Name ?? Path.GetFileName(themeDirectory),
            Version = themeConfig.Version ?? "1.0.0",
            Description = themeConfig.Description ?? "Local theme",
            Author = themeConfig.Author ?? "Unknown",
            PreviewImageUri = ParsePreviewUri(themeConfig.PreviewImageUrl),
            Tags = themeConfig.Tags ?? []
        };

        manifest = new ThemeManifest
        {
            LayoutTemplate = themeConfig.Templates?.Layout ?? "layout.revela",

            Variables = themeConfig.Variables?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value) ?? []
        };
    }

    private static Uri? ParsePreviewUri(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return new Uri(url, UriKind.RelativeOrAbsolute);
    }

    /// <inheritdoc />
    IPluginMetadata IPlugin.Metadata => Metadata;

    /// <inheritdoc />
    public IThemeMetadata Metadata => metadata;

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Local themes don't add configuration
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Local themes don't register services
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services)
    {
        // Local themes don't need initialization
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        // Themes don't provide commands
        yield break;
    }

    /// <inheritdoc />
    public ThemeManifest GetManifest() => manifest;

    /// <inheritdoc />
    public Stream? GetFile(string relativePath)
    {
        var fullPath = Path.Combine(ThemeDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return File.OpenRead(fullPath);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllFiles()
    {
        return Directory.EnumerateFiles(ThemeDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(ThemeDirectory, path))
            .Where(path => !path.Equals("theme.json", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default)
    {
        foreach (var relativePath in GetAllFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = Path.Combine(ThemeDirectory, relativePath);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await using var source = File.OpenRead(sourcePath);
            await using var target = File.Create(targetPath);
            await source.CopyToAsync(target, cancellationToken);
        }
    }
}

/// <summary>
/// Metadata for local themes
/// </summary>
internal sealed class LocalThemeMetadata : IThemeMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public Uri? PreviewImageUri { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
