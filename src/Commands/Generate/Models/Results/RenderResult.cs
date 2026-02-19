namespace Spectara.Revela.Commands.Generate.Models.Results;

/// <summary>
/// Result of page rendering.
/// </summary>
internal sealed class RenderResult
{
    /// <summary>Whether rendering succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Number of pages rendered.</summary>
    public int PageCount { get; init; }

    /// <summary>Rendering duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; init; }
}
