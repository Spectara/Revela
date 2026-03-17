namespace Spectara.Revela.Sdk.Models.Engine;

/// <summary>
/// Progress during the full generation pipeline.
/// </summary>
public sealed record GenerateProgress
{
    /// <summary>Name of the current pipeline step.</summary>
    public required string StepName { get; init; }

    /// <summary>Index of the current step (0-based).</summary>
    public int CurrentStep { get; init; }

    /// <summary>Total number of steps in the pipeline.</summary>
    public int TotalSteps { get; init; }
}
