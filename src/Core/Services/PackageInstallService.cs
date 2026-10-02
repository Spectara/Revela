using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Outcome of <see cref="PackageInstallService.InstallAsync"/>.
/// </summary>
public enum PackageInstallStatus
{
    /// <summary>The package was installed and declared.</summary>
    Installed,

    /// <summary>The package could not be downloaded or extracted.</summary>
    Failed,

    /// <summary>The package's nuspec does not declare the required package type; nothing was declared.</summary>
    WrongPackageType,

    /// <summary>The package was installed, but declaring it failed; the installed files are kept.</summary>
    DeclarationFailed,

    /// <summary>No package installer is available (Standalone edition).</summary>
    InstallerUnavailable
}

/// <summary>
/// Result of <see cref="PackageInstallService.InstallAsync"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Package">The extracted package, when one was extracted.</param>
/// <param name="Error">Why declaring the package failed (<see cref="PackageInstallStatus.DeclarationFailed"/>).</param>
public sealed record PackageInstallResult(PackageInstallStatus Status, InstalledPackage? Package = null, string? Error = null);

/// <summary>
/// The single install/uninstall flow behind <c>plugin install</c>, <c>theme install</c>, the setup
/// wizard and restore's theme fallback.
/// </summary>
/// <remarks>
/// <para>
/// The package type is validated against the types declared in the package's own nuspec (not the
/// package index). A freshly extracted package of the wrong type is removed again; a package that
/// was already installed is left in place.
/// </para>
/// <para>
/// Successful installs are declared via <see cref="PackageDeclarations"/> (project.json inside a
/// project, otherwise revela.json).
/// </para>
/// </remarks>
public sealed class PackageInstallService
{
    private readonly IPackageInstaller? installer;
    private readonly PackageDeclarations declarations;
    private readonly string pluginDirectory;

    /// <summary>
    /// Creates the service for the local plugin directory.
    /// </summary>
    public PackageInstallService(IEnumerable<IPackageInstaller> installers, PackageDeclarations declarations)
        : this(installers, declarations, ConfigPathResolver.LocalPluginDirectory)
    {
    }

    /// <summary>
    /// Creates the service for <paramref name="pluginDirectory"/>.
    /// </summary>
    public PackageInstallService(IEnumerable<IPackageInstaller> installers, PackageDeclarations declarations, string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(installers);
        installer = installers.FirstOrDefault();
        this.declarations = declarations;
        this.pluginDirectory = pluginDirectory;
    }

    /// <summary>
    /// Gets whether packages can be installed (only when the Packages feature is loaded).
    /// </summary>
    public bool IsAvailable => installer is not null;

    /// <summary>
    /// Returns whether <paramref name="packageId"/> is installed in the plugin directory.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    public bool IsInstalled(string packageId) =>
        !string.IsNullOrWhiteSpace(packageId)
        && packageId.IndexOfAny(['/', '\\']) < 0
        && !packageId.Contains("..", StringComparison.Ordinal)
        && File.Exists(Path.Combine(pluginDirectory, packageId, $"{packageId}.dll"));

    /// <summary>
    /// Installs <paramref name="packageId"/> when its nuspec declares <paramref name="requiredPackageType"/>
    /// and declares it.
    /// </summary>
    /// <param name="packageId">The full package ID.</param>
    /// <param name="requiredPackageType">The required NuGet package type (see <see cref="PackageIds"/>).</param>
    /// <param name="version">Exact version, or <see langword="null"/>/<c>latest</c> for the newest allowed version.</param>
    /// <param name="source">Optional feed name, URL or folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<PackageInstallResult> InstallAsync(
        string packageId,
        string requiredPackageType,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (installer is null)
        {
            return new PackageInstallResult(PackageInstallStatus.InstallerUnavailable);
        }

        var wasInstalled = IsInstalled(packageId);
        var package = await installer.InstallAsync(packageId, version, source, cancellationToken);
        if (package is null)
        {
            return new PackageInstallResult(PackageInstallStatus.Failed);
        }

        if (!package.PackageTypes.Contains(requiredPackageType, StringComparer.OrdinalIgnoreCase))
        {
            if (!wasInstalled)
            {
                _ = await installer.UninstallAsync(package.Id, cancellationToken);
            }

            return new PackageInstallResult(PackageInstallStatus.WrongPackageType, package);
        }

        try
        {
            await declarations.DeclareAsync(package.Id, package.Version, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PackageInstallResult(PackageInstallStatus.DeclarationFailed, package, $"{declarations.TargetPath}: {ex.Message}");
        }

        return new PackageInstallResult(PackageInstallStatus.Installed, package);
    }

    /// <summary>
    /// Removes the package files and its declaration.
    /// </summary>
    /// <param name="packageId">The full package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the package was installed and removed.</returns>
    public async Task<bool> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (installer is null || !await installer.UninstallAsync(packageId, cancellationToken))
        {
            return false;
        }

        await declarations.RemoveAsync(packageId, cancellationToken);
        return true;
    }
}
