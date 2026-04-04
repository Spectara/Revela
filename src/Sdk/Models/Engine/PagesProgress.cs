namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Progress during page rendering.
/// </summary>
public sealed record PagesProgress
{
    /// <summary>Current page being rendered.</summary>
    public required string CurrentPage { get; init; }

    /// <summary>Number of pages rendered so far.</summary>
    public int Rendered { get; init; }

    /// <summary>Total number of pages to render.</summary>
    public int Total { get; init; }
}
