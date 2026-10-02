namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Outcome of a UI-free operation: a pipeline step (<see cref="IPipelineStep"/>)
/// or an artifact invalidation (<see cref="Artifacts.IArtifactInvalidator"/>).
/// </summary>
/// <remarks>
/// One type for both lets a step return a failed invalidation unchanged instead
/// of translating between equivalent result records.
/// </remarks>
public sealed record OperationResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message when the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static OperationResult Ok() => new() { Success = true };

    /// <summary>Creates a failed result with an error message.</summary>
    public static OperationResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
