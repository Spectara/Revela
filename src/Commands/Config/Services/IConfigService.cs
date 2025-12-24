using System.Text.Json.Nodes;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Service for reading and writing Revela configuration files.
/// </summary>
/// <remarks>
/// Provides abstraction over project.json with:
/// - JSON-based access via JsonObject
/// - Deep merge for partial updates (only provided properties are written)
/// Reusable by Core commands and Plugins.
/// Note: site.json is theme-dependent and handled dynamically via JsonPropertyExtractor.
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
    /// Reads the project.json configuration as JSON.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed JSON object, or null if file doesn't exist.</returns>
    Task<JsonObject?> ReadProjectConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the project.json configuration with deep merge.
    /// Only provided properties are updated, existing properties are preserved.
    /// </summary>
    /// <param name="updates">The properties to update (deep merged with existing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateProjectConfigAsync(JsonObject updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the site.json configuration as JSON.
    /// Used for display purposes (config show command).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JSON object, or null if file doesn't exist.</returns>
    Task<JsonObject?> ReadSiteConfigAsync(CancellationToken cancellationToken = default);
}
