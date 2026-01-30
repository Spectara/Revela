namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Progress during image processing.
/// </summary>
public sealed class ImageProgress
{
    /// <summary>Number of images processed so far.</summary>
    public int Processed { get; init; }

    /// <summary>Total number of images to process.</summary>
    public int Total { get; init; }

    /// <summary>Number of images skipped (cached).</summary>
    public int Skipped { get; init; }

    /// <summary>Configured output formats (for legend display).</summary>
    public IReadOnlyList<string> Formats { get; init; } = [];

    /// <summary>Active workers with their current state.</summary>
    public IReadOnlyList<WorkerState> Workers { get; init; } = [];
}

/// <summary>
/// State of a single worker processing an image.
/// </summary>
public sealed class WorkerState
{
    /// <summary>Worker index (0-based).</summary>
    public int WorkerId { get; init; }

    /// <summary>Image filename being processed (null if idle).</summary>
    public string? ImageName { get; init; }

    /// <summary>Total number of variants to generate (sizes × formats).</summary>
    public int VariantsTotal { get; init; }

    /// <summary>Variants completed so far (■).</summary>
    public int VariantsDone { get; init; }

    /// <summary>Variants skipped from cache (»).</summary>
    public int VariantsSkipped { get; init; }

    /// <summary>
    /// Ordered list of variant results (Done/Skipped) in processing order.
    /// Used for accurate progress display with interleaved symbols.
    /// </summary>
    public IReadOnlyList<VariantResult> VariantResults { get; init; } = [];

    /// <summary>Whether this worker is idle (no image assigned).</summary>
    public bool IsIdle => ImageName is null;

    /// <summary>Whether this image is complete.</summary>
    public bool IsComplete => !IsIdle && (VariantsDone + VariantsSkipped) >= VariantsTotal;
}

/// <summary>
/// Result of processing a single variant.
/// </summary>
public enum VariantResult
{
    /// <summary>Variant was generated (new file) - JPG format.</summary>
    DoneJpg,

    /// <summary>Variant was generated (new file) - WebP format.</summary>
    DoneWebp,

    /// <summary>Variant was generated (new file) - AVIF format.</summary>
    DoneAvif,

    /// <summary>Variant was generated (new file) - PNG format.</summary>
    DonePng,

    /// <summary>Variant was generated (new file) - other format.</summary>
    DoneOther,

    /// <summary>Variant was skipped (already exists).</summary>
    Skipped
}
