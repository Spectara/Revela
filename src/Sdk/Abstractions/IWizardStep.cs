namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Represents an optional wizard step that plugins can provide.
/// </summary>
/// <remarks>
/// <para>
/// Plugins can implement this interface to add optional configuration steps
/// to the project setup wizard. After the core steps (project, theme, site),
/// the wizard will offer all registered wizard steps to the user.
/// </para>
/// <para>
/// The user can select which optional steps to run via checkboxes.
/// This allows plugins to integrate into the setup flow without
/// the core needing to know about specific plugins.
/// </para>
/// <example>
/// <code>
/// public sealed class OneDriveWizardStep : IWizardStep
/// {
///     public string Name => "OneDrive Source";
///     public string Description => "Import images from OneDrive shared folder";
///     public int Order => 100;
///
///     public bool ShouldPrompt() => !IsAlreadyConfigured();
///
///     public async Task&lt;int&gt; ExecuteAsync(CancellationToken ct)
///     {
///         // Run interactive configuration
///         return await configCommand.ExecuteAsync(ct);
///     }
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
    /// Shown in the checkbox list, e.g., "OneDrive Source".
    /// Keep it short (2-3 words).
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets a brief description of what this step configures.
    /// </summary>
    /// <remarks>
    /// Shown as additional context, e.g., "Import images from OneDrive shared folder".
    /// One sentence, no period at end.
    /// </remarks>
    string Description { get; }

    /// <summary>
    /// Gets the order in which this step should appear.
    /// </summary>
    /// <remarks>
    /// Lower values appear first. Recommended ranges:
    /// <list type="bullet">
    /// <item>100-199: Source providers (OneDrive, Dropbox, etc.)</item>
    /// <item>200-299: Build options (Statistics, etc.)</item>
    /// <item>300-399: Output options (Deploy, etc.)</item>
    /// </list>
    /// </remarks>
    int Order { get; }

    /// <summary>
    /// Determines whether this step should be offered to the user.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the step should be shown in the wizard;
    /// <c>false</c> to skip (e.g., already configured).
    /// </returns>
    /// <remarks>
    /// Called before displaying the optional steps list.
    /// Use this to hide steps that are already configured or not applicable.
    /// </remarks>
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
