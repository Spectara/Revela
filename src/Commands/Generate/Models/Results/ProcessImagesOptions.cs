namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Options for image processing.
/// </summary>
internal sealed class ProcessImagesOptions
{
    /// <summary>Force rebuild all images (ignore cache).</summary>
    public bool Force { get; init; }
}
