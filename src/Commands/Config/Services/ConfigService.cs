using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Json;

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
internal sealed partial class ConfigService(
    ILogger<ConfigService> logger,
    IConfiguration configuration,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitorCache<ThemeConfig> themeCache,
    IOptionsMonitorCache<ProjectConfig> projectCache,
    IOptionsMonitorCache<GenerateConfig> generateCache,
    IOptionsMonitorCache<DependenciesConfig> dependenciesCache) : IConfigService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim updateGate = new(1, 1);
    private readonly Lock lifetimeGate = new();
    private int pendingUpdates;
    private bool disposed;

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
            var json = await File.ReadAllTextAsync(ProjectConfigPath, cancellationToken);
            return JsonNode.Parse(json, nodeOptions: null, RevelaJsonOptions.LenientDocument)?.AsObject();
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
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            pendingUpdates++;
        }

        try
        {
            await updateGate.WaitAsync(cancellationToken);
            try
            {
                await UpdateProjectConfigCoreAsync(updates, cancellationToken);
            }
            finally
            {
                updateGate.Release();
            }
        }
        finally
        {
            lock (lifetimeGate)
            {
                pendingUpdates--;
                if (disposed && pendingUpdates == 0)
                {
                    updateGate.Dispose();
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (lifetimeGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (pendingUpdates == 0)
            {
                updateGate.Dispose();
            }
        }
    }

    private async Task UpdateProjectConfigCoreAsync(JsonObject updates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = await ReadProjectConfigForUpdateAsync(cancellationToken);
        var original = existing.DeepClone();

        // Deep merge updates into existing
        DeepMerge(existing, updates);

        cancellationToken.ThrowIfCancellationRequested();
        if (JsonNode.DeepEquals(original, existing))
        {
            return;
        }

        var json = Encoding.UTF8.GetBytes(existing.ToJsonString(JsonOptions));
        ValidateProjectConfiguration(json);
        var directory = Path.GetDirectoryName(ProjectConfigPath)
            ?? throw new InvalidOperationException("Project configuration path has no parent directory.");
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(ProjectConfigPath)}.{Guid.NewGuid():N}.tmp");
        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        var originalMode = (UnixFileMode?)null;
        if (!OperatingSystem.IsWindows())
        {
            streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if (File.Exists(ProjectConfigPath))
            {
                originalMode = File.GetUnixFileMode(ProjectConfigPath);
            }
        }

        try
        {
            await using (var stream = new FileStream(tempPath, streamOptions))
            {
                await stream.WriteAsync(json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows() && originalMode is { } mode)
            {
                File.SetUnixFileMode(tempPath, mode);
            }

            await AtomicFileReplace.ReplaceAsync(tempPath, ProjectConfigPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        // Configuration sources don't watch files: reload explicitly so this process sees the change,
        // then invalidate IOptionsMonitor caches so CurrentValue returns fresh data
        ReloadConfigurationAndInvalidateCaches();

        LogConfigUpdated(ProjectConfigPath);
    }

    private async Task<JsonObject> ReadProjectConfigForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllBytesAsync(ProjectConfigPath, cancellationToken);
            ValidateProjectConfiguration(json);
            using var stream = new MemoryStream(json, writable: false);
            return (await JsonNode.ParseAsync(stream, nodeOptions: null, RevelaJsonOptions.LenientDocument, cancellationToken))?.AsObject()
                ?? throw new JsonException("Project configuration must be a JSON object.");
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    private static void ValidateProjectConfiguration(byte[] json)
    {
        using var stream = new MemoryStream(json, writable: false);
        using var validation = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
    }

    /// <summary>
    /// Reloads configuration from files and invalidates all IOptionsMonitor caches.
    /// </summary>
    /// <remarks>
    /// This is needed for immediate in-process updates (e.g., wizard flows): configuration
    /// sources don't watch files, so nothing else picks up the change.
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
            var json = await File.ReadAllTextAsync(SiteConfigPath, cancellationToken);
            return JsonNode.Parse(json, nodeOptions: null, RevelaJsonOptions.LenientDocument)?.AsObject();
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
            var key = ResolveExistingKey(target, property.Key);
            if (property.Value is null)
            {
                // Null value means "remove this key"
                target.Remove(key);
            }
            else if (property.Value is JsonObject sourceObj)
            {
                if (target[key] is JsonObject targetObj)
                {
                    DeepMerge(targetObj, sourceObj);
                }
                else
                {
                    var merged = new JsonObject();
                    DeepMerge(merged, sourceObj);
                    if (merged.Count > 0 || sourceObj.Count == 0)
                    {
                        target[key] = merged;
                    }
                }
            }
            else
            {
                // Replace value (clone to avoid parent issues)
                target[key] = property.Value.DeepClone();
            }
        }
    }

    private static string ResolveExistingKey(JsonObject target, string key)
    {
        var matchingKey = (string?)null;
        foreach (var property in target)
        {
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                if (matchingKey is not null)
                {
                    throw new InvalidOperationException(
                        $"Cannot update configuration key '{key}' because multiple existing keys differ only by case.");
                }

                matchingKey = property.Key;
            }
        }

        return matchingKey ?? key;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updated configuration: {ConfigPath}")]
    private partial void LogConfigUpdated(string configPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read configuration from {Path}")]
    private partial void LogReadFailed(string path, Exception exception);
}
