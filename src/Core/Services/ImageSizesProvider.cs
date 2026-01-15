using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Provides image sizes and resize mode from theme configuration.
/// </summary>
public interface IImageSizesProvider
{
    /// <summary>
    /// Gets the image sizes to generate for responsive images.
    /// </summary>
    /// <returns>List of sizes in pixels (interpretation depends on ResizeMode).</returns>
    /// <exception cref="InvalidOperationException">When no sizes are configured and theme is not found.</exception>
    IReadOnlyList<int> GetSizes();

    /// <summary>
    /// Gets the resize mode that determines how sizes are applied.
    /// </summary>
    /// <returns>Resize mode: "longest" (default), "width", or "height".</returns>
    string GetResizeMode();
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
    private string? cachedResizeMode;

    /// <inheritdoc />
    public IReadOnlyList<int> GetSizes()
    {
        // Return cached value if available
        if (cachedSizes is not null)
        {
            return cachedSizes;
        }

        // Load from resolved theme (local or installed)
        var (themeSizes, _) = TryLoadThemeSettings();
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

    /// <inheritdoc />
    public string GetResizeMode()
    {
        // Return cached value if available
        if (cachedResizeMode is not null)
        {
            return cachedResizeMode;
        }

        // Load from resolved theme
        var (_, resizeMode) = TryLoadThemeSettings();
        cachedResizeMode = resizeMode ?? "longest";
        return cachedResizeMode;
    }

    /// <summary>
    /// Load settings from resolved theme's GetImagesTemplate().
    /// </summary>
    private (ReadOnlyCollection<int>? Sizes, string? ResizeMode) TryLoadThemeSettings()
    {
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            return (null, null);
        }

        var projectPath = projectEnvironment.Value.Path;
        var theme = themeResolver.Resolve(themeName, projectPath);
        if (theme is null)
        {
            return (null, null);
        }

        using var stream = theme.GetImagesTemplate();
        if (stream is null)
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(stream);

            // images.json format: { "sizes": [...], "resizeMode": "longest" }
            ReadOnlyCollection<int>? sizes = null;
            string? resizeMode = null;

            if (doc.RootElement.TryGetProperty("sizes", out var sizesElement) &&
                sizesElement.ValueKind == JsonValueKind.Array)
            {
                var sizesList = new List<int>();
                foreach (var size in sizesElement.EnumerateArray())
                {
                    if (size.TryGetInt32(out var value))
                    {
                        sizesList.Add(value);
                    }
                }
                sizes = sizesList.AsReadOnly();
            }

            if (doc.RootElement.TryGetProperty("resizeMode", out var resizeModeElement) &&
                resizeModeElement.ValueKind == JsonValueKind.String)
            {
                resizeMode = resizeModeElement.GetString();
            }

            // Cache both values
            cachedSizes = sizes;
            cachedResizeMode = resizeMode ?? "longest";

            return (sizes, resizeMode);
        }
        catch (JsonException)
        {
            // Invalid JSON in theme
        }

        return (null, null);
    }
}
