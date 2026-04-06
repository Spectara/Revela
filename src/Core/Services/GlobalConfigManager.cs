using System.Text.Json;
using System.Text.Json.Serialization;

using Spectara.Revela.Sdk.Services;
namespace Spectara.Revela.Core.Services;

/// <summary>
/// Manages the global CLI configuration (revela.json).
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
public sealed partial class GlobalConfigManager(ILogger<GlobalConfigManager> logger) : IGlobalConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private GlobalConfigFile? cachedConfig;

    /// <inheritdoc />
    public string ConfigFilePath => ConfigPathResolver.ConfigFilePath;

    /// <inheritdoc />
    public bool ConfigFileExists() => File.Exists(ConfigFilePath);

    /// <summary>
    /// Loads the global configuration file, creating defaults if not exists.
    /// </summary>
    private async Task<GlobalConfigFile> LoadFileAsync(CancellationToken cancellationToken = default)
    {
        if (cachedConfig is not null)
        {
            return cachedConfig;
        }

        var configPath = ConfigFilePath;

        if (!File.Exists(configPath))
        {
            LogCreatingDefaultConfig(configPath);
            cachedConfig = new GlobalConfigFile();
            await SaveFileAsync(cachedConfig, cancellationToken);
            return cachedConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            cachedConfig = JsonSerializer.Deserialize<GlobalConfigFile>(json, JsonOptions) ?? new GlobalConfigFile();
            LogConfigLoaded(configPath);
        }
        catch (Exception ex)
        {
            LogConfigCorrupted(configPath, ex.Message);
            cachedConfig = new GlobalConfigFile();
        }

        return cachedConfig;
    }

    /// <summary>
    /// Saves the global configuration file.
    /// </summary>
    private async Task SaveFileAsync(GlobalConfigFile config, CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
#pragma warning disable CA1054 // URI parameters should not be strings - supports both URLs and local paths
    public async Task AddFeedAsync(string name, string url, CancellationToken cancellationToken = default)
#pragma warning restore CA1054
    {
        var config = await LoadFileAsync(cancellationToken);

        if (config.Packages.Feeds.ContainsKey(name))
        {
            throw new InvalidOperationException($"Feed '{name}' already exists");
        }

        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add feed with reserved name 'nuget.org'");
        }

        config.Packages.Feeds[name] = url;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveFeedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in feed 'nuget.org'");
        }

        var config = await LoadFileAsync(cancellationToken);

        if (!config.Packages.Feeds.Remove(name))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task AddThemeAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        config.Themes[packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveThemeAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);

        if (!config.Themes.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        config.Plugins[packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemovePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);

        if (!config.Plugins.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetThemesAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        return config.Themes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetPluginsAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadFileAsync(cancellationToken);
        return config.Plugins;
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating default config at '{ConfigPath}'")]
    private partial void LogCreatingDefaultConfig(string configPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded config from '{ConfigPath}'")]
    private partial void LogConfigLoaded(string configPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config file '{ConfigPath}' is corrupted ({Error}), using defaults")]
    private partial void LogConfigCorrupted(string configPath, string error);

    #endregion

    /// <summary>
    /// Internal file structure for revela.json serialization
    /// </summary>
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
