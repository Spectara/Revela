namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Encapsulates all command-line options for the OneDrive download command
/// </summary>
/// <remarks>
/// Using the Parameter Object pattern to avoid long parameter lists.
/// All properties use 'required init' to ensure complete initialization.
/// </remarks>
public sealed class ExecutionOptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "CLI option value is string by design, converted later if needed")]
    public required string? ShareUrl { get; init; }
    public required string? OutputDirectory { get; init; }
    public required bool ForceRefresh { get; init; }
    public required IReadOnlyList<string>? IncludePatterns { get; init; }
    public required IReadOnlyList<string>? ExcludePatterns { get; init; }
    public required int? Concurrency { get; init; }
    public required bool Debug { get; init; }
    public required bool DryRun { get; init; }
    public required bool Clean { get; init; }
    public required bool CleanAll { get; init; }
    public required bool ShowFiles { get; init; }
}
