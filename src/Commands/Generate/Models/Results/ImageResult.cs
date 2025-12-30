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

    /// <summary>Number of files created (multiple per image: sizes × formats).</summary>
    public int FilesCreated { get; init; }

    /// <summary>Total size of created files in bytes.</summary>
    public long TotalSize { get; init; }

    /// <summary>Processing duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Warnings collected during processing (e.g., from libvips).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
