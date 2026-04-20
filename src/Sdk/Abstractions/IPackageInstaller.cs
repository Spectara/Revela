namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Installs and uninstalls NuGet-based packages (plugins and themes).
/// </summary>
/// <remarks>
/// Abstraction over <c>PackageManager</c> to decouple consumers (e.g., ThemeService)
/// from the NuGet infrastructure. Only available when the Packages feature is loaded.
/// </remarks>
public interface IPackageInstaller
{
    /// <summary>
    /// Installs a package by NuGet ID.
    /// </summary>
    /// <param name="packageId">NuGet package ID.</param>
    /// <param name="version">Optional version constraint.</param>
    /// <param name="source">Optional NuGet source override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if installation succeeded.</returns>
    Task<bool> InstallAsync(string packageId, string? version = null, string? source = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a package by NuGet ID.
    /// </summary>
    /// <param name="packageId">NuGet package ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if uninstallation succeeded.</returns>
    Task<bool> UninstallAsync(string packageId, CancellationToken cancellationToken = default);
}
