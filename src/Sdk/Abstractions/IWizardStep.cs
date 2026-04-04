namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents a wizard step that plugins can provide for project setup.
/// </summary>
/// <remarks>
/// <para>
/// Plugins implement this interface to add configuration steps
/// to the project setup wizard. Steps are divided into two categories:
/// </para>
/// <list type="bullet">
/// <item><b>Required steps</b> (<see cref="IsRequired"/> = true): Always run in Order sequence
/// as part of the main wizard flow (e.g., paths, theme, images).</item>
/// <item><b>Optional steps</b> (<see cref="IsRequired"/> = false, default): Offered to the user
/// as checkboxes after the required steps complete.</item>
/// </list>
/// <para>
/// This allows plugins to integrate into the setup flow without
/// the host needing to know about specific plugins at compile time.
/// </para>
/// <example>
/// <code>
/// // Required step (runs automatically in wizard flow)
/// public sealed class PathsWizardStep : IWizardStep
/// {
///     public string Name =&gt; "Directory Paths";
///     public string Description =&gt; "Configure source and output directories";
///     public int Order =&gt; 20;
///     public bool IsRequired =&gt; true;
///     public bool ShouldPrompt() =&gt; true;
///     public Task&lt;int&gt; ExecuteAsync(CancellationToken ct) =&gt; ...;
/// }
///
/// // Optional step (shown as checkbox)
/// public sealed class OneDriveWizardStep : IWizardStep
/// {
///     public string Name =&gt; "OneDrive Source";
///     public string Description =&gt; "Import images from OneDrive shared folder";
///     public int Order =&gt; 100;
///     // IsRequired defaults to false via default interface method
///     public bool ShouldPrompt() =&gt; !IsAlreadyConfigured();
///     public Task&lt;int&gt; ExecuteAsync(CancellationToken ct) =&gt; ...;
/// }
/// </code>
/// </example>
/// </remarks>
public interface IWizardStep
{
    /// <summary>
    /// Gets the display name for this wizard step.
    /// </summary>
    /// <remarks>
    /// Shown in the wizard header or checkbox list, e.g., "Directory Paths".
    /// Keep it short (2-3 words).
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets a brief description of what this step configures.
    /// </summary>
    /// <remarks>
    /// Shown as additional context, e.g., "Configure source and output directories".
    /// One sentence, no period at end.
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Gets the order in which this step should appear.
    /// </summary>
    /// <remarks>
    /// Lower values appear first. Recommended ranges:
    /// <list type="bullet">
    /// <item>10-49: Required setup steps (paths, theme, images)</item>
    /// <item>100-199: Source providers (OneDrive, Dropbox, etc.)</item>
    /// <item>200-299: Build options (Statistics, etc.)</item>
    /// <item>300-399: Output options (Deploy, etc.)</item>
    /// </list>
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// Whether this step is required and runs automatically in the wizard flow.
    /// </summary>
    /// <remarks>
    /// Required steps run in Order sequence as numbered steps in the wizard.
    /// Optional steps (default) are offered as checkboxes after required steps complete.
    /// </remarks>
    bool IsRequired => false;

    /// <summary>
    /// Determines whether this step should be offered to the user.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the step should be shown in the wizard;
    /// <c>false</c> to skip (e.g., already configured).
    /// </returns>
    bool ShouldPrompt();

    /// <summary>
    /// Executes the wizard step configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code: 0 = success, non-zero = error/cancelled.</returns>
    /// <remarks>
    /// Should run interactively, prompting the user for required values.
    /// Typically delegates to an existing config command's ExecuteAsync method.
    /// </remarks>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}
