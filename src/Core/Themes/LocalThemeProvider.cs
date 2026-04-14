using System.Text.Json;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Themes;

namespace Spectara.Revela.Core.Themes;

/// <summary>
/// Adapts a local directory to the <see cref="ITheme"/> interface.
/// </summary>
/// <remarks>
/// Allows using themes from local folders (e.g., project/themes/my-theme/)
/// without requiring them to be packaged as NuGet themes.
/// First-class citizen — no special type checks needed.
/// </remarks>
public sealed class LocalThemeProvider : ITheme
{
    /// <summary>
    /// Get the theme directory path.
    /// </summary>
    public string ThemeDirectory { get; }

    /// <summary>
    /// Creates a new LocalThemeProvider for the specified directory.
    /// </summary>
    /// <param name="themeDirectory">Absolute path to the theme directory.</param>
    public LocalThemeProvider(string themeDirectory)
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

        var themeName = themeConfig.Name ?? Path.GetFileName(themeDirectory);
        Metadata = new PackageMetadata
        {
            Id = $"Spectara.Revela.Themes.{themeName}",
            Name = themeName,
            Version = themeConfig.Version ?? "1.0.0",
            Description = themeConfig.Description ?? "Local theme",
            Author = themeConfig.Author ?? "Unknown",
            PreviewImageUri = themeConfig.PreviewImage,
            Tags = themeConfig.Tags ?? []
        };

        Manifest = new ThemeManifest
        {
            LayoutTemplate = themeConfig.Templates?.Layout ?? "layout.revela",
            Variables = themeConfig.Variables ?? System.Collections.Frozen.FrozenDictionary<string, string>.Empty
        };

        // Local themes are always base themes (no prefix, no target)
        Prefix = null;
        TargetTheme = null;
    }

    /// <inheritdoc />
    public PackageMetadata Metadata { get; }

    /// <inheritdoc />
    public string? Prefix { get; }

    /// <inheritdoc />
    public string? TargetTheme { get; }

    /// <inheritdoc />
    public ThemeManifest Manifest { get; }

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
    public IEnumerable<string> GetAllFiles() =>
        Directory.EnumerateFiles(ThemeDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(ThemeDirectory, path))
            .Where(path => !path.Equals("theme.json", StringComparison.OrdinalIgnoreCase));

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
    public Stream? GetSiteTemplate() => GetFile("Configuration/site.json");

    /// <inheritdoc />
    public Stream? GetImagesTemplate() => GetFile("Configuration/images.json");
}
