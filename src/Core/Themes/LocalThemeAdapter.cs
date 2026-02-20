using System.Text.Json;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Themes;

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
    private readonly ThemeManifest manifest;

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
        var themeConfig = JsonSerializer.Deserialize<ThemeJsonConfig>(json, ThemeJsonConfig.JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse theme.json");

        Metadata = new ThemeMetadata
        {
            Name = themeConfig.Name ?? Path.GetFileName(themeDirectory),
            Version = themeConfig.Version ?? "1.0.0",
            Description = themeConfig.Description ?? "Local theme",
            Author = themeConfig.Author ?? "Unknown",
            PreviewImageUri = themeConfig.PreviewImage,
            Tags = themeConfig.Tags ?? []
        };

        manifest = new ThemeManifest
        {
            LayoutTemplate = themeConfig.Templates?.Layout ?? "layout.revela",
            Variables = themeConfig.Variables ?? new Dictionary<string, string>()
        };
    }



    /// <inheritdoc />
    PluginMetadata IPlugin.Metadata => Metadata;

    /// <inheritdoc />
    public ThemeMetadata Metadata { get; }

    /// <inheritdoc />
    public void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        // Local themes don't register services
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

    /// <inheritdoc />
    public Stream? GetSiteTemplate() =>
        // Load site.json from Configuration folder
        GetFile("Configuration/site.json");

    /// <inheritdoc />
    public Stream? GetImagesTemplate() =>
        // Load images.json from Configuration folder
        GetFile("Configuration/images.json");
}
