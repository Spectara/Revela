using System.Text.Json;
using System.Text.Json.Serialization;

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
/// The config file is created with defaults on first access if it doesn't exist.
/// </para>
/// <para>
/// NOTE: This class handles WRITING to revela.json. For READING, use
/// IOptionsMonitor&lt;FeedsConfig&gt;, IOptionsMonitor&lt;DependenciesConfig&gt;, etc.
/// </para>
/// </remarks>
public static class GlobalConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static GlobalConfigFile? cachedConfig;

    /// <summary>
    /// Gets the path to the config file
    /// </summary>
    public static string ConfigFilePath => ConfigPathResolver.ConfigFilePath;

    /// <summary>
    /// Loads the global configuration file, creating defaults if not exists
    /// </summary>
    private static async Task<GlobalConfigFile> LoadFileAsync(CancellationToken cancellationToken = default)
    {
        if (cachedConfig is not null)
        {
            return cachedConfig;
        }

        var configPath = ConfigFilePath;

        if (!File.Exists(configPath))
        {
            // Create default config
            cachedConfig = new GlobalConfigFile();
            await SaveFileAsync(cachedConfig, cancellationToken);
            return cachedConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            cachedConfig = JsonSerializer.Deserialize<GlobalConfigFile>(json, JsonOptions) ?? new GlobalConfigFile();
        }
        catch
        {
            // Corrupted config - use defaults
            cachedConfig = new GlobalConfigFile();
        }

        return cachedConfig;
    }

    /// <summary>
    /// Saves the global configuration file
    /// </summary>
    private static async Task SaveFileAsync(GlobalConfigFile config, CancellationToken cancellationToken = default)
    {
        var configPath = ConfigFilePath;

        // Ensure directory exists
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);

        cachedConfig = config;
    }

    /// <summary>
    /// Adds a feed to the configuration
    /// </summary>
#pragma warning disable CA1054 // URI parameters should not be strings - supports both URLs and local paths
    public static async Task AddFeedAsync(string name, string url, CancellationToken cancellationToken = default)
#pragma warning restore CA1054
    {
        var config = await LoadFileAsync(cancellationToken);

        // Check for duplicate
        if (config.Packages.Feeds.ContainsKey(name))
        {
            throw new InvalidOperationException($"Feed '{name}' already exists");
        }

        // Check reserved name
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add feed with reserved name 'nuget.org'");
        }

        config.Packages.Feeds[name] = url;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <summary>
    /// Removes a feed from the configuration
    /// </summary>
    /// <returns>True if the feed was found and removed</returns>
    public static async Task<bool> RemoveFeedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in feed 'nuget.org'");
        }

        var config = await LoadFileAsync(cancellationToken);

        if (!config.Packages.Feeds.Remove(name))
        {
            return false; // Not found
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <summary>
    /// Adds a theme to the global configuration
    /// </summary>
    public static async Task AddThemeAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        config.Themes[packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <summary>
    /// Removes a theme from the global configuration
    /// </summary>
    /// <returns>True if the theme was found and removed</returns>
    public static async Task<bool> RemoveThemeAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);

        if (!config.Themes.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <summary>
    /// Adds a plugin to the global configuration
    /// </summary>
    public static async Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        config.Plugins[packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <summary>
    /// Removes a plugin from the global configuration
    /// </summary>
    /// <returns>True if the plugin was found and removed</returns>
    public static async Task<bool> RemovePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);

        if (!config.Plugins.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <summary>
    /// Clears the cached configuration (for testing)
    /// </summary>
    public static void ClearCache() => cachedConfig = null;

    /// <summary>
    /// Internal file structure for revela.json serialization
    /// </summary>
    /// <remarks>
    /// This matches the JSON structure written to disk. For reading, use the
    /// separate config classes via IOptionsMonitor (PackagesConfig, DependenciesConfig, etc.)
    /// </remarks>
    private sealed class GlobalConfigFile
    {
        public PackagesSection Packages { get; init; } = new();
        public LoggingSection Logging { get; init; } = new();
        public DefaultsSection Defaults { get; init; } = new();
        public bool CheckUpdates { get; init; } = true;
        public Dictionary<string, string> Themes { get; init; } = [];
        public Dictionary<string, string> Plugins { get; init; } = [];

        public sealed class PackagesSection
        {
            public Dictionary<string, string> Feeds { get; init; } = [];
        }

        public sealed class LoggingSection
        {
            public Dictionary<string, string> LogLevel { get; init; } = new()
            {
                ["Default"] = "Warning",
                ["Spectara.Revela"] = "Warning",
                ["Microsoft"] = "Warning",
                ["System"] = "Warning"
            };
        }

        public sealed class DefaultsSection
        {
            public string Theme { get; init; } = "Lumina";
        }
    }
}
