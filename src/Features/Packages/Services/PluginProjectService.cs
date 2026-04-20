using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin entries in project.json.
/// </summary>
/// <remarks>
/// Handles reading and writing the "plugins" section of project.json.
/// Operations are no-ops when project.json doesn't exist (optional feature).
/// </remarks>
public sealed class PluginProjectService(
    IOptions<ProjectEnvironment> projectEnvironment,
    ILogger<PluginProjectService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Adds or updates a plugin entry in project.json.
    /// </summary>
    /// <param name="packageId">The plugin package ID.</param>
    /// <param name="version">The plugin version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        try
        {
            if (await ModifyPluginsAsync(plugins =>
            {
                plugins[packageId] = version;
                return true;
            }, cancellationToken))
            {
                logger.PluginAdded(ProjectJsonPath, packageId, version);
            }
        }
        catch (Exception ex)
        {
            logger.AddPluginFailed(ex, ProjectJsonPath);
        }
    }

    /// <summary>
    /// Removes a plugin entry from project.json.
    /// </summary>
    /// <param name="packageId">The plugin package ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RemovePluginAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            if (await ModifyPluginsAsync(plugins => plugins.Remove(packageId), cancellationToken))
            {
                logger.PluginRemoved(ProjectJsonPath, packageId);
            }
        }
        catch (Exception ex)
        {
            logger.RemovePluginFailed(ex, ProjectJsonPath, packageId);
        }
    }

    private string ProjectJsonPath =>
        Path.Combine(projectEnvironment.Value.Path, "project.json");

    /// <summary>
    /// Modifies the plugins section of project.json using a transform function.
    /// </summary>
    /// <remarks>
    /// Silently skips if project.json doesn't exist (optional feature).
    /// Reads, modifies, and writes back with pretty-print formatting.
    /// </remarks>
    private async Task<bool> ModifyPluginsAsync(
        Func<Dictionary<string, string>, bool> transform,
        CancellationToken cancellationToken)
    {
        var projectJsonPath = ProjectJsonPath;
        if (!File.Exists(projectJsonPath))
        {
            return false;
        }

        var jsonText = await File.ReadAllTextAsync(projectJsonPath, cancellationToken);
        var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);
        if (root is null)
        {
            return false;
        }

        if (!root.TryGetValue("plugins", out var pluginsElement))
        {
            pluginsElement = JsonSerializer.SerializeToElement(new Dictionary<string, string>());
        }

        var plugins = pluginsElement.Deserialize<Dictionary<string, string>>() ?? [];

        if (!transform(plugins))
        {
            return false;
        }

        root["plugins"] = JsonSerializer.SerializeToElement(plugins);

        await using var writeStream = File.Create(projectJsonPath);
        await JsonSerializer.SerializeAsync(writeStream, root, JsonOptions, cancellationToken);
        return true;
    }
}
