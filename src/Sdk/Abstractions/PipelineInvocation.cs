using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Explicit signal telling a pipeline step's CLI command that it runs as part of
/// a multi-step pipeline (e.g. <c>revela generate all</c>) rather than standalone.
/// </summary>
/// <remarks>
/// <para>
/// The host attaches the hidden <see cref="InPipelineOption"/> to every step of an
/// auto-generated <c>all</c> command and passes it when invoking the steps. Standalone
/// invocations (<c>revela generate pages</c>) never set it.
/// </para>
/// <para>
/// Steps use it to suppress standalone-only output such as "Next steps" hints —
/// the pipeline prints a single next-step hint when it completes.
/// </para>
/// </remarks>
public static class PipelineInvocation
{
    /// <summary>
    /// Gets the hidden option the host passes to steps executed inside a pipeline.
    /// </summary>
    public static Option<bool> InPipelineOption { get; } = new("--in-pipeline")
    {
        Description = "Set by the host when the command runs as a pipeline step",
        Hidden = true,
    };

    /// <summary>
    /// Gets whether the parsed command was invoked as a step of a multi-step pipeline.
    /// </summary>
    /// <param name="parseResult">The parse result passed to the command action.</param>
    /// <returns><c>true</c> inside a pipeline; <c>false</c> for standalone invocations.</returns>
    public static bool IsInPipeline(this ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        return parseResult.GetValue(InPipelineOption);
    }
}
