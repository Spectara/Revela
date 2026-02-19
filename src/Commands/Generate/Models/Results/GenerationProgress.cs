namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Progress during complete generation.
/// </summary>
internal sealed class GenerationProgress
{
    /// <summary>Current phase (Scan, Images, Pages).</summary>
    public required GenerationPhase Phase { get; init; }

    /// <summary>Current status message.</summary>
    public required string Status { get; init; }

    /// <summary>Overall progress percentage (0-100).</summary>
    public int OverallPercent { get; init; }
}

/// <summary>
/// Generation phases.
/// </summary>
internal enum GenerationPhase
{
    /// <summary>Scanning content.</summary>
    Scan,

    /// <summary>Processing images.</summary>
    Images,

    /// <summary>Rendering pages.</summary>
    Pages,

    /// <summary>Copying assets.</summary>
    Assets,

    /// <summary>Generation complete.</summary>
    Complete
}
