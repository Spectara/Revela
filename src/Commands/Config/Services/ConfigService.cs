using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Json;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Default implementation of <see cref="IConfigService"/>.
/// </summary>
/// <remarks>
/// Provides JSON configuration file management with:
/// - JsonObject-based access
/// - Deep merge for partial updates, serialized per instance
/// - Validated, atomic writes through <see cref="ConfigFileWriter"/>, which also reloads the
///   configuration (and with it every <c>IOptionsMonitor&lt;T&gt;</c>)
/// </remarks>
internal sealed partial class ConfigService(
    ILogger<ConfigService> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    ConfigFileWriter configFileWriter) : IConfigService, IDisposable
{
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

        await configFileWriter.WriteAsync(ProjectConfigPath, existing, cancellationToken: cancellationToken);

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
