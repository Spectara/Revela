namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Result of running the full generation pipeline (scan → pages → images).
/// </summary>
public sealed record GenerateResult
{
    /// <summary>Whether the full pipeline succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Scan phase result.</summary>
    public ScanResult? Scan { get; init; }

    /// <summary>Pages phase result.</summary>
    public PagesResult? Pages { get; init; }

    /// <summary>Images phase result.</summary>
    public ImagesResult? Images { get; init; }

    /// <summary>Total pipeline duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
