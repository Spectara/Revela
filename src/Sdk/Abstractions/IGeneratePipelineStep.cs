namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents a step in the generate pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Pipeline steps are executed in order by <c>generate all</c>.
/// Core steps (scan, pages, images) use orders 100, 300, 400.
/// Plugins can insert steps at any order, e.g., statistics at 200.
/// </para>
/// <para>
/// Steps are discovered via DI - register as <c>IGeneratePipelineStep</c>
/// in <see cref="IPlugin.ConfigureServices"/> or in the core service registration.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Plugin registration
/// public void ConfigureServices(IServiceCollection services)
/// {
///     services.AddTransient&lt;IGeneratePipelineStep, MyPipelineStep&gt;();
/// }
/// </code>
/// </example>
public interface IGeneratePipelineStep
{
    /// <summary>
    /// Gets the step name (used for display and logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this step does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the execution order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lower values execute first. Standard orders:
    /// </para>
    /// <list type="bullet">
    /// <item>100 - Scan (content discovery)</item>
    /// <item>200 - Statistics (EXIF extraction)</item>
    /// <item>300 - Pages (HTML generation)</item>
    /// <item>400 - Images (image processing)</item>
    /// </list>
    /// <para>
    /// Use values between these to insert custom steps.
    /// </para>
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// Executes this pipeline step.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<PipelineStepResult> ExecuteAsync(
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Standard pipeline order constants.
/// </summary>
public static class PipelineOrder
{
    /// <summary>Content scanning (100).</summary>
    public const int Scan = 100;

    /// <summary>Statistics/EXIF extraction (200).</summary>
    public const int Statistics = 200;

    /// <summary>HTML page generation (300).</summary>
    public const int Pages = 300;

    /// <summary>Image processing (400).</summary>
    public const int Images = 400;
}

/// <summary>
/// Result of a pipeline step execution.
/// </summary>
/// <param name="Success">Whether the step completed successfully.</param>
/// <param name="Message">Optional message (success info or error details).</param>
/// <param name="ItemsProcessed">Number of items processed (for display).</param>
/// <param name="ErrorDisplayed">Whether error was already displayed (skip duplicate output).</param>
public sealed record PipelineStepResult(
    bool Success,
    string? Message = null,
    int ItemsProcessed = 0,
    bool ErrorDisplayed = false)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PipelineStepResult Ok(string? message = null, int itemsProcessed = 0) =>
        new(true, message, itemsProcessed);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PipelineStepResult Fail(string message) =>
        new(false, message);

    /// <summary>
    /// Creates a failed result where error was already displayed to user.
    /// </summary>
    public static PipelineStepResult FailWithDisplayedError(string message) =>
        new(false, message, ErrorDisplayed: true);

    /// <summary>
    /// Creates a skipped result (e.g., no items to process).
    /// </summary>
    public static PipelineStepResult Skipped(string reason) =>
        new(true, reason);
}

/// <summary>
/// Progress information for pipeline step execution.
/// </summary>
/// <param name="Current">Current item index (1-based).</param>
/// <param name="Total">Total items to process.</param>
/// <param name="Status">Current status message.</param>
public sealed record PipelineProgress(int Current, int Total, string Status);
