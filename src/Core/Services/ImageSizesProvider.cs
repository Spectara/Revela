using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Provides image sizes from theme configuration or theme defaults.
/// </summary>
public interface IImageSizesProvider
{
    /// <summary>
    /// Gets the image sizes to generate for responsive images.
    /// </summary>
    /// <returns>List of widths in pixels.</returns>
    /// <exception cref="InvalidOperationException">When no sizes are configured and theme is not found.</exception>
    IReadOnlyList<int> GetSizes();
}

/// <summary>
/// Resolves image sizes from theme/images.json or theme's GetImagesTemplate().
/// </summary>
/// <remarks>
/// <para>
/// Resolution order:
/// </para>
/// <list type="number">
///   <item>theme/images.json (if exists in project directory)</item>
///   <item>Theme's GetImagesTemplate() (embedded in theme)</item>
/// </list>
/// <para>
/// This approach avoids IConfiguration array merging issues by using
/// explicit either-or logic instead of configuration layering.
/// </para>
/// </remarks>
public sealed class ImageSizesProvider(
    IOptionsMonitor<ThemeConfig> themeConfig,
    IOptions<Sdk.ProjectEnvironment> projectEnvironment,
    IThemeResolver themeResolver) : IImageSizesProvider
{
    private IReadOnlyList<int>? cachedSizes;

    /// <inheritdoc />
    public IReadOnlyList<int> GetSizes()
    {
        // Return cached value if available
        if (cachedSizes is not null)
        {
            return cachedSizes;
        }

        // 1. Try theme/images.json (local override)
        var localSizes = TryLoadLocalSizes();
        if (localSizes is { Count: > 0 })
        {
            cachedSizes = localSizes;
            return cachedSizes;
        }

        // 2. Try theme's GetImagesTemplate()
        var themeSizes = TryLoadThemeSizes();
        if (themeSizes is { Count: > 0 })
        {
            cachedSizes = themeSizes;
            return cachedSizes;
        }

        // 3. No sizes found
        throw new InvalidOperationException(
            $"No image sizes configured. Either create theme/images.json with sizes, " +
            $"or ensure theme '{themeConfig.CurrentValue.Name}' provides GetImagesTemplate().");
    }

    /// <summary>
    /// Try to load sizes from local theme/configuration/images.json file.
    /// </summary>
    private ReadOnlyCollection<int>? TryLoadLocalSizes()
    {
        var projectPath = projectEnvironment.Value.Path;
        var imagesJsonPath = Path.Combine(projectPath, "theme", "configuration", "images.json");

        if (!File.Exists(imagesJsonPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(imagesJsonPath);
            using var doc = JsonDocument.Parse(json);

            // Format matches theme's Configuration/images.json: { "sizes": [...] }
            if (doc.RootElement.TryGetProperty("sizes", out var sizesElement) &&
                sizesElement.ValueKind == JsonValueKind.Array)
            {
                var sizes = new List<int>();
                foreach (var size in sizesElement.EnumerateArray())
                {
                    if (size.TryGetInt32(out var width))
                    {
                        sizes.Add(width);
                    }
                }
                return sizes.AsReadOnly();
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, fall through to theme defaults
        }

        return null;
    }

    /// <summary>
    /// Try to load sizes from theme's GetImagesTemplate().
    /// </summary>
    private ReadOnlyCollection<int>? TryLoadThemeSizes()
    {
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            return null;
        }

        var projectPath = projectEnvironment.Value.Path;
        var theme = themeResolver.Resolve(themeName, projectPath);
        if (theme is null)
        {
            return null;
        }

        using var stream = theme.GetImagesTemplate();
        if (stream is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(stream);

            // images.json format: { "sizes": [...], "formats": {...} }
            if (doc.RootElement.TryGetProperty("sizes", out var sizesElement) &&
                sizesElement.ValueKind == JsonValueKind.Array)
            {
                var sizes = new List<int>();
                foreach (var size in sizesElement.EnumerateArray())
                {
                    if (size.TryGetInt32(out var width))
                    {
                        sizes.Add(width);
                    }
                }
                return sizes.AsReadOnly();
            }
        }
        catch (JsonException)
        {
            // Invalid JSON in theme
        }

        return null;
    }
}
