namespace Spectara.Revela.Core.Services;

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
/// This interface handles WRITING to revela.json. For READING, use
/// IOptionsMonitor&lt;FeedsConfig&gt;, IOptionsMonitor&lt;DependenciesConfig&gt;, etc.
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
#pragma warning disable CA1054 // URI parameters should not be strings - supports both URLs and local paths
    Task AddFeedAsync(string name, string url, CancellationToken cancellationToken = default);
#pragma warning restore CA1054

    /// <summary>
    /// Removes a feed from the configuration.
    /// </summary>
    /// <returns>True if the feed was found and removed.</returns>
    Task<bool> RemoveFeedAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a theme to the global configuration.
    /// </summary>
    Task AddThemeAsync(string packageId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a theme from the global configuration.
    /// </summary>
    /// <returns>True if the theme was found and removed.</returns>
    Task<bool> RemoveThemeAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a plugin to the global configuration.
    /// </summary>
    Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a plugin from the global configuration.
    /// </summary>
    /// <returns>True if the plugin was found and removed.</returns>
    Task<bool> RemovePluginAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all installed themes from the global configuration.
    /// </summary>
    /// <returns>Dictionary of package ID to version.</returns>
    Task<IReadOnlyDictionary<string, string>> GetThemesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all installed plugins from the global configuration.
    /// </summary>
    /// <returns>Dictionary of package ID to version.</returns>
    Task<IReadOnlyDictionary<string, string>> GetPluginsAsync(CancellationToken cancellationToken = default);
}
