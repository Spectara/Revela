namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents a step in the generate pipeline with full console output.
/// </summary>
/// <remarks>
/// <para>
/// Generate steps are executed in order by <c>generate all</c>.
/// Each step is responsible for its own console output (progress bars, panels, etc.).
/// </para>
/// <para>
/// Standard orders (see <see cref="GenerateStepOrder"/>):
/// </para>
/// <list type="bullet">
/// <item>100 - Scan (content discovery)</item>
/// <item>200 - Statistics (EXIF aggregation)</item>
/// <item>300 - Pages (HTML generation)</item>
/// <item>400 - Images (image processing)</item>
/// </list>
/// <para>
/// Steps are discovered via DI. Register in <see cref="IPlugin.ConfigureServices"/>:
/// </para>
/// <code>
/// services.AddTransient&lt;IGenerateStep, MyGenerateStep&gt;();
/// </code>
/// </remarks>
public interface IGenerateStep
{
    /// <summary>
    /// Gets the step name (used for display and logging).
    /// </summary>
    /// <remarks>
    /// Short identifier like "scan", "statistics", "pages", "images".
    /// Used in pipeline display: "scan → statistics → pages → images".
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this step does.
    /// </summary>
    /// <remarks>
    /// Brief description shown in step header, e.g., "Scan content and update manifest".
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Gets the execution order.
    /// </summary>
    /// <remarks>
    /// Lower values execute first. Use <see cref="GenerateStepOrder"/> constants
    /// or values between them for custom steps.
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// Executes this step with full console output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The step is responsible for all console output including:
    /// </para>
    /// <list type="bullet">
    /// <item>Progress display (spinners, progress bars, live displays)</item>
    /// <item>Success/error panels</item>
    /// <item>Any informational messages</item>
    /// </list>
    /// <para>
    /// Return 0 for success, non-zero for failure. The pipeline will stop
    /// on the first non-zero return value.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success, non-zero = failure).</returns>
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Standard generate step order constants.
/// </summary>
public static class GenerateStepOrder
{
    /// <summary>Content scanning (100).</summary>
    public const int Scan = 100;

    /// <summary>Statistics/EXIF aggregation (200).</summary>
    public const int Statistics = 200;

    /// <summary>HTML page generation (300).</summary>
    public const int Pages = 300;

    /// <summary>Image processing (400).</summary>
    public const int Images = 400;
}
