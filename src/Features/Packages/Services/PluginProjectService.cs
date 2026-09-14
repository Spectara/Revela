using System.Text.Json.Nodes;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin entries in project.json.
/// </summary>
/// <remarks>
/// Handles reading and writing the "plugins" section of project.json.
/// Operations are no-ops when project.json doesn't exist (optional feature).
/// </remarks>
public sealed class PluginProjectService(
    IConfigService configService,
    ILogger<PluginProjectService> logger)
{
    /// <summary>
    /// Adds or updates a plugin entry in project.json.
    /// </summary>
    /// <param name="packageId">The plugin package ID.</param>
    /// <param name="version">The plugin version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddPluginAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configService.IsProjectInitialized())
        {
            return;
        }

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["plugins"] = new JsonObject { [packageId] = version } },
            cancellationToken);
        logger.PluginAdded(configService.ProjectConfigPath, packageId, version);
    }

    /// <summary>
    /// Removes a plugin entry from project.json.
    /// </summary>
    /// <param name="packageId">The plugin package ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RemovePluginAsync(string packageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configService.IsProjectInitialized())
        {
            return;
        }

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["plugins"] = new JsonObject { [packageId] = null } },
            cancellationToken);
        logger.PluginRemoved(configService.ProjectConfigPath, packageId);
    }
}
