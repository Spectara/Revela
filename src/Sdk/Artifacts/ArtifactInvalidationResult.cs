namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Describes whether derived artifacts were invalidated successfully.
/// </summary>
public sealed record ArtifactInvalidationResult
{
    /// <summary>Whether invalidation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message when invalidation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static ArtifactInvalidationResult Ok() => new() { Success = true };

    /// <summary>Creates a failed result.</summary>
    public static ArtifactInvalidationResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
