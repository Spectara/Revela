using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Core.Abstractions;

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
    /// Installs a package by NuGet ID when its nuspec declares <paramref name="requiredPackageType"/>.
    /// </summary>
    /// <remarks>
    /// The package type is checked before any file is written, so a package of the wrong type
    /// never replaces or adds files in the plugin directory.
    /// </remarks>
    /// <param name="packageId">NuGet package ID.</param>
    /// <param name="requiredPackageType">The required NuGet package type (see <see cref="PackageIds"/>).</param>
    /// <param name="version">Optional exact version; <c>null</c>, empty or <c>"latest"</c> selects the newest allowed version.</param>
    /// <param name="source">Optional NuGet source override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="PackageInstallStatus.Installed"/> with the installed package,
    /// <see cref="PackageInstallStatus.WrongPackageType"/> with the package's declared types (nothing written),
    /// or <see cref="PackageInstallStatus.Failed"/>.
    /// </returns>
    Task<PackageInstallResult> InstallAsync(
        string packageId,
        string requiredPackageType,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a package by NuGet ID.
    /// </summary>
    /// <param name="packageId">NuGet package ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if uninstallation succeeded.</returns>
    Task<bool> UninstallAsync(string packageId, CancellationToken cancellationToken = default);
}
