using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;

using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Json;
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
/// The file is created by the first write (feed or package); reading a missing file
/// yields an empty document. Writes keep every unrelated value.
/// </para>
/// <para>
/// NOTE: This class handles WRITING to revela.json. For READING the merged configuration,
/// use IOptionsMonitor&lt;DependenciesConfig&gt;, etc.
/// </para>
/// </remarks>
public sealed partial class GlobalConfigManager(
    ILogger<GlobalConfigManager> logger,
    ConfigFileWriter configFileWriter) : IGlobalConfigManager
{
    private const string DependenciesSection = DependenciesConfig.Section;
    private const string FeedsKey = "feeds";
    private const string PackagesKey = "packages";

    private string? ExplicitConfigFilePath { get; }
    private JsonObject? cachedConfig;

    internal GlobalConfigManager(
        ILogger<GlobalConfigManager> logger,
        ConfigFileWriter configFileWriter,
        string configFilePath) : this(logger, configFileWriter)
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
    /// Loads the global configuration file; a missing file reads as an empty document.
    /// </summary>
    /// <remarks>
    /// Reading never creates the file: its absence is how the interactive menu detects a first run.
    /// </remarks>
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
            return [];
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
    /// Saves the global configuration file and reloads the configuration.
    /// </summary>
    /// <remarks>
    /// revela.json can hold private feed URLs, so it is always written owner-only on Unix.
    /// </remarks>
    private async Task SaveFileAsync(JsonObject config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConfiguration(config.ToJsonString());

        // A failed write or reload must not leave a cache that differs from the file
        cachedConfig = null;
        await configFileWriter.WriteAsync(
            ConfigFilePath,
            config,
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            cancellationToken);
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
        var feeds = GetMapping(GetSection(config, DependenciesSection)!, FeedsKey)!;

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

        return await RemoveEntryAsync(FeedsKey, name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddPackageAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        GetMapping(GetSection(config, DependenciesSection)!, PackagesKey)![packageId] = version;
        await SaveFileAsync(config, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RemovePackageAsync(string packageId, CancellationToken cancellationToken = default) =>
        RemoveEntryAsync(PackagesKey, packageId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string?>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var dependencies = GetSection(config, DependenciesSection, create: false);
        var packages = dependencies is null ? null : GetMapping(dependencies, PackagesKey, create: false);
        return packages?.ToDictionary(property => property.Key, property => property.Value?.GetValue<string>(), StringComparer.Ordinal)
            ?? [];
    }

    private async Task<bool> RemoveEntryAsync(string mappingName, string key, CancellationToken cancellationToken)
    {
        var config = (JsonObject)(await LoadFileAsync(cancellationToken)).DeepClone();
        var dependencies = GetSection(config, DependenciesSection, create: false);
        var mapping = dependencies is null ? null : GetMapping(dependencies, mappingName, create: false);

        if (mapping is null || !mapping.Remove(key))
        {
            return false;
        }

        await SaveFileAsync(config, cancellationToken);
        return true;
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded config from '{ConfigPath}'")]
    private partial void LogConfigLoaded(string configPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config file '{ConfigPath}' is invalid ({Error}), refusing to overwrite it")]
    private partial void LogConfigCorrupted(string configPath, string error);

    #endregion
}
