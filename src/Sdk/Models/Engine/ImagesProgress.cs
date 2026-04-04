namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Progress during image processing.
/// </summary>
public sealed record ImagesProgress
{
    /// <summary>Number of images processed so far.</summary>
    public int Processed { get; init; }

    /// <summary>Total number of images to process.</summary>
    public int Total { get; init; }

    /// <summary>Number of images skipped (cached).</summary>
    public int Skipped { get; init; }
}
