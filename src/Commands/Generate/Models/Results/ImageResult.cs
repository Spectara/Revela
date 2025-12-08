namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Result of image processing.
/// </summary>
public sealed class ImageResult
{
    /// <summary>Whether processing succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Number of images processed.</summary>
    public int ProcessedCount { get; init; }

    /// <summary>Number of images skipped (unchanged).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
