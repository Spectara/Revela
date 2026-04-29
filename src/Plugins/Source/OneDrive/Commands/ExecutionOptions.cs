namespace Spectara.Revela.Plugins.Source.OneDrive.Commands;

/// <summary>
/// Encapsulates all command-line options for the OneDrive download command
/// </summary>
/// <remarks>
/// Using the Parameter Object pattern to avoid long parameter lists.
/// All properties use 'required init' to ensure complete initialization.
/// </remarks>
internal sealed class ExecutionOptions
{
    public required string? ShareUrl { get; init; }
    public required bool ForceRefresh { get; init; }
    public required bool DryRun { get; init; }
    public required bool Clean { get; init; }
    public required bool CleanAll { get; init; }
    public required bool ShowFiles { get; init; }
}
