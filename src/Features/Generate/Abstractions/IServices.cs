using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Template engine abstraction
/// </summary>
public interface ITemplateEngine
{
    string Render(string templateContent, object model);
    Task<string> RenderFileAsync(string templatePath, object model, CancellationToken cancellationToken = default);
}

/// <summary>
/// Image processor abstraction
/// </summary>
public interface IImageProcessor
{
    Task<Image> ProcessImageAsync(string inputPath, ImageProcessingOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Image processing options
/// </summary>
public sealed class ImageProcessingOptions
{
    public required int Quality { get; init; }
    public required IReadOnlyList<string> Formats { get; init; }
    public required IReadOnlyList<int> Sizes { get; init; }
    public required string OutputDirectory { get; init; }
    public string? CacheDirectory { get; init; }
}
