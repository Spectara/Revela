using System.Text.Json;
using System.Text.Json.Serialization;
using Spectara.Revela.Core.Configuration;

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
/// </remarks>
public static class GlobalConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static GlobalConfig? cachedConfig;

    /// <summary>
    /// Gets the path to the config file
    /// </summary>
    public static string ConfigFilePath => ConfigPathResolver.ConfigFilePath;

    /// <summary>
    /// Loads the global configuration, creating defaults if not exists
    /// </summary>
    public static async Task<GlobalConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (cachedConfig is not null)
        {
            return cachedConfig;
        }

        var configPath = ConfigFilePath;

        if (!File.Exists(configPath))
        {
            // Create default config
            cachedConfig = new GlobalConfig();
            await SaveAsync(cachedConfig, cancellationToken);
            return cachedConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            cachedConfig = JsonSerializer.Deserialize<GlobalConfig>(json, JsonOptions) ?? new GlobalConfig();
        }
        catch
        {
            // Corrupted config - use defaults
            cachedConfig = new GlobalConfig();
        }

        return cachedConfig;
    }

    /// <summary>
    /// Saves the global configuration
    /// </summary>
    public static async Task SaveAsync(GlobalConfig config, CancellationToken cancellationToken = default)
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
        var config = await LoadAsync(cancellationToken);

        // Check for duplicate
        if (config.Feeds.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Feed '{name}' already exists");
        }

        // Check reserved name
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add feed with reserved name 'nuget.org'");
        }

        var newFeeds = new List<FeedConfig>(config.Feeds)
        {
            new() { Name = name, Url = url }
        };

        var newConfig = new GlobalConfig
        {
            Feeds = newFeeds,
            Logging = config.Logging,
            Defaults = config.Defaults,
            CheckUpdates = config.CheckUpdates
        };

        await SaveAsync(newConfig, cancellationToken);
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

        var config = await LoadAsync(cancellationToken);
        var newFeeds = config.Feeds
            .Where(f => !f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (newFeeds.Count == config.Feeds.Count)
        {
            return false; // Not found
        }

        var newConfig = new GlobalConfig
        {
            Feeds = newFeeds,
            Logging = config.Logging,
            Defaults = config.Defaults,
            CheckUpdates = config.CheckUpdates
        };

        await SaveAsync(newConfig, cancellationToken);
        return true;
    }

    /// <summary>
    /// Clears the cached configuration (for testing)
    /// </summary>
    public static void ClearCache()
    {
        cachedConfig = null;
    }
}
