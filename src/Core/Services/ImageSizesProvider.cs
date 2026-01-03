using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Provides image sizes from theme configuration.
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
/// Resolves image sizes from the resolved theme's GetImagesTemplate().
/// </summary>
/// <remarks>
/// <para>
/// The theme is already resolved by <see cref="IThemeResolver"/> with priority:
/// </para>
/// <list type="number">
///   <item>Local theme folder (project/themes/{name}/) - via LocalThemeAdapter</item>
///   <item>Installed theme plugins</item>
///   <item>Default bundled theme</item>
/// </list>
/// <para>
/// Local themes read their images.json from themes/{name}/Configuration/images.json automatically.
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

        // Load from resolved theme (local or installed)
        var themeSizes = TryLoadThemeSizes();
        if (themeSizes is { Count: > 0 })
        {
            cachedSizes = themeSizes;
            return cachedSizes;
        }

        // No sizes found
        throw new InvalidOperationException(
            $"No image sizes configured. Theme '{themeConfig.CurrentValue.Name}' must provide " +
            $"Configuration/images.json with a 'sizes' array.");
    }

    /// <summary>
    /// Load sizes from resolved theme's GetImagesTemplate().
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
