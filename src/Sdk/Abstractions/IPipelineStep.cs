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
/// when registering with <c>IsSequentialStep: true</c>. The host stores
/// this in <see cref="IPipelineStepOrderProvider"/> which both the CLI "all"
/// command and <see cref="Engine.IRevelaEngine"/> use for sorting.
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
    Task<PipelineStepResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides pipeline step ordering information collected during command registration.
/// </summary>
/// <remarks>
/// <para>
/// This is the single source of truth for step ordering. Order values come from
/// <see cref="CommandDescriptor.Order"/> when <c>IsSequentialStep: true</c>.
/// </para>
/// <para>
/// The host populates this during command registration. Both the CLI "all" command
/// and <see cref="Engine.IRevelaEngine"/> use it for sorting.
/// </para>
/// </remarks>
public interface IPipelineStepOrderProvider
{
    /// <summary>
    /// Gets the execution order for a pipeline step.
    /// </summary>
    /// <param name="category">The pipeline category (e.g., "generate", "clean").</param>
    /// <param name="name">The step name (e.g., "scan", "pages").</param>
    /// <returns>The order value, or <see cref="int.MaxValue"/> if not registered.</returns>
    int GetOrder(string category, string name);
}

/// <summary>
/// Well-known pipeline categories.
/// </summary>
public static class PipelineCategories
{
    /// <summary>Content generation pipeline (scan → statistics → pages → images).</summary>
    public const string Generate = "generate";

    /// <summary>Cleanup pipeline (output → images → cache → plugin data).</summary>
    public const string Clean = "clean";
}

/// <summary>
/// Standard pipeline step order constants for the generate pipeline.
/// </summary>
public static class PipelineOrder
{
    /// <summary>Content scanning (100).</summary>
    public const int Scan = 100;

    /// <summary>Calendar generation (150).</summary>
    public const int Calendar = 150;

    /// <summary>Statistics/EXIF aggregation (200).</summary>
    public const int Statistics = 200;

    /// <summary>HTML page generation (300).</summary>
    public const int Pages = 300;

    /// <summary>Image processing (400).</summary>
    public const int Images = 400;
}

/// <summary>
/// Standard pipeline step order constants for the clean pipeline.
/// </summary>
public static class CleanPipelineOrder
{
    /// <summary>Clean output directory (100).</summary>
    public const int Output = 100;

    /// <summary>Clean unused images (150).</summary>
    public const int Images = 150;

    /// <summary>Clean cache directory (200).</summary>
    public const int Cache = 200;
}

/// <summary>
/// Result of a pipeline step execution.
/// </summary>
public sealed record PipelineStepResult
{
    /// <summary>Whether the step succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Error message if the step failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static PipelineStepResult Ok() => new() { Success = true };

    /// <summary>Creates a failed result with an error message.</summary>
    public static PipelineStepResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
