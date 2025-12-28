namespace Spectara.Revela.Core.Models;

/// <summary>
/// Result of a package installation operation.
/// </summary>
/// <param name="Installed">Packages that were successfully installed.</param>
/// <param name="AlreadyInstalled">Packages that were already installed (skipped).</param>
/// <param name="Failed">Packages that failed to install.</param>
public sealed record InstallResult(
    IReadOnlyList<string> Installed,
    IReadOnlyList<string> AlreadyInstalled,
    IReadOnlyList<string> Failed)
{
    /// <summary>
    /// Gets an empty result (no packages processed).
    /// </summary>
    public static InstallResult Empty { get; } = new([], [], []);

    /// <summary>
    /// Gets whether any packages were installed.
    /// </summary>
    public bool HasInstalled => Installed.Count > 0;

    /// <summary>
    /// Gets whether all processed packages were already installed.
    /// </summary>
    public bool AllAlreadyInstalled => Installed.Count == 0 && Failed.Count == 0 && AlreadyInstalled.Count > 0;

    /// <summary>
    /// Gets whether any packages failed to install.
    /// </summary>
    public bool HasFailures => Failed.Count > 0;

    /// <summary>
    /// Gets the total number of packages processed.
    /// </summary>
    public int TotalProcessed => Installed.Count + AlreadyInstalled.Count + Failed.Count;
}
