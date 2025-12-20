using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Spectara.Revela.Commands.Config.Models;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Default implementation of <see cref="IConfigService"/>.
/// </summary>
/// <remarks>
/// Provides JSON configuration file management with:
/// - Strongly-typed DTO serialization
/// - Partial updates (null properties preserved)
/// - Pretty-printed output
/// </remarks>
public sealed partial class ConfigService(ILogger<ConfigService> logger) : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public string ProjectConfigPath => Path.Combine(Directory.GetCurrentDirectory(), "project.json");

    /// <inheritdoc />
    public string SiteConfigPath => Path.Combine(Directory.GetCurrentDirectory(), "site.json");

    /// <inheritdoc />
    public bool IsProjectInitialized() => File.Exists(ProjectConfigPath);

    /// <inheritdoc />
    public bool IsSiteConfigured() => File.Exists(SiteConfigPath);

    /// <inheritdoc />
    public async Task<ProjectConfigDto?> ReadProjectConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ProjectConfigPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ProjectConfigPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ProjectConfigDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogReadFailed(ProjectConfigPath, ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SiteConfigDto?> ReadSiteConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SiteConfigPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(SiteConfigPath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SiteConfigDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogReadFailed(SiteConfigPath, ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task UpdateProjectConfigAsync(ProjectConfigDto update, CancellationToken cancellationToken = default)
    {
        var existing = await ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false) ?? new ProjectConfigDto();

        // Merge: only update non-null properties
        var merged = new ProjectConfigDto
        {
            Name = update.Name ?? existing.Name,
            Url = update.Url ?? existing.Url,
            Theme = update.Theme ?? existing.Theme,
            Plugins = update.Plugins ?? existing.Plugins,
            ImageBasePath = update.ImageBasePath ?? existing.ImageBasePath,
            BasePath = update.BasePath ?? existing.BasePath,
            Generate = MergeGenerateConfig(existing.Generate, update.Generate)
        };

        var json = JsonSerializer.Serialize(merged, JsonOptions);
        await File.WriteAllTextAsync(ProjectConfigPath, json, cancellationToken).ConfigureAwait(false);
        LogConfigUpdated(ProjectConfigPath);
    }

    /// <inheritdoc />
    public async Task UpdateSiteConfigAsync(SiteConfigDto update, CancellationToken cancellationToken = default)
    {
        var existing = await ReadSiteConfigAsync(cancellationToken).ConfigureAwait(false) ?? new SiteConfigDto();

        // Merge: only update non-null properties
        var merged = new SiteConfigDto
        {
            Title = update.Title ?? existing.Title,
            Author = update.Author ?? existing.Author,
            Description = update.Description ?? existing.Description,
            Copyright = update.Copyright ?? existing.Copyright
        };

        var json = JsonSerializer.Serialize(merged, JsonOptions);
        await File.WriteAllTextAsync(SiteConfigPath, json, cancellationToken).ConfigureAwait(false);
        LogConfigUpdated(SiteConfigPath);
    }

    /// <inheritdoc />
    public async Task<JsonObject?> ReadProjectConfigRawAsync(CancellationToken cancellationToken = default)
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
    public async Task<JsonObject?> ReadSiteConfigRawAsync(CancellationToken cancellationToken = default)
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
    /// Merges generate configuration, handling nested objects.
    /// </summary>
    private static GenerateConfigDto? MergeGenerateConfig(GenerateConfigDto? existing, GenerateConfigDto? update)
    {
        if (update is null)
        {
            return existing;
        }

        if (existing is null)
        {
            return update;
        }

        return new GenerateConfigDto
        {
            Output = update.Output ?? existing.Output,
            Images = MergeImageConfig(existing.Images, update.Images)
        };
    }

    /// <summary>
    /// Merges image configuration.
    /// Note: Formats and Sizes are replaced entirely if provided (not merged).
    /// </summary>
    private static ImageConfigDto? MergeImageConfig(ImageConfigDto? existing, ImageConfigDto? update)
    {
        if (update is null)
        {
            return existing;
        }

        if (existing is null)
        {
            return update;
        }

        return new ImageConfigDto
        {
            // Formats and Sizes: replace entirely if provided
            Formats = update.Formats ?? existing.Formats,
            Sizes = update.Sizes ?? existing.Sizes,
            MinWidth = update.MinWidth ?? existing.MinWidth,
            MinHeight = update.MinHeight ?? existing.MinHeight
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updated configuration: {ConfigPath}")]
    private partial void LogConfigUpdated(string configPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read configuration from {Path}")]
    private partial void LogReadFailed(string path, Exception exception);
}
