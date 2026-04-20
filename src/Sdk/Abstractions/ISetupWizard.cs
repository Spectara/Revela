namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// First-run setup wizard for installing themes and plugins.
/// </summary>
/// <remarks>
/// Only available when the Packages feature is loaded (dynamic CLI).
/// The interactive menu resolves this optionally — in embedded mode,
/// no wizard is registered and first-run setup is skipped.
/// </remarks>
public interface ISetupWizard
{
    /// <summary>
    /// Exit code indicating packages were installed and a restart is required.
    /// </summary>
    const int ExitCodeRestartRequired = 2;

    /// <summary>
    /// Runs the setup wizard interactively.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Exit code: 0 = completed, 1 = error/cancelled, 2 = restart required.
    /// </returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
