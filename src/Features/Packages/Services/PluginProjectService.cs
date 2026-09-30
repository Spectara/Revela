using System.Text.Json.Nodes;
using Spectara.Revela.Core.Logging;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages package entries (<c>dependencies.packages</c>) in project.json.
/// </summary>
/// <remarks>
/// Operations are no-ops when project.json doesn't exist (optional feature).
/// </remarks>
public sealed class PluginProjectService(
    IConfigService configService,
    ILogger<PluginProjectService> logger)
{
    private const string PackagesKey = "packages";

    /// <summary>
    /// Adds or updates a package entry in project.json.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The exact installed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddPackageAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configService.IsProjectInitialized())
        {
            return;
        }

        await configService.UpdateProjectConfigAsync(CreatePatch(packageId, version), cancellationToken);
        logger.PluginAdded(configService.ProjectConfigPath, packageId, version);
    }

    /// <summary>
    /// Removes a package entry from project.json.
    /// </summary>
    /// <param name="packageId">The package ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RemovePackageAsync(string packageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configService.IsProjectInitialized())
        {
            return;
        }

        await configService.UpdateProjectConfigAsync(CreatePatch(packageId, version: null), cancellationToken);
        logger.PluginRemoved(configService.ProjectConfigPath, packageId);
    }

    private static JsonObject CreatePatch(string packageId, string? version) => new()
    {
        [DependenciesConfig.Section] = new JsonObject
        {
            [PackagesKey] = new JsonObject { [packageId] = version }
        }
    };
}
