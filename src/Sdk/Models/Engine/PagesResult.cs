namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Result of page rendering.
/// </summary>
public sealed record PagesResult
{
    /// <summary>Whether rendering succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Number of pages rendered.</summary>
    public int PageCount { get; init; }

    /// <summary>Rendering duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
