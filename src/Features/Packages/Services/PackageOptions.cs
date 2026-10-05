using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Features.Packages.Services;

/// <summary>
/// Where <see cref="PackageLoader"/> looks for plugins and themes.
/// </summary>
internal sealed record PackageOptions
{
    private const string DevelopmentEnvironment = "Development";

    /// <summary>
    /// Whether assemblies next to the executable are loaded as bundled packages.
    /// </summary>
    /// <remarks>
    /// Only for development (F5 / <c>launchSettings.json</c>), where plugins and themes are
    /// built next to the host via ProjectReference and work without being installed. Every
    /// <c>*.dll</c> there is loaded into the default context, so release builds keep this off
    /// and load installed packages only. Default: <see langword="false"/>.
    /// </remarks>
    public bool SearchApplicationDirectory { get; init; }

    /// <summary>
    /// Directory searched when <see cref="SearchApplicationDirectory"/> is set.
    /// </summary>
    public string ApplicationDirectory { get; init; } = AppContext.BaseDirectory;

    /// <summary>
    /// Directory of installed packages (<c>plugins/{PackageId}/{PackageId}.dll</c>).
    /// </summary>
    public string PluginDirectory { get; init; } = ConfigPathResolver.LocalPluginDirectory;

    /// <summary>
    /// Creates the options for a host environment: the application directory is only
    /// searched in Development.
    /// </summary>
    /// <param name="environmentName">The host environment name (<c>DOTNET_ENVIRONMENT</c>).</param>
    public static PackageOptions ForEnvironment(string environmentName) => new()
    {
        SearchApplicationDirectory = string.Equals(environmentName, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase),
    };
}
