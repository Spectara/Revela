namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Result of complete generation.
/// </summary>
internal sealed class GenerationResult
{
    /// <summary>Whether generation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Scan phase result.</summary>
    public ContentResult? ContentResult { get; init; }

    /// <summary>Image processing phase result.</summary>
    public ImageResult? ImageResult { get; init; }

    /// <summary>Render phase result.</summary>
    public RenderResult? RenderResult { get; init; }

    /// <summary>Total generation time.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
