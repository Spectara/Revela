using System.Text.Json.Nodes;
using Spectara.Revela.Commands.Config.Models;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Service for reading and writing Revela configuration files.
/// </summary>
/// <remarks>
/// Provides abstraction over project.json and site.json with:
/// - Strongly-typed DTO access (ProjectConfigDto, SiteConfigDto)
/// - Partial updates (only non-null properties are written)
/// Reusable by Core commands and Plugins.
/// </remarks>
public interface IConfigService
{
    /// <summary>
    /// Checks if the current directory contains an initialized Revela project.
    /// </summary>
    /// <returns>True if project.json exists.</returns>
    bool IsProjectInitialized();

    /// <summary>
    /// Checks if the current directory contains a site configuration.
    /// </summary>
    /// <returns>True if site.json exists.</returns>
    bool IsSiteConfigured();

    /// <summary>
    /// Gets the path to project.json in the current directory.
    /// </summary>
    string ProjectConfigPath { get; }

    /// <summary>
    /// Gets the path to site.json in the current directory.
    /// </summary>
    string SiteConfigPath { get; }

    /// <summary>
    /// Reads the project.json configuration as a strongly-typed DTO.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed configuration, or null if file doesn't exist.</returns>
    Task<ProjectConfigDto?> ReadProjectConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the site.json configuration as a strongly-typed DTO.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed configuration, or null if file doesn't exist.</returns>
    Task<SiteConfigDto?> ReadSiteConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the project.json configuration.
    /// Only non-null properties in the update DTO are written.
    /// </summary>
    /// <param name="update">The properties to update (null = keep existing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProjectConfigAsync(ProjectConfigDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the site.json configuration.
    /// Only non-null properties in the update DTO are written.
    /// </summary>
    /// <param name="update">The properties to update (null = keep existing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSiteConfigAsync(SiteConfigDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire project.json configuration as raw JSON.
    /// Used for display purposes (config show command).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw JSON object, or null if file doesn't exist.</returns>
    Task<JsonObject?> ReadProjectConfigRawAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire site.json configuration as raw JSON.
    /// Used for display purposes (config show command).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw JSON object, or null if file doesn't exist.</returns>
    Task<JsonObject?> ReadSiteConfigRawAsync(CancellationToken cancellationToken = default);
}
