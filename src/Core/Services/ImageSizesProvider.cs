using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;

using Spectara.Revela.Sdk.Services;
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
/// The theme is already resolved by <see cref="IThemeRegistry"/> with priority:
/// </para>
/// <list type="number">
///   <item>Local theme folder (project/themes/{name}/) - via LocalThemeProvider</item>
///   <item>Installed theme plugins</item>
///   <item>Default bundled theme</item>
/// </list>
/// <para>
/// Local themes read their images.json from themes/{name}/Configuration/images.json automatically.
/// </para>
/// </remarks>
public sealed partial class ImageSizesProvider(
    IOptionsMonitor<ThemeConfig> themeConfig,
    IOptions<Sdk.ProjectEnvironment> projectEnvironment,
    IThemeRegistry themeRegistry,
    ILogger<ImageSizesProvider> logger) : IImageSizesProvider
{
    private ReadOnlyCollection<int>? cachedSizes;
    private string? cachedResizeMode;
    private string? cachedThemeName;

    /// <inheritdoc />
    public IReadOnlyList<int> GetSizes()
    {
        InvalidateCacheIfThemeChanged();

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
            LogSizesLoaded(cachedSizes.Count);
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
        InvalidateCacheIfThemeChanged();

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
    /// Invalidates cached values when the theme name changes (hot-reload support).
    /// </summary>
    private void InvalidateCacheIfThemeChanged()
    {
        var currentThemeName = themeConfig.CurrentValue.Name;
        if (cachedThemeName is not null && !string.Equals(cachedThemeName, currentThemeName, StringComparison.OrdinalIgnoreCase))
        {
            LogThemeChanged(cachedThemeName, currentThemeName ?? "default");
            cachedSizes = null;
            cachedResizeMode = null;
        }

        cachedThemeName = currentThemeName;
    }

    /// <summary>
    /// Load settings from resolved theme's GetImagesTemplate().
    /// </summary>
    private (ReadOnlyCollection<int>? Sizes, string? ResizeMode) TryLoadThemeSettings()
    {
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            LogNoThemeConfigured();
            return (null, null);
        }

        var projectPath = projectEnvironment.Value.Path;
        var theme = themeRegistry.Resolve(themeName, projectPath);
        if (theme is null)
        {
            LogThemeNotFound(themeName);
            return (null, null);
        }

        using var stream = theme.GetImagesTemplate();
        if (stream is null)
        {
            LogNoImagesTemplate(themeName);
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
                List<int> sizesList = [];
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
        catch (JsonException ex)
        {
            LogInvalidImagesJson(themeName, ex.Message);
        }

        return (null, null);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} image sizes from theme configuration")]
    private partial void LogSizesLoaded(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme changed from '{OldTheme}' to '{NewTheme}', invalidating image sizes cache")]
    private partial void LogThemeChanged(string oldTheme, string newTheme);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No theme configured for image sizes")]
    private partial void LogNoThemeConfigured();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Theme '{ThemeName}' not found for image sizes resolution")]
    private partial void LogThemeNotFound(string themeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Theme '{ThemeName}' does not provide images.json configuration")]
    private partial void LogNoImagesTemplate(string themeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid images.json in theme '{ThemeName}': {Error}")]
    private partial void LogInvalidImagesJson(string themeName, string error);
}


