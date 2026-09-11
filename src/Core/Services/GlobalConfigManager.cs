using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;

using Spectara.Revela.Sdk.Json;
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
    private string? ExplicitConfigFilePath { get; }
    private JsonObject? cachedConfig;

    internal GlobalConfigManager(ILogger<GlobalConfigManager> logger, string configFilePath) : this(logger)
    {
        if (!Path.IsPathFullyQualified(configFilePath))
        {
            throw new ArgumentException("Configuration file path must be absolute.", nameof(configFilePath));
        }

        ExplicitConfigFilePath = configFilePath;
    }

    /// <inheritdoc />
    public string ConfigFilePath => ExplicitConfigFilePath ?? ConfigPathResolver.ConfigFilePath;

    /// <inheritdoc />
    public bool ConfigFileExists() => File.Exists(ConfigFilePath);

    /// <summary>
    /// Loads the global configuration file, creating defaults if not exists.
    /// </summary>
    private async Task<JsonObject> LoadFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cachedConfig is not null)
        {
            return cachedConfig;
        }

        var configPath = ConfigFilePath;

        if (!File.Exists(configPath))
        {
            LogCreatingDefaultConfig(configPath);
            var defaults = JsonSerializer.SerializeToNode(new GlobalConfigFile(), GlobalConfigJsonContext.Default.GlobalConfigFile)!.AsObject();
            await SaveFileAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            ValidateConfiguration(json);
            cachedConfig = JsonNode.Parse(json, nodeOptions: null, RevelaJsonOptions.LenientDocument) as JsonObject
                ?? throw new InvalidDataException("Global configuration must be a JSON object.");
            LogConfigLoaded(configPath);
            return cachedConfig;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            LogConfigCorrupted(configPath, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Saves the global configuration file.
    /// </summary>
    private async Task SaveFileAsync(JsonObject config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = config.ToJsonString(GlobalConfigJsonContext.Default.Options);
        ValidateConfiguration(json);
        var configPath = ConfigFilePath;

        // Ensure directory exists
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        var temporaryPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var temporaryFileCreated = false;
        try
        {
            await using (var stream = new FileStream(temporaryPath, streamOptions))
            {
                temporaryFileCreated = true;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, configPath, overwrite: true);
            temporaryFileCreated = false;
        }
        finally
        {
            if (temporaryFileCreated)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogTemporaryFileCleanupFailed(temporaryPath, ex.Message);
                }
            }
        }

        cachedConfig = config;
    }

    private static void ValidateConfiguration(string json)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            using var validation = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidDataException("Global configuration is invalid for the JSON configuration reader.", ex);
        }
    }

    private static JsonObject? GetSection(JsonObject parent, string name, bool create = true)
    {
        var names = parent.Select(property => property.Key)
            .Where(key => key.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (names.Length == 0)
        {
            if (!create)
            {
                return null;
            }

            var section = new JsonObject();
            parent.Add(name, section);
            return section;
        }

        var result = parent[names[0]] as JsonObject
            ?? throw new InvalidDataException($"Configuration section '{name}' must be an object.");
        foreach (var otherName in names.Skip(1))
        {
            var other = parent[otherName] as JsonObject
                ?? throw new InvalidDataException($"Configuration section '{name}' must be an object.");
            MergeObjects(result, other);
            _ = parent.Remove(otherName);
        }

        return result;
    }

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (!target.TryGetPropertyValue(property.Key, out var existing))
            {
                target.Add(property.Key, property.Value?.DeepClone());
            }
            else if (existing is JsonObject existingObject && property.Value is JsonObject sourceObject)
            {
                MergeObjects(existingObject, sourceObject);
            }
            else
            {
                throw new InvalidDataException($"Cannot combine configuration property '{property.Key}'.");
            }
        }
    }

    private static JsonObject? GetMapping(JsonObject parent, string name, bool create = true)
    {
        var section = GetSection(parent, name, create);
        if (section is not null)
        {
            foreach (var property in section)
            {
                if (property.Value is not null && (property.Value is not JsonValue value || !value.TryGetValue<string>(out _)))
                {
                    throw new InvalidDataException($"Configuration mapping '{name}' must contain strings or null values.");
                }
            }
        }

        return section;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "NuGet feed URL can be local path OR remote URL")]
    public async Task AddFeedAsync(string name, string url, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var feeds = GetMapping(GetSection(config, "packages")!, "feeds")!;

        if (feeds.ContainsKey(name))
        {
            throw new InvalidOperationException($"Feed '{name}' already exists");
        }

        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add feed with reserved name 'nuget.org'");
        }

        feeds[name] = url;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveFeedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in feed 'nuget.org'");
        }

        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var packages = GetSection(config, "packages", create: false);
        var feeds = packages is null ? null : GetMapping(packages, "feeds", create: false);

        if (feeds is null || !feeds.Remove(name))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task AddThemeAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        GetMapping(config, "themes")![packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveThemeAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var themes = GetMapping(config, "themes", create: false);

        if (themes is null || !themes.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        GetMapping(config, "plugins")![packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemovePluginAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var plugins = GetMapping(config, "plugins", create: false);

        if (plugins is null || !plugins.Remove(packageId))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetThemesAsync(CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        return GetMapping(config, "themes", create: false)?.ToDictionary(property => property.Key, property => property.Value?.GetValue<string>()!, StringComparer.Ordinal) ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetPluginsAsync(CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        return GetMapping(config, "plugins", create: false)?.ToDictionary(property => property.Key, property => property.Value?.GetValue<string>()!, StringComparer.Ordinal) ?? [];
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating default config at '{ConfigPath}'")]
    private partial void LogCreatingDefaultConfig(string configPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded config from '{ConfigPath}'")]
    private partial void LogConfigLoaded(string configPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config file '{ConfigPath}' is invalid ({Error}), refusing to overwrite it")]
    private partial void LogConfigCorrupted(string configPath, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove temporary configuration file '{TemporaryPath}' ({Error})")]
    private partial void LogTemporaryFileCleanupFailed(string temporaryPath, string error);

    #endregion

    /// <summary>
    /// Internal file structure for revela.json serialization
    /// </summary>
    internal sealed class GlobalConfigFile
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

/// <summary>
/// Source-generated JSON serializer context for the global revela.json file.
/// </summary>
[JsonSerializable(typeof(GlobalConfigManager.GlobalConfigFile))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GlobalConfigJsonContext : JsonSerializerContext;
