namespace Spectara.Revela.Sdk.Services;

/// <summary>
/// Manages the global CLI configuration (revela.json)
/// </summary>
/// <remarks>
/// <para>
/// Configuration is stored in:
/// </para>
/// <list type="bullet">
/// <item>Portable: next to revela.exe</item>
/// <item>dotnet tool: %APPDATA%/Revela/revela.json</item>
/// </list>
/// <para>
/// This interface handles WRITING to revela.json. For READING the merged configuration,
/// use IOptionsMonitor&lt;DependenciesConfig&gt;, etc.
/// </para>
/// </remarks>
public interface IGlobalConfigManager
{
    /// <summary>
    /// Gets the path to the config file.
    /// </summary>
    string ConfigFilePath { get; }

    /// <summary>
    /// Checks if the global config file exists.
    /// </summary>
    bool ConfigFileExists();

    /// <summary>
    /// Adds a feed to the configuration.
    /// </summary>
    /// <remarks>
    /// <paramref name="url"/> is <see cref="string"/> because NuGet feeds can be either
    /// remote HTTP URLs (<c>https://api.nuget.org/v3/index.json</c>) or local filesystem
    /// paths (<c>./packages</c>) — a heterogeneous mix that <see cref="Uri"/> would
    /// awkwardly conflate.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "NuGet feed URL can be local path OR remote URL")]
    Task AddFeedAsync(string name, string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a feed from the configuration.
    /// </summary>
    /// <returns>True if the feed was found and removed.</returns>
    Task<bool> RemoveFeedAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a package dependency (<c>dependencies.packages</c>) in the global configuration.
    /// </summary>
    /// <param name="packageId">Package ID.</param>
    /// <param name="version">Exact installed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddPackageAsync(string packageId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a package dependency from the global configuration.
    /// </summary>
    /// <returns>True if the package was found and removed.</returns>
    Task<bool> RemovePackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all package dependencies declared in the global configuration.
    /// </summary>
    /// <returns>Dictionary of package ID to version.</returns>
    Task<IReadOnlyDictionary<string, string?>> GetPackagesAsync(CancellationToken cancellationToken = default);
}
