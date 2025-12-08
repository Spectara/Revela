using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Commands.Generate.Models;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Template engine abstraction
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Set the theme for loading partials via include directive
    /// </summary>
    /// <param name="theme">Theme plugin to load partials from</param>
    void SetTheme(IThemePlugin? theme);

    /// <summary>
    /// Render template content with data model
    /// </summary>
    string Render(string templateContent, object model);

    /// <summary>
    /// Render template file with data model
    /// </summary>
    Task<string> RenderFileAsync(string templatePath, object model, CancellationToken cancellationToken = default);
}

/// <summary>
/// Image processor abstraction
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Process an image and generate all size variants
    /// </summary>
    /// <param name="inputPath">Path to source image</param>
    /// <param name="options">Processing options (sizes, formats, quality)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed image with all variants</returns>
    Task<Image> ProcessImageAsync(string inputPath, ImageProcessingOptions options, CancellationToken cancellationToken = default);
}
