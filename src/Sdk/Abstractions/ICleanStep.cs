namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents a step in the clean pipeline with full console output.
/// </summary>
/// <remarks>
/// <para>
/// Clean steps are executed in order by <c>clean all</c>.
/// Each step is responsible for its own console output (progress bars, panels, etc.).
/// </para>
/// <para>
/// Standard orders (see <see cref="CleanStepOrder"/>):
/// </para>
/// <list type="bullet">
/// <item>100 - Output (delete output directory)</item>
/// <item>150 - Images (smart cleanup of unused image variants)</item>
/// <item>200 - Cache (delete cache directory)</item>
/// </list>
/// <para>
/// Steps are discovered via DI. Register in <see cref="IPlugin.ConfigureServices"/>:
/// </para>
/// <code>
/// services.AddTransient&lt;ICleanStep, MyCleanStep&gt;();
/// </code>
/// </remarks>
public interface ICleanStep
{
    /// <summary>
    /// Gets the step name (used for display and logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this step cleans.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the execution order. Lower values execute first.
    /// Use <see cref="CleanStepOrder"/> constants or values between them.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes this clean step with full console output.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success, non-zero = failure).</returns>
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Standard clean step order constants.
/// </summary>
public static class CleanStepOrder
{
    /// <summary>Clean output directory (100).</summary>
    public const int Output = 100;

    /// <summary>Clean unused images (150).</summary>
    public const int Images = 150;

    /// <summary>Clean cache directory (200).</summary>
    public const int Cache = 200;
}
