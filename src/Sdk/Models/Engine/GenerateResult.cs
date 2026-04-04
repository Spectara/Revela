namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Result of running the full generation pipeline via <c>GenerateAllAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// When using <c>GenerateAllAsync</c>, only <see cref="Success"/>, <see cref="Duration"/>,
/// and <see cref="ErrorMessage"/> are populated. The per-phase properties (Scan, Pages, Images)
/// remain null because the pipeline includes plugin steps that don't map to those three phases.
/// </para>
/// <para>
/// For rich per-phase results, use the individual engine methods:
/// <c>ScanAsync</c>, <c>GeneratePagesAsync</c>, <c>GenerateImagesAsync</c>.
/// </para>
/// </remarks>
public sealed record GenerateResult
{
    /// <summary>Whether the full pipeline succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Scan phase result (only populated by individual ScanAsync calls, not GenerateAllAsync).</summary>
    public ScanResult? Scan { get; init; }

    /// <summary>Pages phase result (only populated by individual GeneratePagesAsync calls, not GenerateAllAsync).</summary>
    public PagesResult? Pages { get; init; }

    /// <summary>Images phase result (only populated by individual GenerateImagesAsync calls, not GenerateAllAsync).</summary>
    public ImagesResult? Images { get; init; }

    /// <summary>Total pipeline duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
