using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Default implementation of <see cref="IConfigService"/>.
/// </summary>
/// <remarks>
/// Provides JSON configuration file management with:
/// - JsonObject-based access
/// - Deep merge for partial updates
/// - Pretty-printed output
/// - Automatic IConfiguration reload and IOptionsMonitor cache invalidation after writes
/// </remarks>
public sealed partial class ConfigService(
    ILogger<ConfigService> logger,
    IConfiguration configuration,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitorCache<ThemeConfig> themeCache,
    IOptionsMonitorCache<ProjectConfig> projectCache,
    IOptionsMonitorCache<GenerateConfig> generateCache,
    IOptionsMonitorCache<DependenciesConfig> dependenciesCache) : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public string ProjectConfigPath => Path.Combine(projectEnvironment.Value.Path, "project.json");

    /// <inheritdoc />
    public string SiteConfigPath => Path.Combine(projectEnvironment.Value.Path, "site.json");

    /// <inheritdoc />
    public bool IsProjectInitialized() => File.Exists(ProjectConfigPath);

    /// <inheritdoc />
    public bool IsSiteConfigured() => File.Exists(SiteConfigPath);

    /// <inheritdoc />
    public async Task<JsonObject?> ReadProjectConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ProjectConfigPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ProjectConfigPath, cancellationToken).ConfigureAwait(false);
            return JsonNode.Parse(json)?.AsObject();
        }
        catch (JsonException ex)
        {
            LogReadFailed(ProjectConfigPath, ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UpdateProjectConfigAsync(JsonObject updates, CancellationToken cancellationToken = default)
    {
        var existing = await ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false) ?? [];

        // Deep merge updates into existing
        DeepMerge(existing, updates);

        var json = existing.ToJsonString(JsonOptions);
        await File.WriteAllTextAsync(ProjectConfigPath, json, cancellationToken).ConfigureAwait(false);

        // Force IConfiguration to reload from file immediately (don't wait for FileSystemWatcher)
        // Then invalidate IOptionsMonitor caches so CurrentValue returns fresh data
        ReloadConfigurationAndInvalidateCaches();

        LogConfigUpdated(ProjectConfigPath);
    }

    /// <summary>
    /// Reloads configuration from files and invalidates all IOptionsMonitor caches.
    /// </summary>
    /// <remarks>
    /// This is needed for immediate in-process updates (e.g., wizard flows).
    /// Without explicit Reload(), the FileSystemWatcher has a delay before detecting changes.
    /// </remarks>
    private void ReloadConfigurationAndInvalidateCaches()
    {
        // Force configuration to reload from all sources
        (configuration as IConfigurationRoot)?.Reload();

        // Invalidate caches (BindConfiguration registered change tokens, so this triggers re-bind)
        InvalidateProjectConfigCaches();
    }

    /// <summary>
    /// Invalidates all IOptionsMonitor caches that depend on project.json.
    /// </summary>
    private void InvalidateProjectConfigCaches()
    {
        themeCache.TryRemove(Options.DefaultName);
        projectCache.TryRemove(Options.DefaultName);
        generateCache.TryRemove(Options.DefaultName);
        dependenciesCache.TryRemove(Options.DefaultName);
    }

    /// <inheritdoc />
    public async Task<JsonObject?> ReadSiteConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SiteConfigPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(SiteConfigPath, cancellationToken).ConfigureAwait(false);
            return JsonNode.Parse(json)?.AsObject();
        }
        catch (JsonException ex)
        {
            LogReadFailed(SiteConfigPath, ex);
            return null;
        }
    }

    /// <summary>
    /// Deep merges source into target. Source values override target values.
    /// Objects are merged recursively, arrays and primitives are replaced.
    /// Null values in source remove the key from target.
    /// </summary>
    private static void DeepMerge(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (property.Value is null)
            {
                // Null value means "remove this key"
                target.Remove(property.Key);
            }
            else if (property.Value is JsonObject sourceObj &&
                target[property.Key] is JsonObject targetObj)
            {
                // Both are objects: merge recursively
                DeepMerge(targetObj, sourceObj);
            }
            else
            {
                // Replace value (clone to avoid parent issues)
                target[property.Key] = property.Value.DeepClone();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updated configuration: {ConfigPath}")]
    private partial void LogConfigUpdated(string configPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read configuration from {Path}")]
    private partial void LogReadFailed(string path, Exception exception);
}
