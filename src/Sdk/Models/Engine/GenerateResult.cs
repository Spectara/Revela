namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Result of running the full generation pipeline via <c>GenerateAllAsync</c>.
/// </summary>
/// <remarks>
/// All properties are populated: <see cref="Success"/>, <see cref="Duration"/>,
/// and the per-phase results (<see cref="Scan"/>, <see cref="Pages"/>, <see cref="Images"/>).
/// A phase result is null only if the pipeline stopped before reaching that phase.
/// </remarks>
public sealed record GenerateResult
{
    /// <summary>Whether the full pipeline succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Scan phase result (null if pipeline failed before this phase).</summary>
    public ScanResult? Scan { get; init; }

    /// <summary>Pages phase result (null if pipeline failed before this phase).</summary>
    public PagesResult? Pages { get; init; }

    /// <summary>Images phase result (null if pipeline failed before this phase).</summary>
    public ImagesResult? Images { get; init; }

    /// <summary>Total pipeline duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
