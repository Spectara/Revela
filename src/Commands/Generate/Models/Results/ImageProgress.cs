namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Progress during image processing.
/// </summary>
public sealed class ImageProgress
{
    /// <summary>Current image being processed.</summary>
    public required string CurrentImage { get; init; }

    /// <summary>Number of images processed so far.</summary>
    public int Processed { get; init; }

    /// <summary>Total number of images to process.</summary>
    public int Total { get; init; }

    /// <summary>Number of images skipped (cached).</summary>
    public int Skipped { get; init; }
}
