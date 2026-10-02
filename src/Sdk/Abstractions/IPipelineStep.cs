namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents a step in a named pipeline (generate, clean, deploy, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Pipeline steps provide UI-free execution for programmatic callers
/// (MCP Server, GUI, third-party plugins). Each step contains pure
/// service logic without console output.
/// </para>
/// <para>
/// Commands that also serve as pipeline steps implement this interface
/// via explicit interface implementation, keeping the CLI execution path
/// (with Spectre.Console UI) separate from the service execution path.
/// </para>
/// <para>
/// Step ordering is defined once on <see cref="CommandDescriptor.Order"/>
/// when registering with <c>IsSequentialStep: true</c>. The host uses this order
/// for both the CLI "all" command and <see cref="Engine.IRevelaEngine"/>.
/// </para>
/// </remarks>
public interface IPipelineStep
{
    /// <summary>
    /// Gets the pipeline category this step belongs to.
    /// </summary>
    /// <remarks>
    /// Well-known categories: <see cref="PipelineCategories.Generate"/>,
    /// <see cref="PipelineCategories.Clean"/>.
    /// Third-party plugins can define new categories (e.g., "deploy").
    /// </remarks>
    string Category { get; }

    /// <summary>
    /// Gets the step name (used for progress reporting and logging).
    /// </summary>
    /// <remarks>
    /// Short identifier like "scan", "statistics", "pages", "images".
    /// Must match the command name registered via <see cref="CommandDescriptor"/>.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Executes this step without any console output.
    /// </summary>
    /// <remarks>
    /// Implementations must not write to <c>System.Console</c> or
    /// <c>Spectre.Console.AnsiConsole</c>. Use logging for diagnostics.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    ValueTask<OperationResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Well-known pipeline categories.
/// </summary>
public static class PipelineCategories
{
    /// <summary>Content generation pipeline (scan → plugin data → pages → images).</summary>
    public const string Generate = "generate";

    /// <summary>Cleanup pipeline (output → images → cache → plugin data).</summary>
    public const string Clean = "clean";

    /// <summary>Structural checks (config → structure → theme → content → slugs → plugins).</summary>
    /// <remarks>
    /// Checks are surfaced under the <c>check</c> command group and the bespoke
    /// <c>check all</c> aggregator. They are never <see cref="IPipelineStep"/>, so they
    /// do not participate in <c>generate all</c>.
    /// </remarks>
    public const string Check = "check";
}

/// <summary>
/// Host step order constants for the generate pipeline.
/// </summary>
/// <remarks>
/// Plugin steps choose an order relative to these slots: steps that produce data
/// consumed by page rendering go between <see cref="Scan"/> and <see cref="Pages"/>
/// (for example <c>Scan + 50</c>); post-processing of the rendered site goes after
/// <see cref="Images"/>.
/// </remarks>
public static class PipelineOrder
{
    /// <summary>Content scanning (100).</summary>
    public const int Scan = 100;

    /// <summary>HTML page generation (300).</summary>
    public const int Pages = 300;

    /// <summary>Image processing (400).</summary>
    public const int Images = 400;
}

/// <summary>
/// Host step order constants for the clean pipeline.
/// </summary>
/// <remarks>
/// Plugin clean steps that remove their own derived data run after <see cref="Cache"/>
/// (for example <c>Cache + 100</c>).
/// </remarks>
public static class CleanPipelineOrder
{
    /// <summary>Clean output directory (100).</summary>
    public const int Output = 100;

    /// <summary>Clean unused images (150).</summary>
    public const int Images = 150;

    /// <summary>Clean cache directory (200).</summary>
    public const int Cache = 200;
}
